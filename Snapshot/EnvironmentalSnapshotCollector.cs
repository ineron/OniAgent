using System.Collections.Generic;

namespace OniAgent.Snapshot
{
    // Must be called from Unity's main thread — reads Grid arrays directly,
    // same constraint as the other collectors. See EnvironmentalSnapshot.cs
    // for why this aggregates into sectors instead of reporting per-cell.
    //
    // Confirmed via decompile (Grid.cs): Grid.Temperature/Grid.Mass/
    // Grid.Element are struct indexers over the sim's flat per-cell arrays
    // (Grid.Temperature[cell] -> float Kelvin, Grid.Mass[cell] -> float kg,
    // Grid.Element[cell] -> Element, whose .id is the same SimHashes enum
    // PrimaryElement.ElementID exposes elsewhere in this mod).
    public static class EnvironmentalSnapshotCollector
    {
        public const int SchemaVersion = 1;

        // Default sector edge length in cells. A classic single-asteroid map
        // is roughly 256x384 cells; at 32 cells/side that's ~8x12 = 96
        // sectors, comfortably inside Ledgyx's ~51KB single-row payload
        // budget (see LedgyxPushClient) even as one JSON array. Configurable
        // via AgentSettings.EnvironmentalSectorSizeCells for larger/Spaced
        // Out maps.
        public const int DefaultSectorSizeCells = 32;

        private class SectorAccumulator
        {
            public int CellCount;
            public float MassSum;

            // Only cells with actual substance (mass > 0) count toward the
            // temperature average — a true-vacuum cell has no thermal mass
            // and Grid.Temperature reports a literal 0K placeholder for it
            // (confirmed against a real save: sectors straddling open space
            // showed AvgTemperatureKelvin pulled toward 0 before this split
            // existed), which isn't a meaningful "temperature" to blend in.
            public int SubstantialCellCount;
            public double SubstantialTemperatureSum;

            // Worlds rarely differ within one sector — only possible at the
            // boundary between two adjacent worlds' cell ranges — so this
            // stays a handful of entries at most.
            public Dictionary<int, int> WorldCellCounts = new Dictionary<int, int>();
            public Dictionary<SimHashes, float> ElementMassTotals = new Dictionary<SimHashes, float>();
        }

        public static EnvironmentalSnapshotResponse Collect(int sectorSizeCells)
        {
            if (sectorSizeCells < 1)
            {
                sectorSizeCells = DefaultSectorSizeCells;
            }

            var response = new EnvironmentalSnapshotResponse
            {
                SchemaVersion = SchemaVersion,
                SectorSizeCells = sectorSizeCells,
            };

            var accumulators = new Dictionary<(int SectorX, int SectorY), SectorAccumulator>();

            for (var cell = 0; cell < Grid.CellCount; cell++)
            {
                if (!Grid.IsValidCell(cell))
                {
                    continue;
                }

                // Cells with no owning world are the unclaimed space between
                // separate asteroids' bounding boxes in a Spaced Out cluster
                // (or simply unused padding on a vanilla map) — aggregating
                // them would produce sectors full of meaningless default sim
                // values, so they're dropped rather than reported as WorldId
                // -1. See WorldLookup.
                var worldId = WorldLookup.WorldIdAtCell(cell);
                if (worldId < 0)
                {
                    continue;
                }

                // Indestructible border-wall material surrounding every
                // asteroid — confirmed via decompile (Element.cs): State is
                // a bit-flagged enum where Vacuum/Gas/Liquid/Solid occupy a
                // 2-bit base value (StateMask=3) and Unbreakable=4 is a
                // separate flag OR'd on top, so IsSolid still reports true
                // for these. A real save's capture showed this dominating
                // ~48% of sectors (per-cell mass in the millions of kg for
                // a full 1024-cell sector) because border-wall cells sit at
                // the edge of every asteroid, not just the map's outer
                // edge — checking the Unbreakable flag directly (rather
                // than hardcoding a specific element like Unobtanium or
                // Neutronium, either of which a given save might use, and
                // Unobtanium is also a legitimate minable resource — see
                // CanDigUnobtanium/oreTags — so name-matching it would
                // wrongly exclude real ore too) is what actually
                // identifies "not part of the playable environment" here.
                var element = Grid.Element[cell];
                if (element != null && (element.state & Element.State.Unbreakable) != 0)
                {
                    continue;
                }

                Grid.CellToXY(cell, out var x, out var y);
                var key = (x / sectorSizeCells, y / sectorSizeCells);

                if (!accumulators.TryGetValue(key, out var acc))
                {
                    acc = new SectorAccumulator();
                    accumulators[key] = acc;
                }

                acc.CellCount++;
                var mass = Grid.Mass[cell];
                acc.MassSum += mass;
                if (mass > 0f)
                {
                    acc.SubstantialCellCount++;
                    acc.SubstantialTemperatureSum += Grid.Temperature[cell];
                }

                acc.WorldCellCounts.TryGetValue(worldId, out var worldCellCount);
                acc.WorldCellCounts[worldId] = worldCellCount + 1;

                if (element != null)
                {
                    acc.ElementMassTotals.TryGetValue(element.id, out var elementMass);
                    acc.ElementMassTotals[element.id] = elementMass + mass;
                }
            }

            foreach (var kv in accumulators)
            {
                var acc = kv.Value;
                response.Sectors.Add(new SectorSnapshot
                {
                    SectorX = kv.Key.SectorX,
                    SectorY = kv.Key.SectorY,
                    WorldId = DominantWorldId(acc.WorldCellCounts),
                    CellCount = acc.CellCount,
                    AvgTemperatureKelvin = acc.SubstantialCellCount > 0
                        ? (float)(acc.SubstantialTemperatureSum / acc.SubstantialCellCount)
                        : 0f,
                    TotalMassKg = acc.MassSum,
                    DominantElementId = DominantElementId(acc.ElementMassTotals),
                });
            }

            return response;
        }

        private static int DominantWorldId(Dictionary<int, int> worldCellCounts)
        {
            var bestWorldId = -1;
            var bestCount = -1;
            foreach (var kv in worldCellCounts)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    bestWorldId = kv.Key;
                }
            }
            return bestWorldId;
        }

        private static string DominantElementId(Dictionary<SimHashes, float> elementMassTotals)
        {
            SimHashes? bestElement = null;
            var bestMass = -1f;
            foreach (var kv in elementMassTotals)
            {
                if (kv.Value > bestMass)
                {
                    bestMass = kv.Value;
                    bestElement = kv.Key;
                }
            }
            return bestElement?.ToString();
        }
    }
}
