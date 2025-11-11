using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
 // Simple in-memory transaction manager for per-item batched changes.
 // For safety, we snapshot core fields/variables/constants/tags, and restore on rollback.
 internal sealed class DuckovTransactionManager
 {
 private sealed class Tx
 {
 public ItemSnapshot Snapshot;
 public DateTime StartedAt;
 }
 private readonly ConcurrentDictionary<(int,string), Tx> _map = new ConcurrentDictionary<(int,string), Tx>();

 private static int GetItemId(object item) => DuckovTypeUtils.GetStableId(item);

 public string Begin(IItemAdapter adapter, object item)
 {
 var token = Guid.NewGuid().ToString("N");
 var id = GetItemId(item);
 var snap = ItemSnapshot.Capture(adapter, item);
 _map[(id, token)] = new Tx{ Snapshot = snap, StartedAt = DateTime.UtcNow };
 return token;
 }
 public bool TryGet(object item, string token, out ItemSnapshot snap)
 {
 Tx tx; var ok = _map.TryGetValue((GetItemId(item), token), out tx);
 snap = ok ? tx.Snapshot : null; return ok;
 }
 public bool Commit(object item, string token)
 {
 return _map.TryRemove((GetItemId(item), token), out _);
 }
 public bool Rollback(IItemAdapter adapter, IWriteService writer, object item, string token)
 {
 Tx tx; if (!_map.TryRemove((GetItemId(item), token), out tx) || tx.Snapshot == null) return false;
 // Restore snapshot minimums: core fields, variables, constants, tags
 var snap = tx.Snapshot;
 writer.TryWriteCoreFields(item, new CoreFieldChanges{ Name = snap.NameRaw, RawName = snap.NameRaw, TypeId = snap.TypeId, Quality = snap.Quality, DisplayQuality = snap.DisplayQuality, Value = snap.Value });
 // variables
 var vars = new List<KeyValuePair<string, object>>();
 foreach (var v in snap.Variables) vars.Add(new KeyValuePair<string, object>(v.Key, v.Value));
 writer.TryWriteVariables(item, (IEnumerable<KeyValuePair<string, object>>)vars, true);
 // tags
 writer.TryWriteTags(item, (IEnumerable<string>)(snap.Tags ?? Array.Empty<string>()), false);
 // constants cannot be fully captured as values type unknown; best effort: reuse variables path if adapter treats constants similarly
 var consts = adapter.GetConstants(item);
 if (consts != null)
 {
 var list = new List<KeyValuePair<string, object>>();
 foreach (var c in consts) list.Add(new KeyValuePair<string, object>(c.Key, c.Value));
 writer.TryWriteConstants(item, (IEnumerable<KeyValuePair<string, object>>)list, true);
 }
 return true;
 }
 }
}
