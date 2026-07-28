using System.Collections.Generic;

namespace OniAgent.Snapshot
{
    // Must be called from Unity's main thread — reads live game components.
    // See SnapshotTicker, which is the only caller.
    public static class RocketSnapshotCollector
    {
        public static void Collect(List<RocketSnapshot> rockets)
        {
            foreach (var craft in Components.Clustercrafts.Items)
            {
                rockets.Add(CollectOne(craft));
            }
        }

        private static RocketSnapshot CollectOne(Clustercraft craft)
        {
            var interiorWorld = craft.ModuleInterface.GetInteriorWorld();

            var snapshot = new RocketSnapshot
            {
                Id = craft.GetInstanceID().ToString(),
                Name = craft.Name,
                Status = craft.Status.ToString(),
                LocationQ = craft.Location.Q,
                LocationR = craft.Location.R,
                DestinationQ = craft.Destination.Q,
                DestinationR = craft.Destination.R,
                InteriorWorldId = interiorWorld != null ? (int?)interiorWorld.id : null,
                Speed = craft.Speed,
                EnginePower = craft.EnginePower,
                HasFuelToMove = craft.HasResourcesToMove(),
            };

            CollectPassengers(craft, snapshot.PassengerDuplicantIds);

            foreach (var cargoBay in craft.GetAllCargoBays())
            {
                snapshot.CargoBays.Add(new RocketCargoBaySnapshot
                {
                    CargoType = cargoBay.storageType.ToString(),
                    MaxCapacity = cargoBay.MaxCapacity,
                    RemainingCapacity = cargoBay.RemainingCapacity,
                });
            }

            return snapshot;
        }

        // Stored (in-transit) duplicants live in a MinionStorage component on
        // one of the rocket's cluster modules, not on the craft itself — same
        // lookup Clustercraft.DestroyCraftAndModules() uses internally.
        private static void CollectPassengers(Clustercraft craft, List<string> passengerIds)
        {
            foreach (var moduleRef in craft.ModuleInterface.ClusterModules)
            {
                var module = moduleRef?.Get();
                var minionStorage = module?.GetComponent<MinionStorage>();
                if (minionStorage == null)
                {
                    continue;
                }

                foreach (var info in minionStorage.GetStoredMinionInfo())
                {
                    var storedIdentity = info.serializedMinion?.Get()?.GetComponent<MinionIdentity>();
                    if (storedIdentity != null)
                    {
                        passengerIds.Add(storedIdentity.GetInstanceID().ToString());
                    }
                }
            }
        }
    }
}
