using System.Collections.Generic;

namespace OniAgent.Snapshot
{
    // Environmental tier: tile temperature/mass("pressure")/element, aggregated
    // into fixed-size sector chunks rather than reported per-cell. A real map
    // has tens to hundreds of thousands of cells, and ONI's continuous
    // heat/gas diffusion means nearly every cell "changes" every tick, so
    // neither per-cell snapshotting (like the operational tier's per-building
    // rows) nor change-detection (like the critical tier) makes sense here.
    // Sampled periodically on its own cadence (EnvironmentalCadenceSeconds),
    // not change-triggered. See ColonySnapshot (operational tier) and
    // CriticalEvent (critical/event tier) for the other two cadences in this
    // design.
    public class EnvironmentalSnapshotResponse
    {
        public int SchemaVersion;

        // Sector chunk edge length in cells, so a consumer can turn
        // SectorX/SectorY back into a cell-space bounding box:
        // [SectorX*SectorSizeCells, (SectorX+1)*SectorSizeCells) and same for Y.
        public int SectorSizeCells;

        public List<SectorSnapshot> Sectors = new List<SectorSnapshot>();
    }

    public class SectorSnapshot
    {
        public int SectorX;
        public int SectorY;

        // Which WorldContainer (planet/asteroid) most of this sector's cells
        // belong to. See WorldLookup. Sectors made up entirely of cells with
        // no owning world (the unclaimed space between Spaced Out asteroid
        // bounding boxes) are dropped by the collector rather than reported
        // with WorldId -1 — see EnvironmentalSnapshotCollector.
        public int WorldId;

        // Real, in-world, non-border-wall cells only — see
        // EnvironmentalSnapshotCollector for what's excluded and why.
        public int CellCount;

        // Averaged only over cells with actual substance (mass > 0) — a
        // true-vacuum cell has no thermal mass and reports a meaningless 0K
        // placeholder, which would otherwise drag down any sector that's
        // partly open space. 0 if the whole sector is vacuum.
        public float AvgTemperatureKelvin;

        // ONI has no separate pressure value — a cell's gas/liquid mass (kg)
        // is what the game itself treats as pressure (shown as "g" on the
        // tile info screen), so this doubles as both. Excludes indestructible
        // border-wall cells (see EnvironmentalSnapshotCollector) — their mass
        // dwarfs any real terrain/resource in the sector and isn't something
        // a duplicant can ever interact with.
        public float TotalMassKg;

        // Element with the highest total mass in the sector, not the most
        // common cell — a sector that's mostly vacuum with one dense liquid
        // pool should report the liquid, not Vacuum. Border-wall cells are
        // excluded before this is computed (see EnvironmentalSnapshotCollector),
        // otherwise the indestructible boundary material would dominate
        // nearly every sector touching an asteroid's edge.
        public string DominantElementId;
    }
}
