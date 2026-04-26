using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;
using UnityEngine;

namespace ItemModKit.Adapters.Duckov.Buffs
{
    /// <summary>
    /// buffs draft 服务：提供当前主角/宿主的 buff 查询与最小直接写入能力。
    /// buff 在 vanilla 中是 character-hosted runtime state，而不是 item-local effect graph 片段；
    /// 因此该服务当前仍停留在 draft surface，只承接 item-strongly-related runtime support，
    /// 不进入 IReadService / IWriteService 稳定面，也不扩张成完整 combat bridge。
    /// </summary>
    internal static class DuckovBuffDraftService
    {
        /// <summary>枚举当前运行时可用的 buff prefab 目录。</summary>
        public static RichResult<BuffCatalogDraft> EnumerateCatalog()
        {
            try
            {
                var result = new BuffCatalogDraft();
                foreach (var buff in EnumerateAllBuffPrefabs())
                {
                    var entry = CaptureCatalogEntry(buff);
                    if (entry != null)
                    {
                        result.Entries.Add(entry);
                    }
                }

                result.Entries = result.Entries
                    .OrderBy(e => e.Id)
                    .ThenBy(e => e.DisplayName ?? e.Name ?? string.Empty, StringComparer.Ordinal)
                    .ToList();
                return RichResult<BuffCatalogDraft>.Success(result);
            }
            catch (Exception ex)
            {
                Log.Error("EnumerateBuffCatalogDraft failed", ex);
                return RichResult<BuffCatalogDraft>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>读取指定宿主上下文当前激活的 buff 列表。</summary>
        public static RichResult<BuffSnapshotDraft[]> TryReadBuffs(object hostContext)
        {
            try
            {
                var manager = ResolveBuffManager(hostContext, out var error);
                if (manager == null)
                {
                    return RichResult<BuffSnapshotDraft[]>.Fail(ErrorCode.NotFound, error ?? "buff manager not found");
                }

                var list = new List<BuffSnapshotDraft>();
                foreach (var buff in EnumerateActiveBuffs(manager))
                {
                    var snapshot = CaptureActiveSnapshot(buff);
                    if (snapshot != null)
                    {
                        list.Add(snapshot);
                    }
                }

                return RichResult<BuffSnapshotDraft[]>.Success(list.ToArray());
            }
            catch (Exception ex)
            {
                Log.Error("TryReadBuffsDraft failed", ex);
                return RichResult<BuffSnapshotDraft[]>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>按 ID 查找当前激活 buff。</summary>
        public static RichResult<BuffSnapshotDraft> TryFindBuff(int buffId, object hostContext)
        {
            try
            {
                if (buffId <= 0) return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.InvalidArgument, "buffId must be > 0");
                var manager = ResolveBuffManager(hostContext, out var error);
                if (manager == null)
                {
                    return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.NotFound, error ?? "buff manager not found");
                }

                var buff = FindActiveBuff(manager, buffId);
                if (buff == null) return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.NotFound, "buff not active");
                return RichResult<BuffSnapshotDraft>.Success(CaptureActiveSnapshot(buff));
            }
            catch (Exception ex)
            {
                Log.Error("TryFindBuffDraft failed", ex);
                return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>按独占标签查找当前激活 buff。</summary>
        public static RichResult<BuffSnapshotDraft> TryFindBuffByExclusiveTag(string exclusiveTag, object hostContext)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(exclusiveTag)) return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.InvalidArgument, "exclusiveTag must not be empty");
                var manager = ResolveBuffManager(hostContext, out var error);
                if (manager == null)
                {
                    return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.NotFound, error ?? "buff manager not found");
                }

