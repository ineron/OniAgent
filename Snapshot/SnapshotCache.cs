using OniAgent.Networking;

namespace OniAgent.Snapshot
{
    // Written only by SnapshotTicker (Unity main thread), read only by
    // ApiServer's HTTP handler (listener thread). Reference assignment is
    // atomic in .NET, so this needs no lock as long as nothing ever mutates
    // a response object in place after publishing it here.
    public static class SnapshotCache
    {
        public static volatile DuplicantSnapshotResponse LatestDuplicants;
        public static volatile ColonySnapshot LatestColony;

        // Bounded rolling window of the most recent critical/event-tier
        // events (see CriticalEventCollector), not just "since the last
        // tick" — a new CriticalEventResponse replaces this reference each
        // tick that produces new events (never mutated in place), same
        // pattern as the other two fields.
        public static volatile CriticalEventResponse RecentCriticalEvents;

        public static volatile EnvironmentalSnapshotResponse LatestEnvironmental;

        // Written by LedgyxSseClient's own background thread (not the Unity
        // main thread, unlike the fields above), read by ApiServer's
        // listener thread. Same no-lock/never-mutate-in-place posture.
        public static volatile AgentRunResult LatestAgentRunResult;
    }
}
