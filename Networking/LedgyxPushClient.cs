using System;
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
    // Periodically POSTs the cached snapshot to Ledgyx. Runs off a
    // System.Threading.Timer callback (a thread-pool thread), never the
    // Unity main thread — HTTP latency must never touch the sim tick.
    // BCL-only (HttpWebRequest), matching ApiServer.cs's no-external-HTTP-
    // deps constraint on the inbound side.
    public class LedgyxPushClient
    {
        private readonly AgentSettings settings;
        private System.Threading.Timer timer;
        private bool warnedMissingApiKey;

        public LedgyxPushClient(AgentSettings settings)
        {
            this.settings = settings;
        }

        public void Start()
        {
            var periodMs = settings.PushCadenceSeconds * 1000;
            timer = new System.Threading.Timer(_ => Tick(), null, periodMs, periodMs);
            Debug.Log("[OniAgent] LedgyxPushClient started, cadence=" + settings.PushCadenceSeconds + "s");
        }

        public void Stop()
        {
            timer?.Dispose();
            timer = null;
        }

        // Never let an exception escape the timer callback — that would
        // silently stop all future ticks with no error surfaced anywhere.
        //
        // The start/completed bracket exists to localize a native Mono SEGV
        // reported 2026-07-31 (thread 74) whose last log line before the
        // crash was this client's own "push succeeded" — see task #20. If
        // "tick completed" is missing from the log at crash time, the fault
        // is inside this callback (Push/JSON/HTTP); if it's present, the
        // fault happened after the ThreadPool callback returned, pointing
        // at thread-pool/Mono teardown timing instead of this code.
        private void Tick()
        {
            Debug.Log("[OniAgent] LedgyxPushClient: tick started");
            try
            {
                Push();
            }
            catch (Exception e)
            {
                Debug.LogError("[OniAgent] LedgyxPushClient push failed: " + e);
            }
            finally
            {
                Debug.Log("[OniAgent] LedgyxPushClient: tick completed");
            }
        }

        private void Push()
        {
            if (string.IsNullOrEmpty(settings.ApiKey))
            {
                if (!warnedMissingApiKey)
                {
                    Debug.LogWarning("[OniAgent] LedgyxPushClient: ApiKey not set in settings.json, skipping pushes.");
                    warnedMissingApiKey = true;
                }
                return;
            }

            var duplicants = SnapshotCache.LatestDuplicants;
            var colony = SnapshotCache.LatestColony;
            if (duplicants == null && colony == null)
            {
                return; // nothing collected yet
            }

            var payload = new LedgyxSnapshotPayload
            {
                CapturedAt = System.DateTime.UtcNow.ToString("o"),
                Duplicants = duplicants,
                Colony = TrimForPush(colony),
            };
            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

            var request = (HttpWebRequest)WebRequest.Create(settings.LedgyxEndpoint);
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
                    Debug.Log("[OniAgent] LedgyxPushClient: push succeeded (" + (int)response.StatusCode + ")");
                }
            }
            catch (WebException e)
            {
                var status = (e.Response as HttpWebResponse)?.StatusCode;
                Debug.LogWarning("[OniAgent] LedgyxPushClient: push rejected"
                    + (status != null ? " (" + (int)status + ")" : "") + ": " + e.Message);
            }
        }

        // Ledgyx's gateway has a request body size limit (measured ~51KB —
        // above that it returns 200/success:true but silently drops the
        // request instead of persisting it, so this is NOT a safe margin to
        // approach). A developed, multi-world colony's full ColonySnapshot
        // can run well past it — dominated by localized building/consumer
        // Name strings (~1/3 of the payload on a real save, and almost
        // entirely redundant with PrefabId: a few hundred distinct prefabs
        // repeated across thousands of building instances) and by
        // Power.Consumers, which duplicates buildings already listed in
        // Buildings[] and adds only wattage detail. Trims those for the wire
        // payload only — the cached ColonySnapshot instance itself is
        // untouched, since SnapshotCache is read concurrently by ApiServer's
        // listener thread and must never be mutated in place after
        // publishing. Even trimmed, a large colony (1000+ buildings) can
        // still exceed the limit; current mitigation is a larger limit
        // configured on the Ledgyx side, not further trimming here.
        private static ColonySnapshot TrimForPush(ColonySnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            var trimmed = new ColonySnapshot
            {
                SchemaVersion = source.SchemaVersion,
                Cycle = source.Cycle,
                Research = source.Research,
                Worlds = source.Worlds,
                Rockets = source.Rockets,
                Power = new PowerSnapshot
                {
                    Generators = new List<GeneratorSnapshot>(source.Power.Generators.Count),
                    Batteries = new List<BatterySnapshot>(source.Power.Batteries.Count),
                },
            };

            foreach (var building in source.Buildings)
            {
                trimmed.Buildings.Add(new BuildingSnapshot
                {
                    Id = building.Id,
                    PrefabId = building.PrefabId,
                    PosX = building.PosX,
                    PosY = building.PosY,
                    WorldId = building.WorldId,
                    IsOperational = building.IsOperational,
                    PriorityClass = building.PriorityClass,
                    PriorityValue = building.PriorityValue,
                    StoredItems = building.StoredItems,
                });
            }

            foreach (var generator in source.Power.Generators)
            {
                trimmed.Power.Generators.Add(new GeneratorSnapshot
                {
                    Id = generator.Id,
                    WorldId = generator.WorldId,
                    WattageRating = generator.WattageRating,
                    JoulesAvailable = generator.JoulesAvailable,
                    Capacity = generator.Capacity,
                    IsProducingPower = generator.IsProducingPower,
                });
            }

            foreach (var battery in source.Power.Batteries)
            {
                trimmed.Power.Batteries.Add(new BatterySnapshot
                {
                    Id = battery.Id,
                    WorldId = battery.WorldId,
                    JoulesAvailable = battery.JoulesAvailable,
                    Capacity = battery.Capacity,
                    PercentFull = battery.PercentFull,
                });
            }

            // Power.Consumers deliberately omitted — every consumer is
            // already a building in Buildings[]; dropping it loses only
            // per-consumer wattage, not classification.
            return trimmed;
        }
    }
}
