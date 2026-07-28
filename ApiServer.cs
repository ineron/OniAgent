using System;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
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
                    ?? new DuplicantSnapshotResponse { SchemaVersion = SnapshotCollector.SchemaVersion };
                WriteJson(response, snapshot);
                return;
            }

            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/api/snapshot/colony")
            {
                var snapshot = SnapshotCache.LatestColony
                    ?? new ColonySnapshot { SchemaVersion = ColonySnapshotCollector.SchemaVersion };
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

            response.StatusCode = 404;
            response.Close();
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
