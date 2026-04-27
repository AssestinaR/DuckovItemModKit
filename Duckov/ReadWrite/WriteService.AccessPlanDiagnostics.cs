using System;
using System.Collections.Generic;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    internal sealed partial class WriteService : IWriteService
    {
        internal static Dictionary<string, object> CollectAccessPlanDiagnostics(IEnumerable<object> sampleItems)
        {
            var samples = new List<object>();
            if (sampleItems != null)
            {
                foreach (var sample in sampleItems)
                {
                    if (sample != null) samples.Add(sample);
                }
            }

            return new Dictionary<string, object>
            {
                ["sampleCount"] = samples.Count,
                ["stats"] = CollectStatsAccessPlanDiagnostics(samples),
                ["modifiers"] = CollectModifierAccessPlanDiagnostics(samples),
                ["slots"] = CollectSlotAccessPlanDiagnostics(samples),
            };
        }

        private static Dictionary<string, object> CollectStatsAccessPlanDiagnostics(List<object> sampleItems)
        {
            var ownerTypes = new HashSet<Type>();
            var statsHosts = new List<object>();
            var missing = new List<string>();
            var createMissing = false;
            var getterMissing = false;

            foreach (var sample in sampleItems)
            {
                var ownerType = sample?.GetType();
                if (ownerType == null || !ownerTypes.Add(ownerType)) continue;

                var hostPlan = GetStatsHostPlan(ownerType);
                if (hostPlan.StatsGetter == null) getterMissing = true;
                if (hostPlan.CreateStatsComponent == null) createMissing = true;

                var stats = hostPlan.StatsGetter?.Invoke(sample);
                if (stats != null) statsHosts.Add(stats);
            }

            if (getterMissing) missing.Add("Stats getter");
            if (createMissing) missing.Add("CreateStatsComponent");

            var lookupAvailable = false;
            var setDirtyAvailable = false;
            object sampledStat = null;
            string statsTypeName = string.Empty;
            foreach (var stats in statsHosts)
            {
                if (stats == null) continue;
                statsTypeName = stats.GetType().FullName ?? string.Empty;
                var plan = GetStatsCollectionPlan(stats.GetType());
                if (plan.GetByIndexer != null || plan.GetByKey != null) lookupAvailable = true;
                if (plan.SetDirty != null) setDirtyAvailable = true;

                if (stats is System.Collections.IEnumerable enumerable)
                {
                    foreach (var entry in enumerable)
                    {
                        if (entry == null) continue;
                        sampledStat = entry;
                        break;
                    }
                }

                if (sampledStat != null) break;
            }

            if (!lookupAvailable) missing.Add("Stat lookup by key");
            if (!setDirtyAvailable) missing.Add("Stats.SetDirty");

            var valueWriteAvailable = false;
            var statTypeName = string.Empty;
            if (sampledStat != null)
            {
                statTypeName = sampledStat.GetType().FullName ?? string.Empty;
                var writePlan = GetStatValueWritePlan(sampledStat.GetType());
                valueWriteAvailable = writePlan.BaseValueSetter != null
                    || writePlan.ValueMethods.Length > 0
                    || writePlan.PropertySetters.Length > 0
                    || writePlan.WritableFields.Length > 0;
            }

            if (!valueWriteAvailable) missing.Add(sampledStat == null ? "Sample stat instance" : "Stat value write chain");

            var status = ownerTypes.Count == 0
                ? "unknown"
                : (lookupAvailable && valueWriteAvailable && !getterMissing ? "complete" : "degraded");

            var summary = status == "complete"
                ? "stats writable"
                : status == "unknown"
                    ? "stats write plan not sampled"
                    : "stats writable but degraded";

            return new Dictionary<string, object>
            {
                ["status"] = status,
                ["summary"] = summary,
                ["sampleOwnerTypeCount"] = ownerTypes.Count,
                ["statsHostObserved"] = statsHosts.Count > 0,
                ["statsType"] = statsTypeName,
                ["sampleStatType"] = statTypeName,
                ["lookupByKeyAvailable"] = lookupAvailable,
                ["setDirtyAvailable"] = setDirtyAvailable,
                ["valueWriteAvailable"] = valueWriteAvailable,
                ["degraded"] = status == "degraded",
                ["missingMembers"] = missing.ToArray(),
            };
        }

        private static Dictionary<string, object> CollectModifierAccessPlanDiagnostics(List<object> sampleItems)
        {
            var ownerTypes = new HashSet<Type>();
            var hosts = new List<object>();
            var getterMissing = false;
            var createMissing = false;

            foreach (var sample in sampleItems)
            {
                var ownerType = sample?.GetType();
                if (ownerType == null || !ownerTypes.Add(ownerType)) continue;

                var hostPlan = GetModifierHostPlan(ownerType);
                if (hostPlan.HostGetter == null) getterMissing = true;
                if (hostPlan.CreateHost == null) createMissing = true;

                var host = hostPlan.HostGetter?.Invoke(sample);
                if (host != null) hosts.Add(host);
            }

            var clearAvailable = false;
            var reapplyAvailable = false;
            string hostTypeName = string.Empty;
            foreach (var host in hosts)
            {
                if (host == null) continue;
                hostTypeName = host.GetType().FullName ?? string.Empty;
                var plan = GetModifierCollectionPlan(host.GetType());
                if (plan.Clear != null) clearAvailable = true;
                if (plan.Reapply != null) reapplyAvailable = true;
                if (clearAvailable && reapplyAvailable) break;
            }

            var missing = new List<string>();
            if (getterMissing) missing.Add("Modifiers getter");
            if (createMissing) missing.Add("CreateModifiersComponent");
            if (!clearAvailable) missing.Add("Modifiers.Clear/ClearModifiers");
            if (!reapplyAvailable) missing.Add("Modifiers.ReapplyModifiers");

            var status = ownerTypes.Count == 0
                ? "unknown"
                : (!getterMissing && clearAvailable && reapplyAvailable ? "complete" : "degraded");

            var summary = status == "complete"
                ? "modifier host operations ready"
                : status == "unknown"
                    ? "modifier host plan not sampled"
                    : "modifier host operations partially constrained";

            return new Dictionary<string, object>
            {
                ["status"] = status,
                ["summary"] = summary,
                ["sampleOwnerTypeCount"] = ownerTypes.Count,
                ["hostObserved"] = hosts.Count > 0,
                ["hostType"] = hostTypeName,
                ["clearAvailable"] = clearAvailable,
                ["reapplyAvailable"] = reapplyAvailable,
                ["degraded"] = status == "degraded",
                ["missingMembers"] = missing.ToArray(),
            };
        }

        private static Dictionary<string, object> CollectSlotAccessPlanDiagnostics(List<object> sampleItems)
        {
            var ownerTypes = new HashSet<Type>();
            var slotHosts = new List<object>();
            var getterMissing = false;
            var createMissing = false;

            foreach (var sample in sampleItems)
            {
                var ownerType = sample?.GetType();
                if (ownerType == null || !ownerTypes.Add(ownerType)) continue;

                var hostPlan = GetSlotHostPlan(ownerType);
                if (hostPlan.SlotsGetter == null) getterMissing = true;
                if (hostPlan.CreateSlotsComponent == null) createMissing = true;

                var host = hostPlan.SlotsGetter?.Invoke(sample);
                if (host != null) slotHosts.Add(host);
            }

            var getSlotAvailable = false;
            var listFallbackAvailable = false;
            var cacheInvalidationAvailable = false;
            object sampledSlot = null;
            string slotsTypeName = string.Empty;
            foreach (var slots in slotHosts)
            {
                if (slots == null) continue;
                slotsTypeName = slots.GetType().FullName ?? string.Empty;
                var plan = GetSlotCollectionPlan(slots.GetType());
                if (plan.GetSlotByKey != null) getSlotAvailable = true;
                if (plan.BackingListField != null) listFallbackAvailable = true;
                if (plan.CachedDictionaryField != null) cacheInvalidationAvailable = true;

                if (slots is System.Collections.IEnumerable enumerable)
                {
                    foreach (var entry in enumerable)
                    {
                        if (entry == null) continue;
                        sampledSlot = entry;
                        break;
                    }
                }

                if (sampledSlot != null) break;
            }

            var contentAvailable = false;
            var unplugAvailable = false;
            var changedAvailable = false;
            string slotTypeName = string.Empty;
            if (sampledSlot != null)
            {
                slotTypeName = sampledSlot.GetType().FullName ?? string.Empty;
                var plan = GetSlotInstancePlan(sampledSlot.GetType());
                contentAvailable = plan.ContentProperty != null;
                unplugAvailable = plan.Unplug != null;
                changedAvailable = plan.Changed != null;
            }

            var missing = new List<string>();
            if (getterMissing) missing.Add("Slots getter");
            if (createMissing) missing.Add("CreateSlotsComponent");
            if (!getSlotAvailable && !listFallbackAvailable) missing.Add("GetSlot/list fallback");
            if (!contentAvailable) missing.Add(sampledSlot == null ? "Sample slot instance" : "Slot.Content");
            if (!unplugAvailable) missing.Add(sampledSlot == null ? "Sample slot instance" : "Slot.Unplug");
            if (!cacheInvalidationAvailable) missing.Add("_cachedSlotsDictionary");

            var status = ownerTypes.Count == 0
                ? "unknown"
                : (!getterMissing && (getSlotAvailable || listFallbackAvailable) && contentAvailable && unplugAvailable ? "complete" : "degraded");

            var summary = status == "complete"
                ? "slot host operations ready"
                : status == "unknown"
                    ? "slot host plan not sampled"
                    : "slot host operations partially constrained";

            return new Dictionary<string, object>
            {
                ["status"] = status,
                ["summary"] = summary,
                ["sampleOwnerTypeCount"] = ownerTypes.Count,
                ["slotHostObserved"] = slotHosts.Count > 0,
                ["slotsType"] = slotsTypeName,
                ["sampleSlotType"] = slotTypeName,
                ["getSlotAvailable"] = getSlotAvailable,
                ["listFallbackAvailable"] = listFallbackAvailable,
                ["cacheInvalidationAvailable"] = cacheInvalidationAvailable,
                ["contentAvailable"] = contentAvailable,
                ["unplugAvailable"] = unplugAvailable,
                ["changedEventAvailable"] = changedAvailable,
                ["degraded"] = status == "degraded",
                ["missingMembers"] = missing.ToArray(),
            };
        }
    }
}