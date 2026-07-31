using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OniAgent.Commands;
using OniAgent.Networking;
using OniAgent.Settings;
using OniAgent.Snapshot;
using UnityEngine;

namespace OniAgent
{
    public class ApiServer
    {
        private const string Prefix = "http://localhost:9813/";

        private readonly AgentSettings settings;
        private HttpListener listener;
        private Thread listenerThread;
        private volatile bool running;

        public ApiServer(AgentSettings settings)
        {
            this.settings = settings;
        }

        public void Start()
        {
            listener = new HttpListener();
            listener.Prefixes.Add(Prefix);
            listener.Start();
            running = true;
            listenerThread = new Thread(Listen) { IsBackground = true };
            listenerThread.Start();
            Debug.Log("[OniAgent] ApiServer listening on " + Prefix);
        }

        public void Stop()
        {
            running = false;
            listener?.Stop();
            listener?.Close();
        }

        private void Listen()
        {
            while (running)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (Exception)
                {
                    break; // listener was stopped
                }

                try
                {
                    Route(context);
                }
                catch (Exception e)
                {
                    Debug.LogError("[OniAgent] ApiServer request error: " + e);
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
            }
        }

        private void Route(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/snapshot/duplicants")
            {
                var snapshot = SnapshotCache.LatestDuplicants
                    ?? new DuplicantSnapshotResponse { SchemaVersion = SnapshotCollector.SchemaVersion, Cycle = -1 };
                WriteJson(response, snapshot);
                return;
            }

            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/snapshot/colony")
            {
                var snapshot = SnapshotCache.LatestColony
                    ?? new ColonySnapshot { SchemaVersion = ColonySnapshotCollector.SchemaVersion, Cycle = -1 };
                WriteJson(response, snapshot);
                return;
            }

            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/snapshot/critical")
            {
                var snapshot = SnapshotCache.RecentCriticalEvents
                    ?? new CriticalEventResponse { SchemaVersion = CriticalEventCollector.SchemaVersion };
                WriteJson(response, snapshot);
                return;
            }

            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/snapshot/environmental")
            {
                var snapshot = SnapshotCache.LatestEnvironmental
                    ?? new EnvironmentalSnapshotResponse
                    {
                        SchemaVersion = EnvironmentalSnapshotCollector.SchemaVersion,
                        Cycle = -1,
                        SectorSizeCells = settings.EnvironmentalSectorSizeCells,
                    };
                WriteJson(response, snapshot);
                return;
            }

            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/agent-run/latest")
            {
                var result = SnapshotCache.LatestAgentRunResult
                    ?? new AgentRunResult { Cycle = -1 };
                WriteJson(response, result);
                return;
            }

            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/settings")
            {
                WriteJson(response, new
                {
                    LedgyxEndpoint = settings.LedgyxEndpoint,
                    ApiKeySet = !string.IsNullOrEmpty(settings.ApiKey),
                    OperationalCadenceSeconds = settings.OperationalCadenceSeconds,
                    PushCadenceSeconds = settings.PushCadenceSeconds
                });
                return;
            }

            // POST /api/command — manual test entry point for Stage 2 (dig/build).
            // Body: {"commands":[{"type":"dig_rect","x1":..,"x2":..,"y1":..,"y2":..}, {"type":"build","building":"Ladder","x":..,"y":..}, ...]}
            // Coordinates are cell offsets relative to the Duplicant Printing Pod.
            // Only enqueues — CommandTicker executes on the main thread next frame.
            if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/api/command")
            {
                string body = ReadBody(request);
                CommandBatchRequest parsed;
                try
                {
                    parsed = JsonConvert.DeserializeObject<CommandBatchRequest>(body) ?? new CommandBatchRequest();
                }
                catch (Exception e)
                {
                    response.StatusCode = 400;
                    WriteJson(response, new { success = false, message = "Invalid JSON: " + e.Message });
                    return;
                }

                string batchId = Guid.NewGuid().ToString();
                CommandQueue.Enqueue(new PendingBatch { BatchId = batchId, Request = parsed });
                WriteJson(response, new { success = true, batch_id = batchId, queued = parsed.Commands.Count });
                return;
            }

            // GET /api/command/result?batch_id=... — poll for a specific batch's outcome.
            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/command/result")
            {
                string batchId = request.QueryString["batch_id"];
                if (string.IsNullOrEmpty(batchId) || !CommandResultCache.TryGet(batchId, out var result))
                {
                    WriteJson(response, new { success = true, found = false });
                    return;
                }
                WriteJson(response, new { success = true, found = true, data = result });
                return;
            }

            // GET /api/command/results — most recent batches, newest first (dev convenience).
            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/command/results")
            {
                WriteJson(response, new { success = true, data = CommandResultCache.Recent(10) });
                return;
            }

            response.StatusCode = 404;
            response.Close();
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static void WriteJson(HttpListenerResponse response, object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}
