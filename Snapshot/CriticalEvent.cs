using System.Collections.Generic;

namespace OniAgent.Snapshot
{
    public class CriticalEvent
    {
        public string EventType;
        public string EntityId;
        public string EntityName;
        public int WorldId;
        public string Detail;
        public string CapturedAt;

        // In-game cycle number (GameClock.GetCycle()) at capture time — per
        // event, not on the response wrapper, since a batch can straddle a
        // cycle boundary. See ColonySnapshot.Cycle.
        public int Cycle;
    }

    public class CriticalEventResponse
    {
        public int SchemaVersion;
        public List<CriticalEvent> Events = new List<CriticalEvent>();
    }
}
