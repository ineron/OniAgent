using System.Collections.Generic;

namespace OniAgent.Snapshot
{
    public class RocketSnapshot
    {
        public string Id;
        public string Name;

        // Clustercraft.CraftStatus: Grounded, Launching, InFlight, Landing.
        public string Status;

        public int LocationQ;
        public int LocationR;
        public int DestinationQ;
        public int DestinationR;

        // Null until the rocket has an interior world (Klei creates one on
        // first launch); ties back to WorldSnapshot.Id.
        public int? InteriorWorldId;

        public float Speed;
        public float EnginePower;
        public bool HasFuelToMove;

        public List<string> PassengerDuplicantIds = new List<string>();
        public List<RocketCargoBaySnapshot> CargoBays = new List<RocketCargoBaySnapshot>();
    }

    public class RocketCargoBaySnapshot
    {
        // CargoBay.CargoType: Solids, Liquids, Gasses, Entities.
        public string CargoType;
        public float MaxCapacity;
        public float RemainingCapacity;
    }
}
