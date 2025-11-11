using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ItemModKit.Adapters.Duckov
{
    internal static class DuckovBindings
    {
        private const string FileName = "imk.bindings.txt"; // line-based store: Type|addNumSig|ModifierType|EnumType
        private static readonly Dictionary<string, AddSigDto> s_add = new Dictionary<string, AddSigDto>(StringComparer.Ordinal);
        private static bool s_loaded;
        private static string PathFile => System.IO.Path.Combine(Application.persistentDataPath, "IMK", FileName);

        private static void EnsureLoaded()
        {
            if (s_loaded) return;
            s_loaded = true;
            try
            {
                var dir = System.IO.Path.GetDirectoryName(PathFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (!File.Exists(PathFile)) return;
                foreach (var line in File.ReadAllLines(PathFile, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("#")) continue;
                    var parts = line.Split('|');
                    if (parts.Length < 4) continue;
                    var dto = new AddSigDto { AddNum = parts[1], ModifierType = parts[2], EnumType = parts[3] };
                    s_add[parts[0]] = dto;
                }
            }
            catch { }
        }

        private static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# IMK runtime bindings (TypeFullName|AddNumSig|ModifierType|EnumType)");
                foreach (var kv in s_add)
                {
                    var dto = kv.Value;
                    sb.Append(kv.Key).Append('|')
                      .Append(dto.AddNum ?? string.Empty).Append('|')
                      .Append(dto.ModifierType ?? string.Empty).Append('|')
                      .Append(dto.EnumType ?? string.Empty).AppendLine();
                }
                File.WriteAllText(PathFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static bool TryGetAddSignature(Type itemType, out (MethodInfo addNum, MethodInfo addObj, Type modifierType, Type enumType) sig)
        {
            EnsureLoaded(); sig = default; if (itemType == null) return false;
            if (!s_add.TryGetValue(itemType.FullName, out var dto)) return false;
            try
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                MethodInfo addNum = null, addObj = null; Type modType = null, enumType = null;
                if (!string.IsNullOrEmpty(dto.AddNum))
                {
                    var pars = dto.AddNum == "s,f,b" ? new[] { typeof(string), typeof(float), typeof(bool) } : new[] { typeof(string), typeof(float) };
                    addNum = itemType.GetMethod("AddModifier", flags, null, pars, null);
                }
                if (!string.IsNullOrEmpty(dto.ModifierType))
                {
                    modType = FindType(dto.ModifierType);
                    if (modType != null)
                    {
                        addObj = itemType.GetMethod("AddModifier", flags, null, new[] { typeof(string), modType }, null);
                        if (!string.IsNullOrEmpty(dto.EnumType)) enumType = FindType(dto.EnumType);
                    }
                }
                sig = (addNum, addObj, modType, enumType);
                return addNum != null || addObj != null;
            }
            catch { return false; }
        }

        public static void RecordAddSignature(Type itemType, MethodInfo addNum, MethodInfo addObj, Type modifierType, Type enumType)
        {
            EnsureLoaded(); if (itemType == null) return;
            try
            {
                var dto = new AddSigDto
                {
                    AddNum = addNum == null ? null : (addNum.GetParameters().Length == 3 ? "s,f,b" : "s,f"),
                    ModifierType = modifierType?.FullName,
                    EnumType = enumType?.FullName
                };
                s_add[itemType.FullName] = dto;
                Save();
            }
            catch { }
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { var t = a.GetType(fullName, throwOnError: false); if (t != null) return t; } catch { }
                }
            }
            catch { }
            return null;
        }

        private class AddSigDto
        {
            public string AddNum; // "s,f" or "s,f,b" or null
            public string ModifierType;
            public string EnumType;
        }
    }
}
