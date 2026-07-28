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
    }

    public class CriticalEventResponse
    {
        public int SchemaVersion;
        public List<CriticalEvent> Events = new List<CriticalEvent>();
    }
}
