namespace OniAgent.Snapshot
{
    // Written only by SnapshotTicker (Unity main thread), read only by
    // ApiServer's HTTP handler (listener thread). Reference assignment is
    // atomic in .NET, so this needs no lock as long as nothing ever mutates
    // a response object in place after publishing it here.
    public static class SnapshotCache
    {
        public static volatile DuplicantSnapshotResponse LatestDuplicants;
    }
}
