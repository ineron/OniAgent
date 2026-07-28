using OniAgent.Snapshot;

namespace OniAgent.Networking
{
    // What LedgyxPushClient POSTs. Wraps the two already-versioned snapshot
    // objects as-is (each keeps its own SchemaVersion) plus a capture
    // timestamp, since the old oni_colony_snapshot/oni_duplicant_state
    // Postgres DDL is dead — Ledgyx's gateway passes this body through
    // opaquely into a Dictionary-backed table, so there's no column schema
    // to match here.
    public class LedgyxSnapshotPayload
    {
        public int SchemaVersion = 1;
        public string CapturedAt;
        public DuplicantSnapshotResponse Duplicants;
        public ColonySnapshot Colony;
    }
}
