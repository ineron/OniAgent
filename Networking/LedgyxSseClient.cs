using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniAgent.Commands;
using OniAgent.Settings;
using OniAgent.Snapshot;
using UnityEngine;

namespace OniAgent.Networking
{
    // Task #8: outbound SSE client that receives agent-run results from
    // Ledgyx. Ledgyx hosts the SSE endpoint, not the mod — the mod runs on
    // end-user machines behind NAT with no inbound access, so a webhook push
    // from Ledgyx to the mod isn't reachable, but an outbound long-lived GET
    // from the mod is (see oni-ledgyx-integration-design-push-snapshot-
    // trigger-agent-sse-notify-loop). HttpListener (used by ApiServer) is
    // server-only and can't consume SSE, so this hand-rolls a line reader
    // over HttpWebRequest instead — still BCL-only, no external HTTP/SSE
    // deps, matching the rest of this project's networking code.
    //
    // Auth is token-in-query-string, confirmed against the working Obsidian
    // sse-notes-receiver precedent (2026-07-27) and deliberately temporary:
    // Ledgyx plans to move this to a header later, matching the X-API-Key
    // header already used on the push side. Don't switch this to a header
    // without confirming that migration happened first.
    //
    // Channel/type filtering: confirmed 2026-07-31 directly against the
    // FastAPI route signature (sse_server.py:271-274) — the shared /sse/
    // stream endpoint takes flat query params `channels` (comma-separated,
    // default "default" if omitted — our events are published to "oni", so
    // omitting this would silently get nothing) and `types` (comma-
    // separated, default unfiltered). Both are requested explicitly here, so
    // filtering happens server-side via the subscription itself — a message
    // payload is not guaranteed to carry its own "channel" field (an earlier
    // version of this file assumed it would and silently dropped every
    // oni_agent_run frame as a result, since the field was simply absent).
    // HandleAgentRun now only rejects an *explicit* channel mismatch, and
    // logs it, rather than treating a missing field as one.
    public class LedgyxSseClient
    {
        private const string Channel = "oni";
        private const string AgentRunEventType = "oni_agent_run";

        // Task #50 command channel: the agent issues dig/build orders by
        // calling the already-generic send_sse_notification tool with
        // event_type="oni_command" — deliberately reusing the platform's
        // existing native SSE push instead of a bespoke agent tool (see
        // ledgyx-admin-ui task #50 discussion). data payload matches
        // Commands.CommandBatchRequest's shape ({"commands":[...]}); extra
        // keys (e.g. "message") are ignored by Newtonsoft on deserialize.
        private const string CommandEventType = "oni_command";
        private const int MaxReconnectAttempts = 10;
        private const int MaxBackoffMs = 30000;

        private readonly AgentSettings settings;
        private Thread worker;
        private volatile bool running;
        private int reconnectAttempt;

        public LedgyxSseClient(AgentSettings settings)
        {
            this.settings = settings;
        }

        public void Start()
        {
            if (string.IsNullOrEmpty(settings.SseEndpoint) || string.IsNullOrEmpty(settings.SseToken))
            {
                Debug.LogWarning("[OniAgent] LedgyxSseClient: SseEndpoint or SseToken not set in settings.json, not starting.");
                return;
            }

            running = true;
            worker = new Thread(Run) { IsBackground = true, Name = "OniAgentSse" };
            worker.Start();
            Debug.Log("[OniAgent] LedgyxSseClient started.");
        }

        public void Stop()
        {
            running = false;
            worker?.Join(5000);
            worker = null;
        }

        // Reconnect loop: exponential backoff 1000ms*2^attempt capped at
        // 30s, give up after 10 attempts rather than retry forever (mirrors
        // the working Obsidian precedent). The attempt counter is only
        // reset by a "connected" frame actually arriving (see Dispatch), not
        // merely by Connect() returning — a connection that never completes
        // its handshake shouldn't get a fresh budget.
        private void Run()
        {
            while (running)
            {
                try
                {
                    Connect();
                }
                catch (ThreadAbortException)
                {
                    // Unity aborts still-live background threads on domain/
                    // process teardown; not an error, so swallowed rather
                    // than logged (same posture as CriticalEventPushClient).
                    Thread.ResetAbort();
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[OniAgent] LedgyxSseClient: connection error: " + e.Message);
                }

                if (!running)
                {
                    return;
                }

                reconnectAttempt++;
                if (reconnectAttempt > MaxReconnectAttempts)
                {
                    Debug.LogError("[OniAgent] LedgyxSseClient: giving up after " + MaxReconnectAttempts + " failed reconnect attempts.");
                    return;
                }

                var delayMs = Math.Min(1000 * (1 << (reconnectAttempt - 1)), MaxBackoffMs);
                Thread.Sleep(delayMs);
            }
        }

