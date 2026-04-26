using System;
using System.Collections.Generic;
using ItemModKit.Core;
using Newtonsoft.Json;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（槽位/持久化支持）：
    /// 负责槽位草案读写、标准化，以及槽位来源的持久化判定。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        /// <summary>
        /// 基于持久化草案判断槽位的来源提示。
        /// 动态槽位会返回 Dynamic，其余存在草案的键默认按 Builtin 处理。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="slotKey">目标槽位键。</param>
        /// <returns>槽位的持久化来源提示；无法判断时返回 Unknown。</returns>
        private static SlotPersistenceOriginHint TryGetPersistedSlotOrigin(object ownerItem, string slotKey)
        {
            if (ownerItem == null || string.IsNullOrWhiteSpace(slotKey))
            {
                return SlotPersistenceOriginHint.Unknown;
            }

            try
            {
                var payload = ReadSlotPersistenceDraftData(ownerItem);
                if (payload == null)
                {
                    return SlotPersistenceOriginHint.Unknown;
                }

                if (payload.Slots.Exists(entry => entry != null && string.Equals(entry.Key, slotKey, StringComparison.OrdinalIgnoreCase)))
                {
                    return SlotPersistenceOriginHint.Dynamic;
                }

                return SlotPersistenceOriginHint.Builtin;
            }
            catch
            {
                return SlotPersistenceOriginHint.Unknown;
            }
        }

        /// <summary>
        /// 从宿主物品变量中读取槽位持久化草案。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <returns>成功解析则返回草案对象；否则返回 null。</returns>
        private static SlotPersistenceDraftData ReadSlotPersistenceDraftData(object ownerItem)
        {
            try
            {
                var raw = IMKDuckov.Item.GetVariable(ownerItem, DuckovSlotProvisioningDraft.DefaultPersistenceVariableKey) as string;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<SlotPersistenceDraftData>(raw);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 写回槽位持久化草案。
        /// 当草案为空时，会直接移除对应变量键，而不是保留一个空 JSON。
        /// </summary>
        /// <param name="ownerItem">槽位宿主物品。</param>
        /// <param name="payload">待写入的草案对象。</param>
        /// <returns>写入或删除成功时返回 true；否则返回 false。</returns>
        private static bool WriteSlotPersistenceDraftData(object ownerItem, SlotPersistenceDraftData payload)
        {
            try
            {
                NormalizeSlotPersistenceDraft(payload);
                if (payload == null || (payload.Slots.Count == 0 && payload.RemovedBuiltinSlotKeys.Count == 0 && payload.Mutations.Count == 0))
                {
                    IMKDuckov.Item.RemoveVariable(ownerItem, DuckovSlotProvisioningDraft.DefaultPersistenceVariableKey);
                    return true;
                }

                payload.SchemaVersion = SlotPersistenceDraftSchema.CurrentVersion;
                var json = JsonConvert.SerializeObject(payload, Formatting.None);
                var write = IMKDuckov.Write.TryWriteVariables(ownerItem, new[]
                {
                    new KeyValuePair<string, object>(DuckovSlotProvisioningDraft.DefaultPersistenceVariableKey, json)
                }, overwrite: true);
                return write.Ok;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 标准化槽位持久化草案，确保集合已初始化且内容已去重、去空。
        /// </summary>
        /// <param name="payload">待标准化的草案对象。</param>
        private static void NormalizeSlotPersistenceDraft(SlotPersistenceDraftData payload)
        {
            if (payload == null)
            {
                return;
            }

            payload.Slots ??= new List<SlotPersistenceSlotDefinition>();
            payload.RemovedBuiltinSlotKeys ??= new List<string>();
            payload.Mutations ??= new List<SlotPersistenceSlotMutation>();

            payload.Slots.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.Key));
            payload.Mutations.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Kind == SlotPersistenceMutationKind.None);
            payload.RemovedBuiltinSlotKeys.RemoveAll(string.IsNullOrWhiteSpace);
            payload.RemovedBuiltinSlotKeys = new List<string>(new HashSet<string>(payload.RemovedBuiltinSlotKeys, StringComparer.OrdinalIgnoreCase));
        }
    }
}