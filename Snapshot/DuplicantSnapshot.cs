using System.Collections.Generic;

namespace OniAgent.Snapshot
{
    public class DuplicantSnapshot
    {
        public string Id;
        public string Name;
        public float Health;
        public float Stress;
        public float PosX;
        public float PosY;
        public string CurrentChore;
        public List<string> MasteredSkillIds = new List<string>();
        public List<string> TraitIds = new List<string>();
        public List<EffectSnapshot> Effects = new List<EffectSnapshot>();
    }

    public class EffectSnapshot
    {
        public string Id;
        public string Name;
        public float TimeRemaining;
    }

    public class DuplicantSnapshotResponse
    {
        public int SchemaVersion;
        public List<DuplicantSnapshot> Duplicants = new List<DuplicantSnapshot>();
    }
}
