using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OniAgent.Settings;
using OniAgent.Snapshot;
using UnityEngine;

namespace OniAgent.Networking
{
    // Event-driven counterpart to LedgyxPushClient: that client resends the
    // full operational snapshot on a cron timer, which is wrong for this
    // tier (a lost critical event is genuinely lost, not just stale until
    // the next tick). Instead, SnapshotTicker.LateUpdate (Unity main thread)
    // calls Enqueue() the moment CriticalEventCollector reports something
    // new; a single dedicated background thread blocks on the queue and
    // POSTs as soon as anything arrives, draining whatever else queued up
    // in the meantime into the same request. Ledgyx confirmed a batched
    // array insert still fires one AI_AGENT-trigger subscription per row
    // (see oni-ledgyx-batch-insert-fires-per-row-subscription-trigger-
    // critical-event-tier), so batching here loses no downstream behavior.
    //
    // The wire body is a CriticalEventResponse (SchemaVersion + Events[]),
    // same shape GET /api/snapshot/critical returns — matches what the
    // Ledgyx insert endpoint was actually built and tested against (a
    // top-level SchemaVersion sibling to the Events array, not per-row, and
    // not a bare array). See the mismatch this fixed: an earlier version of
    // this file POSTed a bare List<CriticalEvent>, which the endpoint also
    // accepted (rows landed), but without the wrapper Ledgyx's insert logic
    // wasn't actually tested against — this reverts to the tested shape.
    //
    // A failed POST logs and drops the batch — same posture as
    // LedgyxPushClient, no retry/redelivery. Out of scope for now; the
    // GET /api/snapshot/critical rolling window (50 events) is the only
    // redundant path if a push is lost.
    public class CriticalEventPushClient
    {
        private readonly AgentSettings settings;
        private readonly BlockingCollection<CriticalEvent> queue = new BlockingCollection<CriticalEvent>();
        private Thread worker;
        private bool warnedMissingConfig;

        public CriticalEventPushClient(AgentSettings settings)
        {
            this.settings = settings;
        }

        public void Start()
        {
            worker = new Thread(Run) { IsBackground = true, Name = "OniAgentCriticalPush" };
            worker.Start();
            Debug.Log("[OniAgent] CriticalEventPushClient started.");
        }

        public void Stop()
        {
            queue.CompleteAdding();
            worker?.Join(5000);
            worker = null;
        }

        // Safe to call from the Unity main thread; BlockingCollection's Add
        // is thread-safe and never blocks the caller here (unbounded).
        public void Enqueue(List<CriticalEvent> events)
        {
            foreach (var evt in events)
            {
                queue.Add(evt);
            }
        }

        private void Run()
        {
            try
            {
                foreach (var first in queue.GetConsumingEnumerable())
                {
                    var batch = new List<CriticalEvent> { first };
                    while (queue.TryTake(out var more))
                    {
                        batch.Add(more);
                    }

                    try
                    {
                        Push(batch);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[OniAgent] CriticalEventPushClient push failed: " + e);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // Unity aborts still-live background threads on domain/process
                // teardown (e.g. quitting the game while this thread is
                // blocked in GetConsumingEnumerable()). The CLR auto-rethrows
                // ThreadAbortException at the end of any catch block unless
                // ResetAbort() is called here — and an unhandled exception on
                // a non-main thread crashes the whole process by default in
                // .NET, not just this thread. This isn't an error, so it's
                // swallowed rather than logged.
                Thread.ResetAbort();
            }
            catch (Exception e)
            {
                Debug.LogError("[OniAgent] CriticalEventPushClient worker crashed: " + e);
            }
        }

        private void Push(List<CriticalEvent> batch)
        {
            if (string.IsNullOrEmpty(settings.ApiKey) || string.IsNullOrEmpty(settings.CriticalEventsEndpoint))
            {
                if (!warnedMissingConfig)
                {
                    Debug.LogWarning("[OniAgent] CriticalEventPushClient: ApiKey or CriticalEventsEndpoint not set in settings.json, dropping critical events.");
                    warnedMissingConfig = true;
                }
                return;
            }

            var payload = new CriticalEventResponse
            {
                SchemaVersion = CriticalEventCollector.SchemaVersion,
                Events = batch,
            };
            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

            var request = (HttpWebRequest)WebRequest.Create(settings.CriticalEventsEndpoint);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.UserAgent = "OniAgentMod/1.0";
            request.Headers["X-API-Key"] = settings.ApiKey;
            request.Timeout = 15000;
            request.ContentLength = body.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    Debug.Log("[OniAgent] CriticalEventPushClient: pushed " + batch.Count + " event(s) (" + (int)response.StatusCode + ")");
                }
            }
            catch (WebException e)
            {
                var status = (e.Response as HttpWebResponse)?.StatusCode;
                Debug.LogWarning("[OniAgent] CriticalEventPushClient: push rejected"
                    + (status != null ? " (" + (int)status + ")" : "") + ": " + e.Message);
            }
        }
    }
}