                var buff = FindActiveBuffByExclusiveTag(manager, exclusiveTag);
                if (buff == null) return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.NotFound, "buff with exclusive tag not active");
                return RichResult<BuffSnapshotDraft>.Success(CaptureActiveSnapshot(buff));
            }
            catch (Exception ex)
            {
                Log.Error("TryFindBuffByExclusiveTagDraft failed", ex);
                return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>按运行时类型全名查找当前激活 buff。</summary>
        public static RichResult<BuffSnapshotDraft> TryFindBuffByType(string typeFullName, object hostContext)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(typeFullName)) return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.InvalidArgument, "typeFullName must not be empty");
                var manager = ResolveBuffManager(hostContext, out var error);
                if (manager == null)
                {
                    return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.NotFound, error ?? "buff manager not found");
                }

                var buff = FindActiveBuffByType(manager, typeFullName);
                if (buff == null) return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.NotFound, "buff with requested type is not active");
                return RichResult<BuffSnapshotDraft>.Success(CaptureActiveSnapshot(buff));
            }
            catch (Exception ex)
            {
                Log.Error("TryFindBuffByTypeDraft failed", ex);
                return RichResult<BuffSnapshotDraft>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>判断指定 buff 是否处于激活状态。</summary>
        public static RichResult<bool> TryHasBuff(int buffId, object hostContext)
        {
            try
            {
                if (buffId <= 0) return RichResult<bool>.Fail(ErrorCode.InvalidArgument, "buffId must be > 0");
                var manager = ResolveBuffManager(hostContext, out var error);
                if (manager == null)
                {
                    return RichResult<bool>.Fail(ErrorCode.NotFound, error ?? "buff manager not found");
                }

                var hasMethod = DuckovReflectionCache.GetMethod(manager.GetType(), "HasBuff", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(int) });
                if (hasMethod != null)
                {
                    try
                    {
                        return RichResult<bool>.Success(DuckovTypeUtils.ConvertToBool(hasMethod.Invoke(manager, new object[] { buffId })));
                    }
                    catch
                    {
                    }
                }

                return RichResult<bool>.Success(FindActiveBuff(manager, buffId) != null);
            }
            catch (Exception ex)
            {
                Log.Error("TryHasBuffDraft failed", ex);
                return RichResult<bool>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>向目标角色添加一个 buff。</summary>
        public static RichResult TryAddBuff(int buffId, object hostContext, int overrideWeaponId)
        {
            try
            {
                if (buffId <= 0) return RichResult.Fail(ErrorCode.InvalidArgument, "buffId must be > 0");
                var character = ResolveCharacterMain(hostContext, out var error);
                if (character == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "character not found");

                var prefab = FindBuffPrefab(buffId);
                if (prefab == null) return RichResult.Fail(ErrorCode.NotFound, "buff prefab not found");

                var before = ReadActiveBuffCount(ResolveBuffManager(character, out _));
                var addMethod = DuckovReflectionCache.GetMethod(character.GetType(), "AddBuff", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { prefab.GetType(), character.GetType(), typeof(int) })
                    ?? character.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(m => string.Equals(m.Name, "AddBuff", StringComparison.Ordinal) && m.GetParameters().Length >= 2);
                if (addMethod == null) return RichResult.Fail(ErrorCode.NotSupported, "CharacterMainControl.AddBuff not found");

                InvokeAddBuff(addMethod, character, prefab, character, overrideWeaponId);

                var manager = ResolveBuffManager(character, out _);
                var afterBuff = FindActiveBuff(manager, buffId);
                if (afterBuff == null)
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "buff add did not produce an active buff");
                }

                var after = ReadActiveBuffCount(manager);
                if (after <= 0 && before <= 0)
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "buff manager did not report active buffs after add");
                }

                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryAddBuffDraft failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>移除一个激活 buff；removeOneLayer=true 时走原版减层语义。</summary>
        public static RichResult TryRemoveBuff(int buffId, bool removeOneLayer, object hostContext)
        {
            try
            {
                if (buffId <= 0) return RichResult.Fail(ErrorCode.InvalidArgument, "buffId must be > 0");
                var character = ResolveCharacterMain(hostContext, out var error);
                if (character == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "character not found");
                var manager = ResolveBuffManager(character, out error);
                if (manager == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "buff manager not found");

                var beforeBuff = FindActiveBuff(manager, buffId);
                if (beforeBuff == null) return RichResult.Fail(ErrorCode.NotFound, "buff not active");
                var beforeLayers = ReadInt(beforeBuff, "CurrentLayers");

                var removeMethod = DuckovReflectionCache.GetMethod(character.GetType(), "RemoveBuff", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(int), typeof(bool) });
                if (removeMethod == null) return RichResult.Fail(ErrorCode.NotSupported, "CharacterMainControl.RemoveBuff not found");
                removeMethod.Invoke(character, new object[] { buffId, removeOneLayer });

                var afterBuff = FindActiveBuff(manager, buffId);
                if (!removeOneLayer && afterBuff != null)
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "buff still active after remove");
                }
                if (removeOneLayer && afterBuff != null && ReadInt(afterBuff, "CurrentLayers") >= beforeLayers)
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "buff layer did not decrease");
                }

                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryRemoveBuffDraft failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>直接设置当前激活 buff 的层数；0 表示移除该 buff。</summary>
        public static RichResult TrySetBuffLayers(int buffId, int layers, object hostContext)
        {
            try
            {
                if (buffId <= 0) return RichResult.Fail(ErrorCode.InvalidArgument, "buffId must be > 0");
                if (layers < 0) return RichResult.Fail(ErrorCode.InvalidArgument, "layers must be >= 0");

                var character = ResolveCharacterMain(hostContext, out var error);
                if (character == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "character not found");
                var manager = ResolveBuffManager(character, out error);
                if (manager == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "buff manager not found");

                var buff = FindActiveBuff(manager, buffId);
                if (buff == null) return RichResult.Fail(ErrorCode.NotFound, "buff not active");

                if (layers == 0)
                {
                    return TryRemoveBuff(buffId, removeOneLayer: false, hostContext: character);
                }

                var maxLayers = Math.Max(1, ReadInt(buff, "MaxLayers"));
                var clamped = Math.Min(layers, maxLayers);
                if (!DuckovTypeUtils.TrySetMember(buff, new[] { "CurrentLayers", "currentLayers" }, clamped))
                {
                    return RichResult.Fail(ErrorCode.NotSupported, "CurrentLayers setter not found");
                }

                var actual = ReadInt(buff, "CurrentLayers");
                if (actual != clamped)
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "buff layer write mismatch");
                }

                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TrySetBuffLayersDraft failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>对目标 buff 增加若干层；缺失时可选自动添加。</summary>
        public static RichResult TryAddBuffLayers(int buffId, int layerDelta, bool addIfMissing, object hostContext, int overrideWeaponId)
        {
            try
            {
                if (buffId <= 0) return RichResult.Fail(ErrorCode.InvalidArgument, "buffId must be > 0");
                if (layerDelta <= 0) return RichResult.Fail(ErrorCode.InvalidArgument, "layerDelta must be > 0");

                var character = ResolveCharacterMain(hostContext, out var error);
                if (character == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "character not found");
                var manager = ResolveBuffManager(character, out error);
                if (manager == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "buff manager not found");

                var buff = FindActiveBuff(manager, buffId);
                if (buff == null)
                {
                    if (!addIfMissing)
                    {
                        return RichResult.Fail(ErrorCode.NotFound, "buff not active");
                    }

                    var add = TryAddBuff(buffId, character, overrideWeaponId);
                    if (!add.Ok)
                    {
                        return add;
                    }

                    buff = FindActiveBuff(manager, buffId);
                    if (buff == null)
                    {
                        return RichResult.Fail(ErrorCode.OperationFailed, "buff add did not produce an active buff");
                    }
                }

                var maxLayers = Math.Max(1, ReadInt(buff, "MaxLayers", "maxLayers"));
                var currentLayers = Math.Max(1, ReadInt(buff, "CurrentLayers", "currentLayers"));
                var desiredLayers = Math.Min(maxLayers, currentLayers + layerDelta);
                return TrySetBuffLayers(buffId, desiredLayers, character);
            }
            catch (Exception ex)
            {
                Log.Error("TryAddBuffLayersDraft failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>对目标 buff 减少若干层；减到 0 时移除。</summary>
        public static RichResult TryRemoveBuffLayers(int buffId, int layerDelta, object hostContext)
        {
            try
            {
                if (buffId <= 0) return RichResult.Fail(ErrorCode.InvalidArgument, "buffId must be > 0");
                if (layerDelta <= 0) return RichResult.Fail(ErrorCode.InvalidArgument, "layerDelta must be > 0");

                var character = ResolveCharacterMain(hostContext, out var error);
                if (character == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "character not found");
                var manager = ResolveBuffManager(character, out error);
                if (manager == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "buff manager not found");

                var buff = FindActiveBuff(manager, buffId);
                if (buff == null) return RichResult.Fail(ErrorCode.NotFound, "buff not active");

                var currentLayers = Math.Max(1, ReadInt(buff, "CurrentLayers", "currentLayers"));
                var desiredLayers = currentLayers - layerDelta;
                if (desiredLayers <= 0)
                {
                    return TryRemoveBuff(buffId, false, character);
                }

                return TrySetBuffLayers(buffId, desiredLayers, character);
            }
            catch (Exception ex)
            {
                Log.Error("TryRemoveBuffLayersDraft failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>按独占标签移除激活 buff；removeOneLayer=true 时走原版减层语义。</summary>
        public static RichResult TryRemoveBuffsByExclusiveTag(string exclusiveTag, bool removeOneLayer, object hostContext)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(exclusiveTag)) return RichResult.Fail(ErrorCode.InvalidArgument, "exclusiveTag must not be empty");

                var character = ResolveCharacterMain(hostContext, out var error);
                if (character == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "character not found");
                var manager = ResolveBuffManager(character, out error);
                if (manager == null) return RichResult.Fail(ErrorCode.NotFound, error ?? "buff manager not found");

                var matching = EnumerateActiveBuffs(manager)
                    .Where(buff => HasExclusiveTag(buff, exclusiveTag))
                    .ToArray();
                if (matching.Length == 0)
                {
                    return RichResult.Fail(ErrorCode.NotFound, "buff with exclusive tag not active");
                }

                var removeByTagMethod = DuckovReflectionCache.GetMethod(character.GetType(), "RemoveBuffsByTag", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { matching[0].GetType().GetProperty("ExclusiveTag")?.PropertyType ?? typeof(object), typeof(bool) });
                if (removeByTagMethod != null)
                {
                    var tagValue = DuckovTypeUtils.GetMaybe(matching[0], new[] { "ExclusiveTag", "exclusiveTag" });
                    removeByTagMethod.Invoke(character, new[] { tagValue, (object)removeOneLayer });
                }
                else
                {
                    foreach (var buff in matching)
                    {
                        var remove = TryRemoveBuff(ReadInt(buff, "ID", "id"), removeOneLayer, character);
                        if (!remove.Ok)
                        {
                            return remove;
                        }
                    }
                }

                if (EnumerateActiveBuffs(manager).Any(buff => HasExclusiveTag(buff, exclusiveTag)))
                {
                    return RichResult.Fail(ErrorCode.OperationFailed, "buffs with exclusive tag still active after remove");
                }

                return RichResult.Success();
            }
            catch (Exception ex)
            {
                Log.Error("TryRemoveBuffsByExclusiveTagDraft failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        private static void InvokeAddBuff(MethodInfo method, object character, object prefab, object fromWho, int overrideWeaponId)
        {
            var parameters = method.GetParameters();
            if (parameters.Length >= 3)
            {
                method.Invoke(character, new[] { prefab, fromWho, (object)overrideWeaponId });
                return;
            }

            if (parameters.Length == 2)
            {
                method.Invoke(character, new[] { prefab, fromWho });
                return;
            }

            method.Invoke(character, new[] { prefab });
        }

        private static BuffCatalogEntryDraft CaptureCatalogEntry(object buff)
        {
            if (!IsAlive(buff)) return null;
            return new BuffCatalogEntryDraft
            {
                Id = ReadInt(buff, "ID", "id"),
                TypeFullName = buff.GetType().FullName,
                Name = ReadUnityName(buff),
                DisplayNameKey = ReadString(buff, "DisplayNameKey", "displayName"),
                DisplayName = ReadString(buff, "DisplayName"),
                Description = ReadString(buff, "Description", "description"),
                Hide = ReadBool(buff, "Hide", "hide"),
                LimitedLifeTime = ReadBool(buff, "LimitedLifeTime", "limitedLifeTime"),
                TotalLifeTime = ReadFloat(buff, "TotalLifeTime", "totalLifeTime"),
                MaxLayers = Math.Max(1, ReadInt(buff, "MaxLayers", "maxLayers")),
                ExclusiveTag = ReadEnumString(buff, "ExclusiveTag", "exclusiveTag"),
                ExclusiveTagPriority = ReadInt(buff, "ExclusiveTagPriority", "exclusiveTagPriority"),
                EffectTypes = ReadEffectTypes(buff)
            };
        }

        private static BuffSnapshotDraft CaptureActiveSnapshot(object buff)
        {
            if (!IsAlive(buff)) return null;
            return new BuffSnapshotDraft
            {
                Id = ReadInt(buff, "ID", "id"),
                TypeFullName = buff.GetType().FullName,
                Name = ReadUnityName(buff),
                DisplayNameKey = ReadString(buff, "DisplayNameKey", "displayName"),
                DisplayName = ReadString(buff, "DisplayName"),
                Description = ReadString(buff, "Description", "description"),
                Hide = ReadBool(buff, "Hide", "hide"),
                LimitedLifeTime = ReadBool(buff, "LimitedLifeTime", "limitedLifeTime"),
                TotalLifeTime = ReadFloat(buff, "TotalLifeTime", "totalLifeTime"),
                CurrentLifeTime = ReadFloat(buff, "CurrentLifeTime"),
                RemainingTime = ReadFloat(buff, "RemainingTime"),
                CurrentLayers = ReadInt(buff, "CurrentLayers", "currentLayers"),
                MaxLayers = Math.Max(1, ReadInt(buff, "MaxLayers", "maxLayers")),
                IsOutOfTime = ReadBool(buff, "IsOutOfTime"),
                ExclusiveTag = ReadEnumString(buff, "ExclusiveTag", "exclusiveTag"),
                ExclusiveTagPriority = ReadInt(buff, "ExclusiveTagPriority", "exclusiveTagPriority"),
                FromCharacterName = ReadFromCharacterName(buff),
                FromWeaponId = ReadInt(buff, "fromWeaponID", "FromWeaponID"),
                EffectTypes = ReadEffectTypes(buff)
            };
        }

        private static IEnumerable<object> EnumerateAllBuffPrefabs()
        {
            var seenIds = new HashSet<int>();
            var buffsData = GetBuffsData();
            if (buffsData == null) yield break;

            var allBuffs = DuckovReflectionCache.GetField(buffsData.GetType(), "allBuffs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(buffsData) as IEnumerable;
            if (allBuffs != null)
            {
                foreach (var buff in allBuffs)
                {
                    if (!IsAlive(buff)) continue;
                    var id = ReadInt(buff, "ID", "id");
                    if (id > 0 && !seenIds.Add(id)) continue;
                    yield return buff;
                }
            }

            foreach (var property in buffsData.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length != 0) continue;
                object buff = null;
                try { buff = property.GetValue(buffsData, null); } catch { }
                if (!IsAlive(buff)) continue;
                var id = ReadInt(buff, "ID", "id");
                if (id > 0 && !seenIds.Add(id)) continue;
                yield return buff;
            }
        }

        private static IEnumerable<object> EnumerateActiveBuffs(object manager)
        {
            if (!IsAlive(manager)) yield break;
            var buffs = DuckovTypeUtils.GetMaybe(manager, new[] { "Buffs", "buffs" }) as IEnumerable;
            if (buffs == null) yield break;
            foreach (var buff in buffs)
            {
                if (IsAlive(buff)) yield return buff;
            }
        }

        private static object FindBuffPrefab(int buffId)
        {
            return EnumerateAllBuffPrefabs().FirstOrDefault(buff => ReadInt(buff, "ID", "id") == buffId);
        }

        private static object FindActiveBuff(object manager, int buffId)
        {
            return EnumerateActiveBuffs(manager).FirstOrDefault(buff => ReadInt(buff, "ID", "id") == buffId);
        }

        private static object FindActiveBuffByExclusiveTag(object manager, string exclusiveTag)
        {
            return EnumerateActiveBuffs(manager).FirstOrDefault(buff => HasExclusiveTag(buff, exclusiveTag));
        }

        private static object FindActiveBuffByType(object manager, string typeFullName)
        {
            return EnumerateActiveBuffs(manager).FirstOrDefault(buff =>
                string.Equals(buff?.GetType().FullName ?? buff?.GetType().Name, typeFullName, StringComparison.Ordinal)
                || string.Equals(buff?.GetType().Name, typeFullName, StringComparison.Ordinal));
        }

        private static object ResolveCharacterMain(object hostContext, out string error)
        {
            error = null;
            var main = GetMainCharacter();
            if (!IsAlive(hostContext)) return main;

            if (IsCharacterMain(hostContext)) return hostContext;

            var manager = TryGetBuffManager(hostContext);
            if (manager != null)
            {
                var master = DuckovTypeUtils.GetMaybe(manager, new[] { "Master", "master" });
                if (IsAlive(master)) return master;
            }

            var ownerCharacter = DuckovTypeUtils.GetMaybe(hostContext, new[] { "Character" });
            if (IsCharacterMain(ownerCharacter)) return ownerCharacter;

            if (main == null)
            {
                error = "main character not found";
                return null;
            }

            var charItem = DuckovTypeUtils.GetMaybe(main, new[] { EngineKeys.Property.CharacterItem, "characterItem" });
            if (ReferenceEquals(hostContext, charItem)) return main;

            if (BelongsToCharacterItem(hostContext, charItem)) return main;

            error = "host context does not resolve to the main character";
            return null;
        }

        private static object ResolveBuffManager(object hostContext, out string error)
        {
            error = null;
            if (IsBuffManager(hostContext)) return hostContext;

            var character = ResolveCharacterMain(hostContext, out error);
            if (character == null) return null;

            var manager = TryGetBuffManager(character);
            if (manager == null)
            {
                error = "character buff manager not found";
                return null;
            }

            return manager;
        }

        private static bool BelongsToCharacterItem(object hostContext, object charItem)
        {
            if (!IsAlive(hostContext) || !IsAlive(charItem)) return false;
            if (ReferenceEquals(hostContext, charItem)) return true;

            var current = hostContext;
            for (var depth = 0; depth < 16 && IsAlive(current); depth++)
            {
                if (ReferenceEquals(current, charItem)) return true;
                var parent = DuckovTypeUtils.GetMaybe(current, new[] { "ParentItem" });
                if (!IsAlive(parent)) break;
                current = parent;
            }

            var currentInventory = DuckovTypeUtils.GetMaybe(hostContext, new[] { "InInventory", "Inventory" });
            var charInventory = DuckovTypeUtils.GetMaybe(charItem, new[] { "Inventory" });
            return IsAlive(currentInventory) && ReferenceEquals(currentInventory, charInventory);
        }

        private static object TryGetBuffManager(object character)
        {
            if (!IsAlive(character)) return null;
            if (IsBuffManager(character)) return character;

            var getMethod = DuckovReflectionCache.GetMethod(character.GetType(), "GetBuffManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes);
            if (getMethod != null)
            {
                try
                {
                    var manager = getMethod.Invoke(character, null);
                    if (IsBuffManager(manager)) return manager;
                }
                catch { }
            }

            var direct = DuckovTypeUtils.GetMaybe(character, new[] { "buffManager", "BuffManager" });
            return IsBuffManager(direct) ? direct : null;
        }

        private static object GetMainCharacter()
        {
            try
            {
                var type = DuckovTypeUtils.FindType("CharacterMainControl") ?? DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.CharacterMainControl");
                return type?.GetProperty("Main", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
            }
            catch { return null; }
        }

        private static object GetBuffsData()
        {
            try
            {
                var type = DuckovTypeUtils.FindType("Duckov.Utilities.GameplayDataSettings") ?? DuckovTypeUtils.FindType("GameplayDataSettings") ?? DuckovTypeUtils.FindType("TeamSoda.Duckov.Core.Duckov.Utilities.GameplayDataSettings");
                return type?.GetProperty("Buffs", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
            }
            catch { return null; }
        }

        private static int ReadActiveBuffCount(object manager)
        {
            return EnumerateActiveBuffs(manager).Count();
        }

        private static bool IsCharacterMain(object instance)
        {
            if (!IsAlive(instance)) return false;
            var type = instance.GetType();
            return string.Equals(type.Name, "CharacterMainControl", StringComparison.Ordinal)
                || string.Equals(type.FullName, "TeamSoda.Duckov.Core.CharacterMainControl", StringComparison.Ordinal);
        }

        private static bool IsBuffManager(object instance)
        {
            if (!IsAlive(instance)) return false;
            var type = instance.GetType();
            return string.Equals(type.Name, "CharacterBuffManager", StringComparison.Ordinal)
                || string.Equals(type.FullName, "Duckov.Buffs.CharacterBuffManager", StringComparison.Ordinal)
                || string.Equals(type.FullName, "TeamSoda.Duckov.Core.Duckov.Buffs.CharacterBuffManager", StringComparison.Ordinal);
        }

        private static bool IsAlive(object instance)
        {
            if (instance == null) return false;
            if (instance is UnityEngine.Object unityObject) return unityObject != null;
            return true;
        }

        private static string[] ReadEffectTypes(object buff)
        {
            var effects = DuckovTypeUtils.GetMaybe(buff, new[] { "effects", "Effects" }) as IEnumerable;
            if (effects == null) return Array.Empty<string>();

            var list = new List<string>();
            foreach (var effect in effects)
            {
                if (!IsAlive(effect)) continue;
                list.Add(effect.GetType().FullName ?? effect.GetType().Name);
            }
            return list.ToArray();
        }

        private static string ReadUnityName(object instance)
        {
            try
            {
                if (instance is UnityEngine.Object unityObject) return unityObject.name;
            }
            catch { }
            return ReadString(instance, "name", "Name");
        }

        private static string ReadFromCharacterName(object buff)
        {
            var from = DuckovTypeUtils.GetMaybe(buff, new[] { "fromWho", "FromWho" });
            if (!IsAlive(from)) return null;

            var name = DuckovTypeUtils.GetMaybe(from, new[] { "name", "Name", "DisplayName" });
            return Convert.ToString(name);
        }

        private static string ReadString(object instance, params string[] names)
        {
            try { return Convert.ToString(DuckovTypeUtils.GetMaybe(instance, names)); } catch { return null; }
        }

        private static string ReadEnumString(object instance, params string[] names)
        {
            try
            {
                var value = DuckovTypeUtils.GetMaybe(instance, names);
                return value?.ToString();
            }
            catch { return null; }
        }

        private static int ReadInt(object instance, params string[] names)
        {
            try
            {
                var value = DuckovTypeUtils.GetMaybe(instance, names);
                return value == null ? 0 : Convert.ToInt32(value);
            }
            catch { return 0; }
        }

        private static float ReadFloat(object instance, params string[] names)
        {
            try { return DuckovTypeUtils.ConvertToFloat(DuckovTypeUtils.GetMaybe(instance, names)); } catch { return 0f; }
        }

        private static bool ReadBool(object instance, params string[] names)
        {
            try { return DuckovTypeUtils.ConvertToBool(DuckovTypeUtils.GetMaybe(instance, names)); } catch { return false; }
        }

        private static bool HasExclusiveTag(object buff, string exclusiveTag)
        {
            if (!IsAlive(buff) || string.IsNullOrWhiteSpace(exclusiveTag)) return false;
            var actual = ReadEnumString(buff, "ExclusiveTag", "exclusiveTag");
            return string.Equals(actual, exclusiveTag, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ReadNormalizedTag(actual), ReadNormalizedTag(exclusiveTag), StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadNormalizedTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var lastDot = value.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < value.Length ? value.Substring(lastDot + 1) : value;
        }
    }
}