using System.Collections.Generic;
using ItemModKit.Core;
using static ItemModKit.Adapters.Duckov.DuckovTypeUtils;

namespace ItemModKit.Adapters.Duckov
{
 internal sealed class DuckovSlotAdapter : ISlotAdapter
 {
 public bool TryPlugToCharacter(object newItem, int preferredFirstIndex =0)
 {
 try
 {
 var cmcT = FindType("CharacterMainControl") ?? FindType("TeamSoda.Duckov.Core.CharacterMainControl");
 var main = cmcT?.GetProperty("Main", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)?.GetValue(null, null);
 var charItem = main?.GetType().GetProperty("CharacterItem", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance)?.GetValue(main, null);
 if (charItem == null) return false;
 var tryPlug = charItem.GetType().GetMethod("TryPlug", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 if (tryPlug != null)
 {
 var ps = tryPlug.GetParameters();
 if (ps.Length >=1)
 {
 var args = new List<object>();
 args.Add(newItem);
 if (ps.Length >=2) args.Add(true);
 if (ps.Length >=3) args.Add(null);
 if (ps.Length >=4) args.Add(preferredFirstIndex);
 var r = tryPlug.Invoke(charItem, args.ToArray());
 if (r is bool b) return b; return true;
 }
 }
 }
 catch { }
 return false;
 }
 }
}