        // Blocks for the lifetime of the connection, dispatching each SSE
        // frame as it arrives. Returns (not throws) once the server closes
        // the stream cleanly; Run() treats that the same as any other drop.
        private void Connect()
        {
            var types = AgentRunEventType + "," + CommandEventType;
            var query = "token=" + Uri.EscapeDataString(settings.SseToken)
                + "&channels=" + Uri.EscapeDataString(Channel)
                + "&types=" + Uri.EscapeDataString(types);
            var url = settings.SseEndpoint + (settings.SseEndpoint.Contains("?") ? "&" : "?") + query;

            // Token redacted — this line exists so a support log can confirm
            // channels/types actually went out, without leaking the token.
            Debug.Log("[OniAgent] LedgyxSseClient: connecting to " + settings.SseEndpoint
                + "?token=<redacted>&channels=" + Channel + "&types=" + types);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Accept = "text/event-stream";
            request.UserAgent = "OniAgentMod/1.0";
            request.KeepAlive = true;
            request.Timeout = 15000; // only governs establishing the connection
            request.ReadWriteTimeout = Timeout.Infinite; // heartbeats keep the stream alive indefinitely

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string currentEvent = null;
                var data = new StringBuilder();
                string line;
                while (running && (line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0)
                    {
                        if (currentEvent != null)
                        {
                            Dispatch(currentEvent, data.ToString());
                        }
                        currentEvent = null;
                        data.Length = 0;
                        continue;
                    }

                    if (line.StartsWith(":"))
                    {
                        continue; // comment / keepalive padding, not a field
                    }

                    if (line.StartsWith("event:"))
                    {
                        currentEvent = line.Substring(6).Trim();
                    }
                    else if (line.StartsWith("data:"))
                    {
                        if (data.Length > 0)
                        {
                            data.Append('\n');
                        }
                        data.Append(line.Substring(5).Trim());
                    }
                    // id:/retry: fields are ignored — nothing here needs
                    // Last-Event-ID resume semantics yet.
                }
            }
        }

        private void Dispatch(string eventType, string data)
        {
            switch (eventType)
            {
                case "connected":
                    Debug.Log("[OniAgent] LedgyxSseClient: connected.");
                    reconnectAttempt = 0;
                    break;
                case "heartbeat":
                    break;
                case AgentRunEventType:
                    HandleAgentRun(data);
                    break;
                case CommandEventType:
                    HandleCommand(data);
                    break;
                default:
                    Debug.Log("[OniAgent] LedgyxSseClient: ignoring unhandled event type '" + eventType + "'.");
                    break;
            }
        }

        private void HandleAgentRun(string json)
        {
            try
            {
                var envelope = JObject.Parse(json);
                var channelToken = envelope["channel"];
                // Channel scoping actually happens server-side via the
                // /sse/stream subscription's `channels=oni` query param
                // (confirmed 2026-07-31 against sse_server.py), not a
                // per-message field — so a missing "channel" key is
                // expected, not a reason to drop. Only reject an explicit
                // mismatch, and log it: silently returning here previously
                // meant a real event could vanish with zero trace.
                if (channelToken != null && (string)channelToken != Channel)
                {
                    Debug.LogWarning("[OniAgent] LedgyxSseClient: dropping oni_agent_run frame for unexpected channel '"
                        + (string)channelToken + "' (expected '" + Channel + "').");
                    return;
                }

                var result = new AgentRunResult
                {
                    Cycle = envelope.Value<int?>("cycle") ?? -1,
                    Summary = (string)envelope["summary"],
                    RawJson = json,
                    ReceivedAt = System.DateTime.UtcNow,
                };
                SnapshotCache.LatestAgentRunResult = result;
                Debug.Log("[OniAgent] LedgyxSseClient: received agent run result (cycle=" + result.Cycle + "): " + result.Summary);
            }
            catch (Exception e)
            {
                Debug.LogError("[OniAgent] LedgyxSseClient: failed to parse oni_agent_run payload: " + e + "\nraw: " + json);
            }
        }

        // Same no-blocking-here posture as the rest of this class: parsing
        // happens on this SSE worker thread, but applying the commands to
        // Grid/BuildingDef must happen on the Unity main thread — so this
        // only ever enqueues onto CommandQueue, same entry point ApiServer's
        // POST /api/command uses. CommandTicker.LateUpdate does the rest.
        private void HandleCommand(string json)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<CommandBatchRequest>(json);
                if (request == null || request.Commands == null || request.Commands.Count == 0)
                {
                    Debug.LogWarning("[OniAgent] LedgyxSseClient: oni_command payload had no commands, ignoring.\nraw: " + json);
                    return;
                }

                var batchId = "sse-" + Guid.NewGuid();
                CommandQueue.Enqueue(new PendingBatch { BatchId = batchId, Request = request });
                Debug.Log("[OniAgent] LedgyxSseClient: queued " + request.Commands.Count + " command(s) from SSE as batch " + batchId);
            }
            catch (Exception e)
            {
                Debug.LogError("[OniAgent] LedgyxSseClient: failed to parse oni_command payload: " + e + "\nraw: " + json);
            }
        }
    }
}
