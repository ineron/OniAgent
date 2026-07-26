using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OniAgent.Snapshot
{
    // Must be called from Unity's main thread — reads live game components.
    // See SnapshotTicker, which is the only caller.
    public static class ColonySnapshotCollector
    {
        public const int SchemaVersion = 1;

        private static readonly Regex RichTextTag = new Regex("<.*?>", RegexOptions.Compiled);

        public static ColonySnapshot Collect()
        {
            var snapshot = new ColonySnapshot { SchemaVersion = SchemaVersion };

            foreach (var building in Components.BuildingCompletes.Items)
            {
                // Components.BuildingCompletes also holds every placed tile,
                // wall, ladder, wire, and conduit segment (confirmed via
                // decompile of BuildingTemplates.CreateFoundationTileDef /
                // CreateLadderDef / BaseWireConfig) — tens of thousands of
                // entries on a real base, none of them things an agent would
                // ever act on. Real buildings sit on ObjectLayer.Building and
                // are neither a foundation piece nor a tile-layer overlay.
                var def = building.Def;
                if (def.ObjectLayer != ObjectLayer.Building || def.IsFoundation || def.IsTilePiece)
                {
                    continue;
                }

                snapshot.Buildings.Add(CollectBuilding(building));
            }

            CollectPower(snapshot.Power);
            CollectResearch(snapshot.Research);

            return snapshot;
        }

        private static BuildingSnapshot CollectBuilding(BuildingComplete building)
        {
            var prefabId = building.GetComponent<KPrefabID>();
            var selectable = building.GetComponent<KSelectable>();
            var operational = building.GetComponent<Operational>();
            var prioritizable = building.GetComponent<Prioritizable>();
            var storage = building.GetComponent<Storage>();
            var position = building.transform.position;

            var snapshot = new BuildingSnapshot
            {
                // Unity's runtime object id — stable for this process only, not
                // across save/load, matching the convention used for duplicants.
                Id = building.GetInstanceID().ToString(),
                PrefabId = prefabId != null ? prefabId.PrefabTag.Name : null,
                Name = selectable != null ? CleanName(selectable.GetName()) : null,
                PosX = position.x,
                PosY = position.y,
            };

            if (operational != null)
            {
                snapshot.IsOperational = operational.IsOperational;
            }

            if (prioritizable != null)
            {
                var priority = prioritizable.GetMasterPriority();
                snapshot.PriorityClass = priority.priority_class.ToString();
                snapshot.PriorityValue = priority.priority_value;
            }

            if (storage != null)
            {
                foreach (var item in storage.GetItems())
                {
                    var primaryElement = item.GetComponent<PrimaryElement>();
                    if (primaryElement == null)
                    {
                        continue;
                    }
                    snapshot.StoredItems.Add(new StoredItemSnapshot
                    {
                        ElementId = primaryElement.ElementID.ToString(),
                        Mass = primaryElement.Mass,
                    });
                }
            }

            return snapshot;
        }

        private static void CollectPower(PowerSnapshot power)
        {
            foreach (var generator in Components.Generators.Items)
            {
                power.Generators.Add(new GeneratorSnapshot
                {
                    Id = generator.GetInstanceID().ToString(),
                    Name = CleanName(generator.GetComponent<KSelectable>()?.GetName()),
                    WattageRating = generator.WattageRating,
                    JoulesAvailable = generator.JoulesAvailable,
                    Capacity = generator.Capacity,
                    IsProducingPower = generator.IsProducingPower(),
                });
            }

            foreach (var battery in Components.Batteries.Items)
            {
                power.Batteries.Add(new BatterySnapshot
                {
                    Id = battery.GetInstanceID().ToString(),
                    Name = CleanName(battery.Name),
                    JoulesAvailable = battery.JoulesAvailable,
                    Capacity = battery.Capacity,
                    PercentFull = battery.PercentFull,
                });
            }

            foreach (var consumer in Components.EnergyConsumers.Items)
            {
                power.Consumers.Add(new ConsumerSnapshot
                {
                    Id = consumer.GetInstanceID().ToString(),
                    Name = CleanName(consumer.Name),
                    WattsUsed = consumer.WattsUsed,
                    WattsNeededWhenActive = consumer.WattsNeededWhenActive,
                    IsPowered = consumer.IsPowered,
                });
            }
        }

        private static void CollectResearch(ResearchSnapshot research)
        {
            var instance = Research.Instance;
            if (instance == null)
            {
                return;
            }

            var activeResearch = instance.GetActiveResearch();
            if (activeResearch != null)
            {
                research.ActiveTechId = activeResearch.tech.Id;
                research.ActiveTechPercentComplete = activeResearch.GetTotalPercentageComplete();
            }

            foreach (var queued in instance.GetResearchQueue())
            {
                research.QueuedTechIds.Add(queued.tech.Id);
            }

            var pointInventory = instance.UseGlobalPointInventory
                ? instance.globalPointInventory
                : activeResearch?.progressInventory;
            if (pointInventory != null)
            {
                foreach (var kv in pointInventory.PointsByTypeID)
                {
                    research.ResearchPointsByType[kv.Key] = kv.Value;
                }
            }
        }

        // KSelectable.GetName() and friends return Unity rich-text with
        // <link="ID">Display Name</link> wrapping (confirmed against a real
        // save's output) — strip markup so the agent sees plain text.
        private static string CleanName(string name)
        {
            return name != null ? RichTextTag.Replace(name, "") : null;
        }
    }
}
