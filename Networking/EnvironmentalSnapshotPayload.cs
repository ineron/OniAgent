using OniAgent.Snapshot;

namespace OniAgent.Networking
{
    // What EnvironmentalPushClient POSTs: the whole cached
    // EnvironmentalSnapshotResponse (Sectors[] included) as a single row, per
    // the operational tier's pattern (see LedgyxPushClient) rather than the
    // critical tier's per-row-explodes pattern — see task #15's node body for
    // why exploding per-sector would multiply AI_AGENT trigger runs per push.
    // CapturedAt lives at this wrapper level since EnvironmentalSnapshotResponse
    // itself has no timestamp field (unlike CriticalEvent, which carries one
    // per event).
    public class EnvironmentalSnapshotPayload
    {
        public string CapturedAt;
        public EnvironmentalSnapshotResponse Environmental;
    }
}
