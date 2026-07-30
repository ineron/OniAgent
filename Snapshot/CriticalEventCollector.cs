using System.Collections.Generic;

namespace OniAgent.Snapshot
{
    // Must be called from Unity's main thread — reads live game components,
    // same as SnapshotCollector/ColonySnapshotCollector. See
    // oni-confirmed-game-api-surface-for-critical-event-tier-via-decompile
    // for the game API this reads and why each threshold was chosen.
    //
    // Unlike the operational/environmental tiers, this tier is edge-
    // triggered: Collect() only returns something on a transition into a
    // dangerous state, mirroring how the game's own state machines (Health,
    // StressMonitor, SicknessMonitor, BuildingHP) gate on threshold-crossing
    // rather than raw values. Per the 3-tier design, events like these are
    // pushed to Ledgyx immediately rather than batched on a cron — see
    // OniAgent.Networking.CriticalEventPushClient, which SnapshotTicker
    // hands each new batch of events to as soon as Collect() returns them.
    public static class CriticalEventCollector
    {
        // v2 added Cycle.
        public const int SchemaVersion = 2;

        // Previous-tick state, keyed by the same GetInstanceID()-based ids
        // used elsewhere in this mod. An id absent from the "last" map means
        // "never observed before this tick" — treated as an implicit healthy
        // baseline (Perfect / tier 0 / Powered), NOT "skip this entity".
        // Verified against a real 1625-cycle save (2026-07-28): a duplicant
        // chronically stuck at max stress tier for a long time produced zero
        // events under an earlier version of this code that only recorded a
        // silent baseline on first sight and then required a further
        // transition — since the tier never changed tick-to-tick, it never
        // fired, even though "a duplicant has been rampaging from stress"
        // is exactly what this tier exists to report. Assuming a healthy
        // baseline for unseen entities means the very first observation of
        // an already-dangerous, long-standing condition (a save loaded
        // mid-crisis, or the mod loaded after the state had already gone
        // bad) fires immediately instead of being silently swallowed. The
        // trade-off — one alert per already-bad entity at mod load, instead
        // of zero — is the correct side to err on for an observability tool.
        //
        // These maps grow for the lifetime of the process (a dead
        // duplicant's id is never removed from lastHealthState, etc.) —
        // intentionally not pruned. The leak is bounded by "total distinct
        // duplicants/consumers that ever existed this session", a few dozen
        // to low hundreds of tiny entries even in a very long game — not
        // worth the complexity of eviction. lastBuildings is the one
        // exception: it's fully rebuilt every tick (see below), so destroyed
        // buildings are naturally dropped, not leaked.
        private static readonly Dictionary<string, Health.HealthState> lastHealthState
            = new Dictionary<string, Health.HealthState>();
        private static readonly Dictionary<string, int> lastStressTier
            = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> lastSicknessTier
            = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> lastOxygenTier
            = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> lastHungerTier
            = new Dictionary<string, int>();
        private static readonly Dictionary<string, bool> lastDeadTag
            = new Dictionary<string, bool>();
        private static readonly Dictionary<string, bool> lastConsumerPowered
            = new Dictionary<string, bool>();
        private static Dictionary<string, BuildingMeta> lastBuildings
            = new Dictionary<string, BuildingMeta>();

        private struct BuildingMeta
        {
            public string Name;
            public string PrefabId;
            public int WorldId;
        }

        public static List<CriticalEvent> Collect()
        {
            var events = new List<CriticalEvent>();
            var now = System.DateTime.UtcNow.ToString("o");

            CollectDuplicantEvents(events, now);
            CollectDeathEvents(events, now);
            CollectBuildingDestroyedEvents(events, now);
            CollectPowerOutageEvents(events, now);

            // Set once for the whole batch rather than threading it through
            // every helper alongside `now` — a batch spans at most a few
            // seconds of real time, never a cycle boundary in practice.
            var cycle = CycleLookup.CurrentCycle();
            foreach (var evt in events)
            {
                evt.Cycle = cycle;
            }

            return events;
        }

