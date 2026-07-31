using UnityEngine;

namespace OniAgent.Commands
{
    // Runs on the Unity main thread, same pattern as SnapshotTicker. Drains
    // whatever ApiServer enqueued since the last frame and applies it to
    // live game state — never touch Grid/BuildingDef from the listener
    // thread directly.
    public class CommandTicker : MonoBehaviour
    {
        private void LateUpdate()
        {
            while (CommandQueue.TryDequeue(out var batch))
            {
                ExecuteBatch(batch);
            }
        }

        private void ExecuteBatch(PendingBatch batch)
        {
            var result = new CommandBatchResult
            {
                BatchId = batch.BatchId,
                ExecutedAtUtc = System.DateTime.UtcNow.ToString("o"),
            };

            if (!CommandExecutor.TryGetOriginCell(out int originCell, out string originError))
            {
                foreach (var item in batch.Request.Commands)
                {
                    result.Results.Add(CommandItemResult.Fail(item, originError));
                }
                CommandResultCache.Store(result);
                return;
            }

            foreach (var item in batch.Request.Commands)
            {
                result.Results.Add(CommandExecutor.Execute(item, originCell));
            }

            CommandResultCache.Store(result);
        }
    }
}
