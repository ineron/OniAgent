using UnityEngine;

namespace OniAgent.Snapshot
{
    // Runs on the Unity main thread. Collects on a real-time interval, not
    // every frame, so this never becomes a per-frame cost on the sim.
    public class SnapshotTicker : MonoBehaviour
    {
        private const float DefaultCadenceSeconds = 60f;

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
        }
    }
}
