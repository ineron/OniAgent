namespace OniAgent.Snapshot
{
    // One entry per WorldContainer — a planet/asteroid in the cluster, or a
    // rocket's interior once it has one (IsModuleInterior). Spaced Out only;
    // a vanilla save reports exactly one world here.
    public class WorldSnapshot
    {
        public int Id;
        public string Name;
        public string WorldType;
        public bool IsModuleInterior;
        public int ParentWorldId;
        public bool IsStartWorld;
    }
}
