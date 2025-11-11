using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// 写入服务（变量/常量/标签）：支持批量写入、回滚保护以及合并策略。
    /// </summary>
    internal sealed partial class WriteService : IWriteService
    {
        /// <summary>写变量集合（overwrite=false 时仅写缺失键）。</summary>
        public RichResult TryWriteVariables(object item, IEnumerable<KeyValuePair<string, object>> entries, bool overwrite)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                if (entries == null) return RichResult.Fail(ErrorCode.InvalidArgument, "entries is null");
                var snapshot = new System.Collections.Generic.List<(string key, object value, bool existed)>();
                try
                {
                    foreach (var kv in entries)
                    {
                        if (string.IsNullOrEmpty(kv.Key)) continue;
                        var existedVal = _item.GetVariable(item, kv.Key);
                        bool existed = existedVal != null;
                        snapshot.Add((kv.Key, existedVal, existed));
                        if (!overwrite && existed) continue;
                        _item.SetVariable(item, kv.Key, kv.Value, false);
                    }
                }
                catch
                {
                    foreach (var s in snapshot)
                    {
                        if (s.existed) _item.SetVariable(item, s.key, s.value, false);
                        else _item.RemoveVariable(item, s.key);
                    }
                    throw;
                }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryWriteVariables failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>写常量集合（createIfMissing 控制是否创建缺失常量）。</summary>
        public RichResult TryWriteConstants(object item, IEnumerable<KeyValuePair<string, object>> entries, bool createIfMissing)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                if (entries == null) return RichResult.Fail(ErrorCode.InvalidArgument, "entries is null");
                var snapshot = new System.Collections.Generic.List<(string key, object value, bool existed)>();
                try
                {
                    foreach (var kv in entries)
                    {
                        if (string.IsNullOrEmpty(kv.Key)) continue;
                        var existedVal = _item.GetConstant(item, kv.Key);
                        bool existed = existedVal != null;
                        snapshot.Add((kv.Key, existedVal, existed));
                        _item.SetConstant(item, kv.Key, kv.Value, createIfMissing);
                    }
                }
                catch
                {
                    foreach (var s in snapshot)
                    {
                        if (s.existed) _item.SetConstant(item, s.key, s.value, true);
                        else _item.RemoveConstant(item, s.key);
                    }
                    throw;
                }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryWriteConstants failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
        /// <summary>写标签集合（merge=true 时并集覆盖）。</summary>
        public RichResult TryWriteTags(object item, IEnumerable<string> tags, bool merge)
        {
            try
            {
                if (item == null) return RichResult.Fail(ErrorCode.InvalidArgument, "item is null");
                if (tags == null) return RichResult.Fail(ErrorCode.InvalidArgument, "tags is null");
                var existing = _item.GetTags(item) ?? Array.Empty<string>();
                try
                {
                    if (merge)
                    {
                        var set = new System.Collections.Generic.HashSet<string>(existing, StringComparer.Ordinal);
                        foreach (var s in tags) if (!string.IsNullOrEmpty(s)) set.Add(s);
                        var arr = new string[set.Count]; set.CopyTo(arr);
                        _item.SetTags(item, arr);
                    }
                    else
                    {
                        _item.SetTags(item, (tags as string[]) ?? tags.ToArray());
                    }
                }
                catch
                {
                    _item.SetTags(item, existing);
                    throw;
                }
                return RichResult.Success();
            }
            catch (Exception ex) { Log.Error("TryWriteTags failed", ex); return RichResult.Fail(ErrorCode.OperationFailed, ex.Message); }
        }
    }
}
