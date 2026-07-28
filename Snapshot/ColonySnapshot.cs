using System.Collections.Generic;

namespace OniAgent.Snapshot
{
    // Operational tier: buildings, storage, power grid, priorities, research.
    // Changes on the order of minutes — meant to be pushed to Ledgyx on a cron
    // interval (task #7), not per-change. See the critical/event tier
    // (duplicant health, outages) and environmental tier (tile temperature/
    // pressure, sector-aggregated) for the other two cadences in this design.
    public class ColonySnapshot
    {
        public int SchemaVersion;
        public List<BuildingSnapshot> Buildings = new List<BuildingSnapshot>();
        public PowerSnapshot Power = new PowerSnapshot();
        public ResearchSnapshot Research = new ResearchSnapshot();

        // Spaced Out only; a vanilla save reports one WorldSnapshot and no rockets.
        public List<WorldSnapshot> Worlds = new List<WorldSnapshot>();
        public List<RocketSnapshot> Rockets = new List<RocketSnapshot>();
    }

    public class BuildingSnapshot
    {
        public string Id;
        public string PrefabId;
        public string Name;
        public float PosX;
        public float PosY;

        // Which WorldContainer (planet/asteroid) this building sits on —
        // see WorldLookup. -1 if off-grid.
        public int WorldId;

        // Null when the building has no Operational component (e.g. plain tiles).
        public bool? IsOperational;

        // Null when the building has no Prioritizable component.
        public string PriorityClass;
        public int? PriorityValue;

        public List<StoredItemSnapshot> StoredItems = new List<StoredItemSnapshot>();
    }

    public class StoredItemSnapshot
    {
        public string ElementId;
        public float Mass;
    }

    public class PowerSnapshot
    {
        public List<GeneratorSnapshot> Generators = new List<GeneratorSnapshot>();
        public List<BatterySnapshot> Batteries = new List<BatterySnapshot>();
        public List<ConsumerSnapshot> Consumers = new List<ConsumerSnapshot>();
    }

    public class GeneratorSnapshot
    {
        public string Id;
        public string Name;
        public int WorldId;
        public float WattageRating;
        public float JoulesAvailable;
        public float Capacity;
        public bool IsProducingPower;
    }

    public class BatterySnapshot
    {
        public string Id;
        public string Name;
        public int WorldId;
        public float JoulesAvailable;
        public float Capacity;
        public float PercentFull;
    }

    public class ConsumerSnapshot
    {
        public string Id;
        public string Name;
        public int WorldId;
        public float WattsUsed;
        public float WattsNeededWhenActive;
        public bool IsPowered;
    }

    public class ResearchSnapshot
    {
        public string ActiveTechId;
        public float ActiveTechPercentComplete;
        public List<string> QueuedTechIds = new List<string>();
        public Dictionary<string, float> ResearchPointsByType = new Dictionary<string, float>();
    }
}
