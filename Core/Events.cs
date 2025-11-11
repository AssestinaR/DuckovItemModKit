using System;

namespace ItemModKit.Core
{
 /// <summary>事件来源类型。</summary>
 public enum ItemEventSourceType { Backpack, Storage, Slot, World, Other }
 /// <summary>事件原因分类。</summary>
 public enum ItemEventCause { Craft, Loot, Move, Merge, Split, Buy, Sell, Equip, Unequip, Other }

 /// <summary>
 /// 事件上下文：用于事件发布与诊断。
 /// </summary>
 public sealed class ItemEventContext
 {
 public ItemEventSourceType Source { get; set; }
 public ItemEventCause Cause { get; set; }
 public int? Index { get; set; }
 public string OwnerId { get; set; }
 public float Timestamp { get; set; } // unscaled time seconds
 }
}