        // Health.State realistically never reaches Dead in practice — traced
        // via decompile (Health.cs): hitpoint depletion routes through
        // Incapacitate() instead, and non-HP deaths (stress, sickness,
        // suffocation, etc.) kill via DeathMonitor.Instance.Kill(...)
        // directly, which never touches Health.State at all. So the old
        // `state == Health.HealthState.Dead` branch here was dead code —
        // confirmed by a real drowning death producing zero notification.
        // Actual death is now handled separately by CollectDeathEvents,
        // which watches GameTags.Dead (the tag DeathMonitor's "dead" state
        // adds, universally, regardless of cause of death).
        private static void CollectDuplicantEvents(List<CriticalEvent> events, string now)
        {
            foreach (var identity in Components.LiveMinionIdentities.Items)
            {
                var id = identity.GetInstanceID().ToString();
                var name = identity.GetProperName();
                var worldId = WorldLookup.WorldIdAt(identity.transform.position);

                var health = identity.GetComponent<Health>();
                if (health != null)
                {
                    var state = health.State;
                    var previousState = lastHealthState.TryGetValue(id, out var seenState)
                        ? seenState
                        : Health.HealthState.Perfect;
                    if (previousState != state
                        && (state == Health.HealthState.Critical
                            || state == Health.HealthState.Incapacitated))
                    {
                        events.Add(new CriticalEvent
                        {
                            EventType = "DuplicantHealth" + state,
                            EntityId = id,
                            EntityName = name,
                            WorldId = worldId,
                            Detail = previousState + " -> " + state,
                            CapturedAt = now,
                        });
                    }
                    lastHealthState[id] = state;
                }

                // Read thresholds via the game's own SMI methods rather than
                // hardcoding the 60/100 stress values ourselves, so a future
                // Klei rebalance can't silently desync our tiers from the
                // game's (see the pattern node cited above).
                var stressSmi = identity.GetSMI<StressMonitor.Instance>();
                if (stressSmi != null)
                {
                    var tier = stressSmi.HasHadEnough() ? 2 : (stressSmi.IsStressed() ? 1 : 0);
                    var previousTier = lastStressTier.TryGetValue(id, out var seenTier) ? seenTier : 0;
                    if (tier > previousTier)
                    {
                        events.Add(new CriticalEvent
                        {
                            EventType = tier == 2 ? "DuplicantStressBreakdown" : "DuplicantStressed",
                            EntityId = id,
                            EntityName = name,
                            WorldId = worldId,
                            CapturedAt = now,
                        });
                    }
                    lastStressTier[id] = tier;
                }

                var sicknessSmi = identity.GetSMI<SicknessMonitor.Instance>();
                if (sicknessSmi != null)
                {
                    var tier = sicknessSmi.HasMajorDisease() ? 2 : (sicknessSmi.IsSick() ? 1 : 0);
                    var previousTier = lastSicknessTier.TryGetValue(id, out var seenTier) ? seenTier : 0;
                    if (tier > previousTier)
                    {
                        events.Add(new CriticalEvent
                        {
                            EventType = tier == 2 ? "DuplicantMajorDisease" : "DuplicantSick",
                            EntityId = id,
                            EntityName = name,
                            WorldId = worldId,
                            CapturedAt = now,
                        });
                    }
                    lastSicknessTier[id] = tier;
                }

                // Oxygen is the fastest-moving danger this tier tracks — a
                // duplicant can go from fine to suffocating within a single
                // polling interval — so this is reported as an early-warning
                // tier (low oxygen, then actively suffocating) well before
                // SuffocationMonitor's own "death" state fires
                // DeathMonitor.Instance.Kill(Deaths.Suffocation), which
                // CollectDeathEvents catches separately. Tier thresholds
                // mirror SuffocationMonitor's own state machine (satisfied ->
                // satisfied.low -> noOxygen.holdingbreath/suffocating) rather
                // than hardcoding breath-meter values.
                var suffocationSmi = identity.GetSMI<SuffocationMonitor.Instance>();
                if (suffocationSmi != null)
                {
                    var tier = suffocationSmi.IsSuffocating() ? 2 : (!suffocationSmi.CanBreath() ? 1 : 0);
                    var previousTier = lastOxygenTier.TryGetValue(id, out var seenTier) ? seenTier : 0;
                    if (tier > previousTier)
                    {
                        events.Add(new CriticalEvent
                        {
                            EventType = tier == 2 ? "DuplicantSuffocating" : "DuplicantLowOxygen",
                            EntityId = id,
                            EntityName = name,
                            WorldId = worldId,
                            CapturedAt = now,
                        });
                    }
                    lastOxygenTier[id] = tier;
                }

                // Hunger mirrors the oxygen tier exactly: satisfied ->
                // hungry -> starving, read via CalorieMonitor.Instance's own
                // IsHungry()/IsStarving() (confirmed via decompile of
                // CalorieMonitor.cs) rather than hardcoding calorie
                // thresholds. Actual starvation death is NOT handled here —
                // CalorieMonitor's "depleted" state already calls
                // DeathMonitor.Instance.Kill(Deaths.Starvation), which sets
                // GameTags.Dead and is caught by CollectDeathEvents, same as
                // every other cause of death.
                var calorieSmi = identity.GetSMI<CalorieMonitor.Instance>();
                if (calorieSmi != null)
                {
                    var tier = calorieSmi.IsStarving() ? 2 : (calorieSmi.IsHungry() ? 1 : 0);
                    var previousTier = lastHungerTier.TryGetValue(id, out var seenTier) ? seenTier : 0;
                    if (tier > previousTier)
                    {
                        events.Add(new CriticalEvent
                        {
                            EventType = tier == 2 ? "DuplicantStarving" : "DuplicantHungry",
                            EntityId = id,
                            EntityName = name,
                            WorldId = worldId,
                            CapturedAt = now,
                        });
                    }
                    lastHungerTier[id] = tier;
                }
            }
        }

