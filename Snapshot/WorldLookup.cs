using UnityEngine;

namespace OniAgent.Snapshot
{
    // Shared by every collector that tags an entity with its Spaced Out
    // world/asteroid. Grid.WorldIdx is a byte array indexed by cell;
    // byte.MaxValue means "no world claims this cell" (e.g. deep space),
    // mapped to -1 since that's not a valid WorldContainer.id.
    public static class WorldLookup
    {
        public static int WorldIdAt(Vector3 position)
        {
            return WorldIdAtCell(Grid.PosToCell(position));
        }

        // Cell-index overload for callers that already have the cell (e.g.
        // EnvironmentalSnapshotCollector, which walks every cell directly) —
        // avoids a redundant position round-trip through Grid.PosToCell.
        public static int WorldIdAtCell(int cell)
        {
            if (!Grid.IsValidCell(cell))
            {
                return -1;
            }

            var worldIdx = Grid.WorldIdx[cell];
            return worldIdx == byte.MaxValue ? -1 : worldIdx;
        }
    }
}
