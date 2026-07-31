using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OniAgent.Commands
{
    // Written by CommandTicker (main thread) after each batch executes,
    // read by ApiServer (listener thread) so the caller can poll for the
    // outcome of a batch it posted — POST /api/command itself only
    // enqueues and returns immediately, it never blocks on execution.
    public static class CommandResultCache
    {
        private const int MaxRetained = 50;

        private static readonly ConcurrentDictionary<string, CommandBatchResult> byId =
            new ConcurrentDictionary<string, CommandBatchResult>();

        // Insertion order, oldest first — used only to evict once byId grows
        // past MaxRetained, so this stays a dev-tool-sized cache instead of
        // growing for the life of the game session.
        private static readonly ConcurrentQueue<string> order = new ConcurrentQueue<string>();

        public static void Store(CommandBatchResult result)
        {
            byId[result.BatchId] = result;
            order.Enqueue(result.BatchId);
            while (order.Count > MaxRetained && order.TryDequeue(out var oldestId))
            {
                byId.TryRemove(oldestId, out _);
            }
        }

        public static bool TryGet(string batchId, out CommandBatchResult result)
        {
            return byId.TryGetValue(batchId, out result);
        }

        public static List<CommandBatchResult> Recent(int count)
        {
            return order.Reverse().Take(count)
                .Select(id => byId.TryGetValue(id, out var r) ? r : null)
                .Where(r => r != null)
                .ToList();
        }
    }
}
