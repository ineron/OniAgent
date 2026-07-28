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
            var cell = Grid.PosToCell(position);
            if (!Grid.IsValidCell(cell))
            {
                return -1;
            }

            var worldIdx = Grid.WorldIdx[cell];
            return worldIdx == byte.MaxValue ? -1 : worldIdx;
        }
    }
}
