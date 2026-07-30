using System.Collections.Generic;
using Klei.AI;

namespace OniAgent.Snapshot
{
    // Must be called from Unity's main thread — reads live game components.
    // See SnapshotTicker, which is the only caller.
    public static class SnapshotCollector
    {
        // v2 added WorldId for Spaced Out cluster support.
        // v3 added duplicant skill Attributes (Athletics/Strength/Cooking/etc.)
        // and AvailableSkillPoints.
        // v4 added Cycle.
        public const int SchemaVersion = 4;

        public static DuplicantSnapshotResponse CollectDuplicants()
        {
            var response = new DuplicantSnapshotResponse
            {
                SchemaVersion = SchemaVersion,
                Cycle = CycleLookup.CurrentCycle(),
            };

            foreach (var identity in Components.LiveMinionIdentities.Items)
            {
                response.Duplicants.Add(CollectOne(identity));
            }

            return response;
        }

        private static DuplicantSnapshot CollectOne(MinionIdentity identity)
        {
            var modifiers = identity.GetComponent<Modifiers>();
            var choreDriver = identity.GetComponent<ChoreDriver>();
            var traits = identity.GetComponent<Traits>();
            var resume = identity.GetComponent<MinionResume>();
            var effects = identity.GetComponent<Effects>();
            var position = identity.transform.position;

            var snapshot = new DuplicantSnapshot
            {
                // Unity's runtime object id — stable for this process only, not
                // across save/load. Swap for a save-stable id (e.g. KPrefabID)
                // once that type's home assembly is vendored and confirmed.
                Id = identity.GetInstanceID().ToString(),
                Name = identity.GetProperName(),
                PosX = position.x,
                PosY = position.y,
                WorldId = WorldLookup.WorldIdAt(position),
            };

            if (modifiers != null)
            {
                snapshot.Health = modifiers.amounts.Get(Db.Get().Amounts.HitPoints)?.value ?? 0f;
                snapshot.Stress = modifiers.amounts.Get(Db.Get().Amounts.Stress)?.value ?? 0f;

                // Only the trainable "profession" attributes (Athletics, Strength,
                // Cooking, etc. — the ones shown on the Skills screen), not the much
                // larger set of internal bookkeeping attributes (deltas, expectations)
                // that Modifiers.attributes also carries.
                if (modifiers.attributes != null)
                {
                    foreach (var attributeInstance in modifiers.attributes)
                    {
                        if (attributeInstance.Attribute.IsTrainable && attributeInstance.Attribute.IsProfession)
                        {
                            snapshot.Attributes.Add(new AttributeSnapshot
                            {
                                Id = attributeInstance.Id,
                                Name = attributeInstance.Name,
                                Value = attributeInstance.GetTotalValue(),
                            });
                        }
                    }
                }
            }

            var currentChore = choreDriver?.GetCurrentChore();
            snapshot.CurrentChore = currentChore?.choreType?.Id;

            if (traits != null)
            {
                snapshot.TraitIds = new List<string>(traits.GetTraitIds());
            }

            if (resume != null)
            {
                foreach (var kv in resume.MasteryBySkillID)
                {
                    if (kv.Value)
                    {
                        snapshot.MasteredSkillIds.Add(kv.Key);
                    }
                }

                snapshot.AvailableSkillPoints = resume.AvailableSkillpoints;
            }

            if (effects != null)
            {
                foreach (var effectInstance in effects.GetTimeLimitedEffects())
                {
                    snapshot.Effects.Add(new EffectSnapshot
                    {
                        Id = effectInstance.effect.Id,
                        Name = effectInstance.effect.Name,
                        TimeRemaining = effectInstance.timeRemaining,
                    });
                }
            }

            return snapshot;
        }
    }
}
