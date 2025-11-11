using ItemModKit.Core;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
 /// <summary>
 /// UI 选中项访问器：尝试从“物品详情面板”和“操作菜单”解析当前选中的物品。
 /// 内部使用弱引用缓存实例，避免频繁查找。
 /// </summary>
 internal sealed class DuckovUISelection : IUISelection
 {
 private System.WeakReference _detailsRef;
 private System.WeakReference _menuRef;

 /// <summary>从详情面板获取选中物品。</summary>
 public bool TryGetDetailsItem(out object item)
 {
 item = null;
 try
 {
 var inst = GetDetailsInstance();
 if (inst == null) return false;
 var t = inst.GetType();
 var p = DuckovReflectionCache.GetProp(t, "SelectedItem", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
 ?? DuckovReflectionCache.GetProp(t, "Target", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 if (p == null) return false;
 item = p.GetValue(inst, null);
 return item != null;
 }
 catch { return false; }
 }
 /// <summary>从操作菜单获取选中物品。</summary>
 public bool TryGetOperationMenuItem(out object item)
 {
 item = null;
 try
 {
 var inst = GetOperationMenuInstance(); if (inst == null) return false;
 var t = inst.GetType();
 var pDisp = DuckovReflectionCache.GetProp(t, "TargetDisplay", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 object display = pDisp?.GetValue(inst, null);
 if (display != null)
 {
 var td = DuckovReflectionCache.GetProp(display.GetType(), "Target", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 item = td?.GetValue(display, null);
 if (item != null) return true;
 }
 var fDisp = DuckovReflectionCache.GetField(t, "_TargetDisplay", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
 ?? DuckovReflectionCache.GetField(t, "___TargetDisplay", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 var d2 = fDisp?.GetValue(inst);
 if (d2 != null)
 {
 var td = DuckovReflectionCache.GetProp(d2.GetType(), "Target", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 item = td?.GetValue(d2, null);
 return item != null;
 }
 }
 catch { }
 return false;
 }
 /// <summary>优先菜单项，其次详情面板。</summary>
 public bool TryGetCurrentItem(out object item)
 {
 if (TryGetOperationMenuItem(out item)) return true;
 return TryGetDetailsItem(out item);
 }

 private object GetDetailsInstance()
 {
 try
 {
 object inst = _detailsRef != null && _detailsRef.IsAlive ? _detailsRef.Target : null;
 if (inst != null && IsActive(inst)) return inst;
 var t = FindType("Duckov.UI.ItemDetailsDisplay");
 inst = t != null ? UnityEngine.Object.FindObjectOfType(t) : null;
 _detailsRef = new System.WeakReference(inst);
 return inst;
 }
 catch { return null; }
 }
 private object GetOperationMenuInstance()
 {
 try
 {
 object inst = _menuRef != null && _menuRef.IsAlive ? _menuRef.Target : null;
 if (inst != null && IsActive(inst)) return inst;
 var t = FindType("Duckov.UI.ItemOperationMenu");
 inst = t != null ? UnityEngine.Object.FindObjectOfType(t) : null;
 _menuRef = new System.WeakReference(inst);
 return inst;
 }
 catch { return null; }
 }
 private static bool IsActive(object comp)
 {
 try
 {
 var cT = comp.GetType();
 var goProp = cT.GetProperty("gameObject", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
 var go = goProp?.GetValue(comp, null) as UnityEngine.GameObject;
 if (go != null) return go.activeInHierarchy;
 var en = cT.GetProperty("enabled", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
 if (en != null) { var v = en.GetValue(comp, null); if (v is bool b) return b; }
 }
 catch { }
 return true;
 }
 }
}
