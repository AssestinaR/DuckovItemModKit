namespace ItemModKit.Core
{
    /// <summary>
    /// 引擎关键名集中定义，避免魔法字符串与拼写错误。
    /// </summary>
    public static class EngineKeys
    {
        /// <summary>变量键常量。</summary>
        public static class Variable
        {
            /// <summary>堆叠数量。</summary>
            public const string Count = "Count";
            /// <summary>当前耐久。</summary>
            public const string Durability = "Durability";
            /// <summary>耐久损耗。</summary>
            public const string DurabilityLoss = "DurabilityLoss";
            /// <summary>是否已检查。</summary>
            public const string Inspected = "Inspected";
        }

        /// <summary>常量键常量。</summary>
        public static class Constant
        {
            /// <summary>最大耐久。</summary>
            public const string MaxDurability = "MaxDurability";
        }

        /// <summary>属性名常量。</summary>
        public static class Property
        {
            /// <summary>最大堆叠数。</summary>
            public const string MaxStackCount = "MaxStackCount";
            /// <summary>是否处于检查中。</summary>
            public const string Inspecting = "Inspecting";
            /// <summary>是否需要检查。</summary>
            public const string NeedInspection = "NeedInspection";
            /// <summary>是否已检查。</summary>
            public const string Inspected = "Inspected";
            /// <summary>主控实例。</summary>
            public const string Main = "Main";
            /// <summary>角色的物品组件。</summary>
            public const string CharacterItem = "CharacterItem";
            /// <summary>背包属性名。</summary>
            public const string Inventory = "Inventory";
        }

        /// <summary>方法名常量。</summary>
        public static class Method
        {
            /// <summary>刷新方法名。</summary>
            public const string Refresh = "Refresh";
            /// <summary>发送到玩家的方法名。</summary>
            public const string SendToPlayer = "SendToPlayer";
        }
    }
}