        // Deliberately iterates Components.MinionIdentities (every minion
        // ever spawned this session, live or dead) rather than
        // LiveMinionIdentities: MinionIdentity.OnDied is subscribed to
        // GameTags.Dead being added and removes the identity from
        // LiveMinionIdentities synchronously, in the same game-event tick
        // that the tag is added — so by the time this collector's next poll
        // runs, a duplicant who just died is already gone from the live
        // list and their death is never observed. MinionIdentities is only
        // pruned later, in OnCleanUp/OnQueueDestroyObject (full GameObject
        // destruction, e.g. cremation/burial), so a corpse stays visible to
        // this loop long enough for the next poll to catch the transition.
        // Confirmed via decompile after a real drowning death produced no
        // event under the previous Health.State-based check (see
        // CollectDuplicantEvents' header comment).
        private static void CollectDeathEvents(List<CriticalEvent> events, string now)
        {
            foreach (var identity in Components.MinionIdentities.Items)
            {
                var id = identity.GetInstanceID().ToString();
                var isDead = identity.gameObject.HasTag(GameTags.Dead);
                var wasDead = lastDeadTag.TryGetValue(id, out var seenDead) && seenDead;

                if (isDead && !wasDead)
                {
                    events.Add(new CriticalEvent
                    {
                        EventType = "DuplicantDied",
                        EntityId = id,
                        EntityName = identity.GetProperName(),
                        WorldId = WorldLookup.WorldIdAt(identity.transform.position),
                        CapturedAt = now,
                    });
                }
                lastDeadTag[id] = isDead;
            }
        }

        // "Destroyed" means removed from the world entirely, not merely
        // damaged — most building defs don't set destroyOnDamaged, so a
        // building at 0 HP (BuildingHP.IsBroken) just sits there awaiting
        // repair rather than disappearing. The only reliable signal for
        // actual destruction is a building id present last tick and absent
        // this tick, so this rebuilds the full id set every tick (same list
        // ColonySnapshotCollector already walks for the operational tier)
        // and diffs it against the previous tick's snapshot.
        private static void CollectBuildingDestroyedEvents(List<CriticalEvent> events, string now)
        {
            var current = new Dictionary<string, BuildingMeta>();

            foreach (var building in Components.BuildingCompletes.Items)
            {
                var def = building.Def;
                if (def.ObjectLayer != ObjectLayer.Building || def.IsFoundation || def.IsTilePiece)
                {
                    continue;
                }

                var id = building.GetInstanceID().ToString();
                var prefabId = building.GetComponent<KPrefabID>();
                var selectable = building.GetComponent<KSelectable>();
                current[id] = new BuildingMeta
                {
                    Name = selectable != null ? ColonySnapshotCollector.CleanName(selectable.GetName()) : null,
                    PrefabId = prefabId != null ? prefabId.PrefabTag.Name : null,
                    WorldId = WorldLookup.WorldIdAt(building.transform.position),
                };
            }

            foreach (var kv in lastBuildings)
            {
                if (current.ContainsKey(kv.Key))
                {
                    continue;
                }

                events.Add(new CriticalEvent
                {
                    EventType = "BuildingDestroyed",
                    EntityId = kv.Key,
                    EntityName = kv.Value.Name,
                    WorldId = kv.Value.WorldId,
                    Detail = kv.Value.PrefabId,
                    CapturedAt = now,
                });
            }

            lastBuildings = current;
        }

        // A consumer is only interesting once it's wired to a circuit
        // (IsConnected) and actually wants power (WattsNeededWhenActive > 0)
        // — a consumer that was simply never wired up is not an outage.
        // IEnergyConsumer only exposes IsConnected/IsPowered booleans, not
        // CircuitManager's richer ConnectionStatus enum (confirmed via
        // decompile — see the pattern node), so the outage signal is
        // reconstructed from those two: was (Connected, Powered), now
        // (Connected, !Powered).
        private static void CollectPowerOutageEvents(List<CriticalEvent> events, string now)
        {
            foreach (var consumer in Components.EnergyConsumers.Items)
            {
                if (consumer.WattsNeededWhenActive <= 0f)
                {
                    continue;
                }

                var id = consumer.GetInstanceID().ToString();

                if (!consumer.IsConnected)
                {
                    lastConsumerPowered.Remove(id);
                    continue;
                }

                var poweredNow = consumer.IsPowered;
                var wasPowered = lastConsumerPowered.TryGetValue(id, out var seenPowered) ? seenPowered : true;
                if (wasPowered && !poweredNow)
                {
                    events.Add(new CriticalEvent
                    {
                        EventType = "PowerOutage",
                        EntityId = id,
                        EntityName = ColonySnapshotCollector.CleanName(consumer.Name),
                        WorldId = WorldLookup.WorldIdAt(consumer.transform.position),
                        CapturedAt = now,
                    });
                }
                lastConsumerPowered[id] = poweredNow;
            }
        }
    }
}
