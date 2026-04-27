using System;
using System.Collections.Concurrent;
using ItemModKit.Core;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
    internal static class DuckovUISelectionResolver
    {
        private static System.WeakReference s_detailsRef;
        private static System.WeakReference s_menuRef;
        private static readonly ConcurrentDictionary<string, byte> s_reportedSelectionFailures = new ConcurrentDictionary<string, byte>();

        private static void ReportSelectionFailureOnce(string operation, Exception ex)
        {
            if (string.IsNullOrEmpty(operation) || ex == null) return;
            if (!s_reportedSelectionFailures.TryAdd(operation, 0)) return;
            Log.Warn($"[IMK.UISelection] {operation} degraded: {ex.GetType().Name}: {ex.Message}");
        }

        public static bool TryGetDetailsItem(out object item)
        {
            item = null;
            try
            {
                var inst = GetDetailsInstance();
                if (inst == null) return false;
                var t = inst.GetType();
                var p = DuckovReflectionCache.GetProp(t, "SelectedItem", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?? DuckovReflectionCache.GetProp(t, "Target", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (p == null) return false;
                item = p.GetValue(inst, null);
                return item != null;
            }
            catch (Exception ex) { ReportSelectionFailureOnce("TryGetDetailsItem", ex); return false; }
        }

        public static bool TryGetOperationMenuItem(out object item)
        {
            item = null;
            try
            {
                var inst = GetOperationMenuInstance(); if (inst == null) return false;
                var t = inst.GetType();
                var pDisp = DuckovReflectionCache.GetProp(t, "TargetDisplay", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                object display = pDisp?.GetValue(inst, null);
                if (display != null)
                {
                    var td = DuckovReflectionCache.GetProp(display.GetType(), "Target", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    item = td?.GetValue(display, null);
                    if (item != null) return true;
                }
                var fDisp = DuckovReflectionCache.GetField(t, "_TargetDisplay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?? DuckovReflectionCache.GetField(t, "___TargetDisplay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var d2 = fDisp?.GetValue(inst);
                if (d2 != null)
                {
                    var td = DuckovReflectionCache.GetProp(d2.GetType(), "Target", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    item = td?.GetValue(d2, null);
                    return item != null;
                }
            }
            catch (Exception ex) { ReportSelectionFailureOnce("TryGetOperationMenuItem", ex); }
            return false;
        }

        public static bool TryGetCurrentItem(out object item)
        {
            if (TryGetOperationMenuItem(out item)) return true;
            return TryGetDetailsItem(out item);
        }

        private static object GetDetailsInstance()
        {
            try
            {
                object inst = s_detailsRef != null && s_detailsRef.IsAlive ? s_detailsRef.Target : null;
                if (inst != null && IsActive(inst)) return inst;
                var t = FindType("Duckov.UI.ItemDetailsDisplay");
                inst = t != null ? UnityEngine.Object.FindObjectOfType(t) : null;
                s_detailsRef = new System.WeakReference(inst);
                return inst;
            }
            catch (Exception ex) { ReportSelectionFailureOnce("GetDetailsInstance", ex); return null; }
        }

        private static object GetOperationMenuInstance()
        {
            try
            {
                object inst = s_menuRef != null && s_menuRef.IsAlive ? s_menuRef.Target : null;
                if (inst != null && IsActive(inst)) return inst;
                var t = FindType("Duckov.UI.ItemOperationMenu");
                inst = t != null ? UnityEngine.Object.FindObjectOfType(t) : null;
                s_menuRef = new System.WeakReference(inst);
                return inst;
            }
            catch (Exception ex) { ReportSelectionFailureOnce("GetOperationMenuInstance", ex); return null; }
        }

        private static bool IsActive(object comp)
        {
            try
            {
                var cT = comp.GetType();
                var goProp = cT.GetProperty("gameObject", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var go = goProp?.GetValue(comp, null) as UnityEngine.GameObject;
                if (go != null) return go.activeInHierarchy;
                var en = cT.GetProperty("enabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (en != null) { var v = en.GetValue(comp, null); if (v is bool b) return b; }
            }
            catch (Exception ex) { ReportSelectionFailureOnce("IsActive", ex); }
            return true;
        }
    }
}
