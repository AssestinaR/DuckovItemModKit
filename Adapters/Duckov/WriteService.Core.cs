using System;
using System.Collections.Generic;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（核心字段）：设置名称/TypeId/品质/显示品质/价值、订单、声音键、基础重量、图标等。
    /// 提供回滚保护与错误码返回，不直接抛异常。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        /// <summary>写入核心字段（失败会回滚到写入前的快照）。</summary>
        public RichResult TryWriteCoreFields(object item, CoreFieldChanges changes)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                if (changes == null) return RichResult.Fail(ErrorCode.InvalidArgument, "changes is null");
                var before = new CoreFields
                {
                    Name = _item.GetName(item),
                    RawName = _item.GetDisplayNameRaw(item),
                    TypeId = _item.GetTypeId(item),
                    Quality = _item.GetQuality(item),
                    DisplayQuality = _item.GetDisplayQuality(item),
                    Value = _item.GetValue(item)
                };
                try
                {
                    if (changes.Name != null) _item.SetName(item, changes.Name);
                    if (changes.RawName != null) _item.SetDisplayNameRaw(item, changes.RawName);
                    if (changes.TypeId.HasValue) _item.SetTypeId(item, changes.TypeId.Value);
                    if (changes.Quality.HasValue) _item.SetQuality(item, changes.Quality.Value);
                    if (changes.DisplayQuality.HasValue) _item.SetDisplayQuality(item, changes.DisplayQuality.Value);
                    if (changes.Value.HasValue) _item.SetValue(item, changes.Value.Value);
                }
                catch
                {
                    _item.SetName(item, before.Name);
                    _item.SetDisplayNameRaw(item, before.RawName);
                    _item.SetTypeId(item, before.TypeId);
                    _item.SetQuality(item, before.Quality);
                    _item.SetDisplayQuality(item, before.DisplayQuality);
                    _item.SetValue(item, before.Value);
                    throw;
                }
                PerfCounters.CoreWrites++;
                // Dirty 延迟到事务提交；非事务模式由调用方决定是否 Flush
                return RichResult.Success();
            }
            catch (Exception ex)
            {
                PerfCounters.CoreWriteFailures++;
                Log.Error("TryWriteCoreFields failed", ex);
                return RichResult.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        /// <summary>设置排序序号。</summary>
        public RichResult TrySetOrder(object item, int order)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var setter = DuckovReflectionCache.GetSetter(item.GetType(), "Order", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (setter == null) return RichResult.Fail(ErrorCode.NotSupported, "Order setter missing");
                setter(item, order);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetOrder failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>设置声音键。</summary>
        public RichResult TrySetSoundKey(object item, string soundKey)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var setter = DuckovReflectionCache.GetSetter(item.GetType(), "soundKey", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                             ?? DuckovReflectionCache.GetSetter(item.GetType(), "SoundKey", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (setter == null) return RichResult.Fail(ErrorCode.NotSupported, "soundKey setter missing");
                setter(item, soundKey);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetSoundKey failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>设置基础重量。</summary>
        public RichResult TrySetWeight(object item, float baseWeight)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var setter = DuckovReflectionCache.GetSetter(item.GetType(), "weight", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (setter == null) return RichResult.Fail(ErrorCode.NotSupported, "weight setter missing");
                setter(item, baseWeight);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetWeight failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>设置图标 Sprite。</summary>
        public RichResult TrySetIcon(object item, object sprite)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                var setter = DuckovReflectionCache.GetSetter(item.GetType(), "Icon", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (setter == null) return RichResult.Fail(ErrorCode.NotSupported, "Icon setter missing");
                setter(item, sprite);
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TrySetIcon failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
    }
}
