using System.Collections.Concurrent;

namespace OniAgent.Commands
{
    public class PendingBatch
    {
        public string BatchId;
        public CommandBatchRequest Request;
    }

    // Enqueue happens on the HttpListener's background thread (ApiServer);
    // dequeue+execute happens on the Unity main thread (CommandTicker).
    // ConcurrentQueue is the only cross-thread contact point, same role
    // SnapshotCache plays for the read direction.
    public static class CommandQueue
    {
        private static readonly ConcurrentQueue<PendingBatch> queue = new ConcurrentQueue<PendingBatch>();

        public static void Enqueue(PendingBatch batch)
        {
            queue.Enqueue(batch);
        }

        public static bool TryDequeue(out PendingBatch batch)
        {
            return queue.TryDequeue(out batch);
        }
    }
}
