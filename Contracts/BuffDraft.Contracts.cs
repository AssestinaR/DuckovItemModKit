using System;
using System.Collections.Generic;

namespace ItemModKit.Core
{
    /// <summary>
    /// buffs draft 的单个目录项。
    /// 用于运行时枚举当前可用 buff prefab，而不是场景中的激活 buff。
    /// </summary>
    [Serializable]
    public sealed class BuffCatalogEntryDraft
    {
        /// <summary>buff ID。</summary>
        public int Id { get; set; }

        /// <summary>buff 运行时类型全名。</summary>
        public string TypeFullName { get; set; }

        /// <summary>Unity 对象名称。</summary>
        public string Name { get; set; }

        /// <summary>显示名本地化键。</summary>
        public string DisplayNameKey { get; set; }

        /// <summary>当前语言显示名。</summary>
        public string DisplayName { get; set; }

        /// <summary>当前语言描述文本。</summary>
        public string Description { get; set; }

        /// <summary>是否隐藏到默认 buff UI。</summary>
        public bool Hide { get; set; }

        /// <summary>是否限制生命周期。</summary>
        public bool LimitedLifeTime { get; set; }

        /// <summary>总生命周期秒数。</summary>
        public float TotalLifeTime { get; set; }

        /// <summary>最大层数。</summary>
        public int MaxLayers { get; set; }

        /// <summary>独占标签名。</summary>
        public string ExclusiveTag { get; set; }

        /// <summary>独占标签优先级。</summary>
        public int ExclusiveTagPriority { get; set; }

        /// <summary>内部 effect 类型名集合。</summary>
        public string[] EffectTypes { get; set; }
    }

    /// <summary>
    /// buffs draft 的总目录。
    /// </summary>
    [Serializable]
    public sealed class BuffCatalogDraft
    {
        /// <summary>当前可枚举到的全部 buff prefab。</summary>
        public List<BuffCatalogEntryDraft> Entries { get; set; } = new List<BuffCatalogEntryDraft>();
    }

    /// <summary>
    /// 单个激活 buff 的运行时快照。
    /// </summary>
    [Serializable]
    public sealed class BuffSnapshotDraft
    {
        /// <summary>buff ID。</summary>
        public int Id { get; set; }

        /// <summary>buff 运行时类型全名。</summary>
        public string TypeFullName { get; set; }

        /// <summary>Unity 对象名称。</summary>
        public string Name { get; set; }

        /// <summary>显示名本地化键。</summary>
        public string DisplayNameKey { get; set; }

        /// <summary>当前语言显示名。</summary>
        public string DisplayName { get; set; }

        /// <summary>当前语言描述文本。</summary>
        public string Description { get; set; }

        /// <summary>是否隐藏到默认 buff UI。</summary>
        public bool Hide { get; set; }

        /// <summary>是否限制生命周期。</summary>
        public bool LimitedLifeTime { get; set; }

        /// <summary>总生命周期秒数。</summary>
        public float TotalLifeTime { get; set; }

        /// <summary>当前已运行秒数。</summary>
        public float CurrentLifeTime { get; set; }

        /// <summary>剩余秒数；不限时 buff 为 PositiveInfinity。</summary>
        public float RemainingTime { get; set; }

        /// <summary>当前层数。</summary>
        public int CurrentLayers { get; set; }

        /// <summary>最大层数。</summary>
        public int MaxLayers { get; set; }

        /// <summary>是否已经超时。</summary>
        public bool IsOutOfTime { get; set; }

        /// <summary>独占标签名。</summary>
        public string ExclusiveTag { get; set; }

        /// <summary>独占标签优先级。</summary>
        public int ExclusiveTagPriority { get; set; }

        /// <summary>来源角色名；无来源时可为空。</summary>
        public string FromCharacterName { get; set; }

        /// <summary>来源武器 ID。</summary>
        public int FromWeaponId { get; set; }

        /// <summary>内部 effect 类型名集合。</summary>
        public string[] EffectTypes { get; set; }
    }
}