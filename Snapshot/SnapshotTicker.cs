using System.Collections.Generic;
using OniAgent.Networking;
using UnityEngine;

namespace OniAgent.Snapshot
{
    // Runs on the Unity main thread. Collects on a real-time interval, not
    // every frame, so this never becomes a per-frame cost on the sim.
    public class SnapshotTicker : MonoBehaviour
    {
        private const float DefaultOperationalCadenceSeconds = 60f;
        private const float DefaultCriticalCadenceSeconds = 2f;

        // Two independent cadences, not one shared timer: the operational
        // snapshot (colony/duplicants) is fine on a slow, infrequent poll,
        // but critical-tier dangers — oxygen depletion above all, which can
        // take a duplicant from fine to suffocating within a single
        // operational tick — need a much tighter loop. Since
        // CriticalEventPushClient already pushes each new event immediately
        // on detection (no cadence of its own), criticalCadenceSeconds is
        // the actual end-to-end reaction-time knob for this tier.
        private const int MaxRecentCriticalEvents = 50;

        private float operationalCadenceSeconds = DefaultOperationalCadenceSeconds;
        private float criticalCadenceSeconds = DefaultCriticalCadenceSeconds;
        private float secondsSinceLastOperationalTick;
        private float secondsSinceLastCriticalTick;
        private CriticalEventPushClient criticalEventPushClient;

        // Called once right after AddComponent, before the first LateUpdate.
        public void Configure(int operationalCadenceSeconds, int criticalCadenceSeconds, CriticalEventPushClient criticalEventPushClient)
        {
            this.operationalCadenceSeconds = operationalCadenceSeconds;
            this.criticalCadenceSeconds = criticalCadenceSeconds;
            this.criticalEventPushClient = criticalEventPushClient;
        }

        private void LateUpdate()
        {
            secondsSinceLastOperationalTick += Time.deltaTime;
            if (secondsSinceLastOperationalTick >= operationalCadenceSeconds)
            {
                secondsSinceLastOperationalTick = 0f;
                SnapshotCache.LatestDuplicants = SnapshotCollector.CollectDuplicants();
                SnapshotCache.LatestColony = ColonySnapshotCollector.Collect();
            }

            secondsSinceLastCriticalTick += Time.deltaTime;
            if (secondsSinceLastCriticalTick >= criticalCadenceSeconds)
            {
                secondsSinceLastCriticalTick = 0f;
                TickCritical();
            }
        }

        private void TickCritical()
        {
            var newEvents = CriticalEventCollector.Collect();
            if (newEvents.Count == 0)
            {
                return;
            }

            var combined = new List<CriticalEvent>(SnapshotCache.RecentCriticalEvents?.Events
                ?? new List<CriticalEvent>());
            combined.AddRange(newEvents);
            if (combined.Count > MaxRecentCriticalEvents)
            {
                combined.RemoveRange(0, combined.Count - MaxRecentCriticalEvents);
            }
            SnapshotCache.RecentCriticalEvents = new CriticalEventResponse
            {
                SchemaVersion = CriticalEventCollector.SchemaVersion,
                Events = combined,
            };

            criticalEventPushClient?.Enqueue(newEvents);
        }
    }
}
