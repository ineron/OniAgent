using UnityEngine;

namespace OniAgent.Commands
{
    // Applies a single CommandItem to live game state. Must only ever be
    // called from the Unity main thread (CommandTicker.LateUpdate) — Grid,
    // Assets and BuildingDef.Build are not thread-safe.
    public static class CommandExecutor
    {
        // Origin for every relative (x,y) in a batch is the Duplicant
        // Printing Pod (Telepad), since that's the one landmark guaranteed
        // to exist and be visible to whoever is writing the command batch
        // without them having to read absolute Grid coordinates off screen.
        public static bool TryGetOriginCell(out int originCell, out string error)
        {
            var telepad = Object.FindObjectOfType<Telepad>();
            if (telepad == null)
            {
                originCell = Grid.InvalidCell;
                error = "Telepad (Duplicant Printing Pod) not found in the world";
                return false;
            }

            originCell = Grid.PosToCell(telepad.gameObject);
            error = null;
            return true;
        }

        public static CommandItemResult Execute(CommandItem item, int originCell)
        {
            try
            {
                switch (item.Type)
                {
                    case "dig_rect":
                        return DigRect(item, originCell);
                    case "build":
                        return Build(item, originCell);
                    default:
                        return CommandItemResult.Fail(item, "Unknown command type: " + item.Type);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[OniAgent] CommandExecutor error: " + e);
                return CommandItemResult.Fail(item, "Exception: " + e.Message);
            }
        }

        // Queues a dig order (same call the in-game Dig tool makes per
        // cell) over the inclusive rectangle [X1,X2] x [Y1,Y2], offsets
        // relative to origin. Duplicants then dig it over time via the
        // normal Dig chore — this does not carve the tiles instantly.
        private static CommandItemResult DigRect(CommandItem item, int originCell)
        {
            Grid.CellToXY(originCell, out int ox, out int oy);
            int queued = 0;
            int skipped = 0;

            int xLo = System.Math.Min(item.X1, item.X2);
            int xHi = System.Math.Max(item.X1, item.X2);
            int yLo = System.Math.Min(item.Y1, item.Y2);
            int yHi = System.Math.Max(item.Y1, item.Y2);

            for (int x = xLo; x <= xHi; x++)
            {
                for (int y = yLo; y <= yHi; y++)
                {
                    int cell = Grid.XYToCell(ox + x, oy + y);
                    if (!Grid.IsValidCell(cell))
                    {
                        skipped++;
                        continue;
                    }

                    // PlaceDig itself checks Grid.Solid/Foundation/already-marked
                    // and returns null when the cell isn't diggable — safe to
                    // call unconditionally over the whole rectangle.
                    var placed = DigTool.PlaceDig(cell);
                    if (placed != null)
                    {
                        queued++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }

            return CommandItemResult.Success(item, $"dig_rect: queued {queued} cell(s), skipped {skipped} (already clear/marked/invalid)");
        }

        // Places a building instantly-complete at (X,Y) relative to origin
        // (bottom-left cell of its footprint, per BuildingDef.Build), using
        // its default construction materials. This bypasses the normal
        // ghost + hauler-delivers-materials + dupe-constructs pipeline —
        // intentional for a first pipeline test where we want to see the
        // building appear immediately rather than debug the construction
        // chore chain too. Building.Build reuses the exact same call the
        // debug/cheat "instant build" menu option makes.
        private static CommandItemResult Build(CommandItem item, int originCell)
        {
            var def = global::Assets.GetBuildingDef(item.Building);
            if (def == null)
            {
                return CommandItemResult.Fail(item, "Unknown building id: " + item.Building);
            }

            Grid.CellToXY(originCell, out int ox, out int oy);
            int cell = Grid.XYToCell(ox + item.X, oy + item.Y);
            if (!Grid.IsValidCell(cell))
            {
                return CommandItemResult.Fail(item, "Invalid cell for build at offset (" + item.X + "," + item.Y + ")");
            }

            var orientation = Orientation.Neutral;
            if (!BuildingDef.CheckFoundation(cell, orientation, def.BuildLocationRule, def.WidthInCells, def.HeightInCells))
            {
                return CommandItemResult.Fail(item, "CheckFoundation failed for " + item.Building + " at offset (" + item.X + "," + item.Y + ") — likely missing floor/foundation or space is occupied");
            }

            var elements = def.DefaultElements();
            var go = def.Build(cell, orientation, null, elements, 293.15f);
            return go != null
                ? CommandItemResult.Success(item, "built " + item.Building + " at offset (" + item.X + "," + item.Y + ")")
                : CommandItemResult.Fail(item, "Build() returned null for " + item.Building);
        }
    }
}
