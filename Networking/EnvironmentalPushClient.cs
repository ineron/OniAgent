using System;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OniAgent.Settings;
using OniAgent.Snapshot;
using UnityEngine;

namespace OniAgent.Networking
{
    // Environmental-tier counterpart to LedgyxPushClient: same persistent-
    // thread cadence loop, whole-snapshot-as-one-row shape (see
    // EnvironmentalSnapshotPayload), just its own endpoint/cadence since
    // this is a separate Ledgyx table pushed far less often. Deliberately
    // NOT the critical tier's queue+worker pattern (CriticalEventPushClient)
    // — that one exists because a missed critical event is genuinely lost,
    // whereas a missed environmental push is superseded by the next
    // periodic sample anyway.
    //
    // Was originally a System.Threading.Timer (ThreadPool callback) until a
    // native Mono SEGV reported 2026-07-31 (thread 74, task #20) implicated
    // ThreadPool thread teardown — see LedgyxPushClient's header comment
    // for the full reasoning; this client used the identical Timer pattern
    // and is equally implicated, so it moved to the same fix.
    public class EnvironmentalPushClient
    {
        private readonly AgentSettings settings;
        private Thread worker;
        private readonly ManualResetEventSlim stopSignal = new ManualResetEventSlim(false);
        private bool warnedMissingConfig;

        public EnvironmentalPushClient(AgentSettings settings)
        {
            this.settings = settings;
        }

        public void Start()
        {
            worker = new Thread(Run) { IsBackground = true, Name = "OniAgentEnvironmentalPush" };
            worker.Start();
            Debug.Log("[OniAgent] EnvironmentalPushClient started, cadence=" + settings.EnvironmentalPushCadenceSeconds + "s");
        }

        public void Stop()
        {
            stopSignal.Set();
            worker?.Join(5000);
            worker = null;
        }

        private void Run()
        {
            var periodMs = settings.EnvironmentalPushCadenceSeconds * 1000;
            while (!stopSignal.Wait(periodMs))
            {
                Tick();
            }
        }

        // Never let an exception escape the loop — that would silently
        // stop all future ticks with no error surfaced anywhere.
        //
        // Start/completed bracket kept for the same reason as
        // LedgyxPushClient.Tick (task #20) — still useful evidence if a
        // crash recurs even after moving off Timer.
        private void Tick()
        {
            Debug.Log("[OniAgent] EnvironmentalPushClient: tick started");
            try
            {
                Push();
            }
            catch (ThreadAbortException)
            {
                Thread.ResetAbort();
            }
            catch (Exception e)
            {
                Debug.LogError("[OniAgent] EnvironmentalPushClient push failed: " + e);
            }
            finally
            {
                Debug.Log("[OniAgent] EnvironmentalPushClient: tick completed");
            }
        }

        private void Push()
        {
            if (string.IsNullOrEmpty(settings.ApiKey) || string.IsNullOrEmpty(settings.EnvironmentalEndpoint))
            {
                if (!warnedMissingConfig)
                {
                    Debug.LogWarning("[OniAgent] EnvironmentalPushClient: ApiKey or EnvironmentalEndpoint not set in settings.json, skipping pushes.");
                    warnedMissingConfig = true;
                }
                return;
            }

            var environmental = SnapshotCache.LatestEnvironmental;
            if (environmental == null)
            {
                return; // nothing collected yet
            }

            var payload = new EnvironmentalSnapshotPayload
            {
                CapturedAt = System.DateTime.UtcNow.ToString("o"),
                Environmental = environmental,
            };
            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

            var request = (HttpWebRequest)WebRequest.Create(settings.EnvironmentalEndpoint);
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
                    Debug.Log("[OniAgent] EnvironmentalPushClient: push succeeded (" + (int)response.StatusCode + ")");
                }
            }
            catch (WebException e)
            {
                var status = (e.Response as HttpWebResponse)?.StatusCode;
                Debug.LogWarning("[OniAgent] EnvironmentalPushClient: push rejected"
                    + (status != null ? " (" + (int)status + ")" : "") + ": " + e.Message);
            }
        }
    }
}
