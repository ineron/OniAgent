using UnityEngine;

namespace OniAgent.Snapshot
{
    // Runs on the Unity main thread. Collects on a tick, not every frame,
    // so this never becomes a per-frame cost on the sim.
    public class SnapshotTicker : MonoBehaviour
    {
        private const int TickIntervalFrames = 150;

        private int framesSinceLastTick;

        private void LateUpdate()
        {
            framesSinceLastTick++;
            if (framesSinceLastTick < TickIntervalFrames)
            {
                return;
            }
            framesSinceLastTick = 0;

            SnapshotCache.LatestDuplicants = SnapshotCollector.CollectDuplicants();
        }
    }
}
