using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ItemStatsSystem;
using ItemModKit.Adapters.Duckov;

namespace ItemModKit.Patches
{
    [HarmonyPatch]
    internal static class Item_OnBeforeSerialize_Patch
    {
        private static List<MethodBase> _targets;

        private static bool Prepare()
        {
            _targets = new List<MethodBase>();
            try
            {
                var itemBase = typeof(Item);
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var type in types)
                    {
                        try
                        {
                            if (type == null || !itemBase.IsAssignableFrom(type))
                            {
                                continue;
                            }

                            var method = type.GetMethod("OnBeforeSerialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (method != null)
                            {
                                _targets.Add(method);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }

            return _targets.Count > 0;
        }

        private static IEnumerable<MethodBase> TargetMethods() => _targets;

        private static void Prefix(object __instance)
        {
            try
            {
                DuckovPersistenceLifecycleBridge.SyncBeforeSerialize(__instance as Item);
            }
            catch
            {
            }
        }
    }
}