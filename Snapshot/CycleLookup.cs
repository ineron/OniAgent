namespace OniAgent.Snapshot
{
    // Shared by every collector that tags a snapshot with the in-game cycle
    // number, so a consumer (agent or dashboard) can tell a save reload back
    // to an earlier cycle apart from normal forward progress — CapturedAt
    // (wall-clock) alone can't distinguish the two.
    public static class CycleLookup
    {
        public static int CurrentCycle()
        {
            return GameClock.Instance != null ? GameClock.Instance.GetCycle() : -1;
        }
    }
}
