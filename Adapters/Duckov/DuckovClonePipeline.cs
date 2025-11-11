using System;
using System.Collections.Generic;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 克隆管线：支持两种克隆策略并尝试放入背包。
    /// - TreeData 策略：通过树数据服务重建完整子树（保真度更高）
    /// - Unity 策略：直接克隆 GameObject 并取同类型组件（简单快速）
    /// 可选变量合并/拷贝标签与 UI 刷新，返回诊断信息。
    /// </summary>
    internal sealed class DuckovClonePipeline : IClonePipeline
    {
        /// <summary>
        /// 从源物品克隆一个副本，按策略选择克隆方式，并尝试放入解析到的目标背包。
        /// </summary>
        public RichResult<ClonePipelineResult> TryCloneToInventory(object source, ClonePipelineOptions options = null)
        {
            options = options ?? new ClonePipelineOptions();
            if (source == null) return RichResult<ClonePipelineResult>.Fail(ErrorCode.InvalidArgument, "source null");
            var diag = options.Diagnostics ? new Dictionary<string, object>() : null;

            object newItem = null; string used = null; string err = null;
            // 策略选择
            if (options.Strategy == CloneStrategy.TreeData || options.Strategy == CloneStrategy.Auto)
            {
                var r = DuckovTreeDataService.TryCloneFromSource(source);
                if (r.Ok && r.Value != null) { newItem = r.Value; used = "TreeData"; }
                else if (options.Strategy == CloneStrategy.TreeData) { err = r.Error ?? "TreeData clone failed"; }
            }
            if (newItem == null && (options.Strategy == CloneStrategy.Unity || options.Strategy == CloneStrategy.Auto))
            {
                var r = IMKDuckov.Factory.TryCloneItem(source);
                if (r.Ok && r.Value != null) { newItem = r.Value; used = "Unity"; }
                else if (options.Strategy == CloneStrategy.Unity) { err = r.Error ?? "Unity clone failed"; }
            }
            if (newItem == null) return RichResult<ClonePipelineResult>.Fail(ErrorCode.OperationFailed, err ?? "clone failed");

            // 变量合并策略（默认仅合并缺失键）与标签复制
            if (options.VariableMerge != VariableMergeMode.None)
            {
                try { IMKDuckov.VariableMerge.Merge(source, newItem, options.VariableMerge, acceptKey: options.AcceptVariableKey); } catch { }
            }
            if (options.CopyTags)
            {
                try { var tags = IMKDuckov.Item.GetTags(source) ?? Array.Empty<string>(); if (tags.Length > 0) IMKDuckov.Write.TryWriteTags(newItem, tags, merge: true); } catch { }
            }

            // 放置：解析目标背包并尝试放入
            object inv = IMKDuckov.InventoryResolver.Resolve(options.Target) ?? IMKDuckov.InventoryResolver.ResolveFallback();
            bool added = false; int index = -1; bool deferred = false;
            if (inv != null)
            {
                try
                {
                    var place = IMKDuckov.InventoryPlacement.TryPlace(inv, newItem, allowMerge: true, enableDeferredRetry: true);
                    added = place.added; index = place.index; deferred = place.deferredScheduled;
                }
                catch { }
                if (options.RefreshUI)
                {
                    IMKDuckov.UIRefresh.RefreshInventory(inv);
                }
            }

            if (options.Diagnostics && diag != null)
            {
                diag["strategy"] = used;
                diag["target"] = options.Target;
                diag["added"] = added; diag["index"] = index; diag["deferred"] = deferred;
                try { diag["newTid"] = IMKDuckov.Item.GetTypeId(newItem); } catch { }
                try { diag["newName"] = IMKDuckov.Item.GetDisplayNameRaw(newItem) ?? IMKDuckov.Item.GetName(newItem); } catch { }
            }
            var res = new ClonePipelineResult { NewItem = newItem, Added = added, Index = index, StrategyUsed = used, Diagnostics = diag };
            return RichResult<ClonePipelineResult>.Success(res);
        }

        // 旧的目标解析与 UI 刷新保留，但当前管线已改为由 InventoryResolver/UIRefresh 统一提供
        private static object ResolveTargetInventory(string target)
        {
            if (string.IsNullOrEmpty(target) || string.Equals(target, "character", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var tLM = DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.LevelManager") ?? DuckovTypeUtils.FindType("LevelManager");
                    var pInst = tLM?.GetProperty("Instance", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                    var lm = pInst?.GetValue(null, null);
                    var pMain = lm?.GetType().GetProperty("MainCharacter", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    var main = pMain?.GetValue(lm, null);
                    var pCharItem = main?.GetType().GetProperty("CharacterItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    var chItem = pCharItem?.GetValue(main, null);
                    var pInv = chItem?.GetType().GetProperty("Inventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                    var inv2 = pInv?.GetValue(chItem, null);
                    if (inv2 != null) return inv2;
                }
                catch { }
            }
            if (string.Equals(target, "storage", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var tPS = DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.PlayerStorage") ?? DuckovTypeUtils.FindType("PlayerStorage");
                    var pInv = tPS?.GetProperty("Inventory", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                    var inv = pInv?.GetValue(null, null);
                    if (inv != null) return inv;
                }
                catch { }
            }
            return null;
        }

        private static void TryScheduleNextFrame(Action a)
        {
            try
            {
                var go = new UnityEngine.GameObject("IMK_ClonePipelineDeferred");
                go.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
                go.AddComponent<DeferredInvoker>().Init(a);
            }
            catch { a?.Invoke(); }
        }
        private static void TryRefreshInventory(object inv)
        {
            try { var p = inv.GetType().GetProperty(EngineKeys.Property.NeedInspection, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); p?.SetValue(inv, true, null); } catch { }
            try { var m = inv.GetType().GetMethod(EngineKeys.Method.Refresh, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); m?.Invoke(inv, null); } catch { }
        }
        private sealed class DeferredInvoker : UnityEngine.MonoBehaviour
        {
            private Action _a; public void Init(Action a){ _a=a; }
            private System.Collections.IEnumerator Start(){ yield return null; try{ _a?.Invoke(); }catch{} try{ UnityEngine.Object.DestroyImmediate(gameObject); }catch{} }
        }
    }
}
