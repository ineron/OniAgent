using System.Collections.Generic;
using UnityEngine;

namespace OniAgent.Snapshot
{
    // Runs on the Unity main thread. Collects on a real-time interval, not
    // every frame, so this never becomes a per-frame cost on the sim.
    public class SnapshotTicker : MonoBehaviour
    {
        private const float DefaultCadenceSeconds = 60f;

        // Critical events are meant to be noticed "immediately" per the
        // 3-tier design, but this ticker's cadence (from
        // OperationalCadenceSeconds, min 5s) is the fastest hook this mod
        // has today — good enough reaction time for a game tick, and
        // reusing it avoids a second MonoBehaviour/timer just for this.
        // Actually pushing each event to Ledgyx without waiting for
        // PushCadenceSeconds is a separate follow-up (see
        // CriticalEventCollector's header comment).
        private const int MaxRecentCriticalEvents = 50;

        private float cadenceSeconds = DefaultCadenceSeconds;
        private float secondsSinceLastTick;

        // Called once right after AddComponent, before the first LateUpdate.
        public void Configure(int operationalCadenceSeconds)
        {
            cadenceSeconds = operationalCadenceSeconds;
        }

        private void LateUpdate()
        {
            secondsSinceLastTick += Time.deltaTime;
            if (secondsSinceLastTick < cadenceSeconds)
            {
                return;
            }
            secondsSinceLastTick = 0f;

            SnapshotCache.LatestDuplicants = SnapshotCollector.CollectDuplicants();
            SnapshotCache.LatestColony = ColonySnapshotCollector.Collect();

            var newEvents = CriticalEventCollector.Collect();
            if (newEvents.Count > 0)
            {
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
            }
        }
    }
}
