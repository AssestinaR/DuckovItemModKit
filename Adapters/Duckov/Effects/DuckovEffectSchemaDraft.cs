using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemModKit.Core;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// effects schema draft：
    /// 只负责枚举 vanilla effect graph 的结构层类型与 primitive 成员，供 Probe、UI 和内部工具解释 stock support。
    /// richer combat event ingress 不属于这里；那类能力应停留在独立 bridge 层，而不是继续混进 effect 结构读写。
    /// </summary>
    internal static class DuckovEffectSchemaDraft
    {
        public static RichResult<EffectSchemaCatalogDraft> Enumerate(bool includeAbstractTypes = false)
        {
            try
            {
                var effectBase = DuckovTypeUtils.FindType("ItemStatsSystem.Effect");
                var triggerBase = DuckovTypeUtils.FindType("ItemStatsSystem.EffectTrigger");
                var filterBase = DuckovTypeUtils.FindType("ItemStatsSystem.EffectFilter");
                var actionBase = DuckovTypeUtils.FindType("ItemStatsSystem.EffectAction");

                if (effectBase == null || triggerBase == null || filterBase == null || actionBase == null)
                {
                    return RichResult<EffectSchemaCatalogDraft>.Fail(ErrorCode.DependencyMissing, "effect schema base types missing");
                }

                var catalog = new EffectSchemaCatalogDraft { IncludeAbstractTypes = includeAbstractTypes };
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                    catch { continue; }

                    foreach (var type in types)
                    {
                        if (type == null || type.IsGenericTypeDefinition) continue;
                        if (!includeAbstractTypes && type.IsAbstract) continue;

                        var category = ResolveCategory(type, effectBase, triggerBase, filterBase, actionBase);
                        if (!category.HasValue) continue;

                        var entry = BuildEntry(type, category.Value);
                        AddEntry(catalog, entry);
                    }
                }

                SortEntries(catalog.Effects);
                SortEntries(catalog.Triggers);
                SortEntries(catalog.Filters);
                SortEntries(catalog.Actions);
                return RichResult<EffectSchemaCatalogDraft>.Success(catalog);
            }
            catch (Exception ex)
            {
                Log.Error("Enumerate effect schema draft failed", ex);
                return RichResult<EffectSchemaCatalogDraft>.Fail(ErrorCode.OperationFailed, ex.Message);
            }
        }

        private static EffectSchemaDraftCategory? ResolveCategory(Type type, Type effectBase, Type triggerBase, Type filterBase, Type actionBase)
        {
            if (type == effectBase || effectBase.IsAssignableFrom(type))
            {
                if (type == triggerBase || triggerBase.IsAssignableFrom(type)) return EffectSchemaDraftCategory.Trigger;
                if (type == filterBase || filterBase.IsAssignableFrom(type)) return EffectSchemaDraftCategory.Filter;
                if (type == actionBase || actionBase.IsAssignableFrom(type)) return EffectSchemaDraftCategory.Action;
                return EffectSchemaDraftCategory.Effect;
            }

            return null;
        }

        private static EffectSchemaEntryDraft BuildEntry(Type type, EffectSchemaDraftCategory category)
        {
            var entry = new EffectSchemaEntryDraft
            {
                Category = category,
                TypeFullName = type.FullName,
                TypeName = type.Name,
                AssemblyName = type.Assembly.GetName().Name,
                Abstract = type.IsAbstract,
            };

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!DuckovEffectSchemaSupport.ShouldCaptureProperty(property)) continue;
                if (entry.Members.Any(m => m.Name == property.Name && m.SourceKind == "Property")) continue;

                entry.Members.Add(new EffectSchemaMemberDraft
                {
                    Name = property.Name,
                    TypeName = property.PropertyType.FullName,
                    SourceKind = "Property",
                    Writable = property.CanWrite,
                    Public = (property.GetMethod?.IsPublic ?? false) || (property.SetMethod?.IsPublic ?? false),
                    SerializedField = false,
                });
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!DuckovEffectSchemaSupport.ShouldCaptureField(field)) continue;
                if (entry.Members.Any(m => m.Name == field.Name && m.SourceKind == "Field")) continue;

                entry.Members.Add(new EffectSchemaMemberDraft
                {
                    Name = field.Name,
                    TypeName = field.FieldType.FullName,
                    SourceKind = "Field",
                    Writable = !field.IsInitOnly,
                    Public = field.IsPublic,
                    SerializedField = field.GetCustomAttribute<UnityEngine.SerializeField>() != null,
                });
            }

            entry.Members = entry.Members
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ThenBy(m => m.SourceKind, StringComparer.Ordinal)
                .ToList();
            return entry;
        }

        private static void AddEntry(EffectSchemaCatalogDraft catalog, EffectSchemaEntryDraft entry)
        {
            switch (entry.Category)
            {
                case EffectSchemaDraftCategory.Effect:
                    catalog.Effects.Add(entry);
                    break;
                case EffectSchemaDraftCategory.Trigger:
                    catalog.Triggers.Add(entry);
                    break;
                case EffectSchemaDraftCategory.Filter:
                    catalog.Filters.Add(entry);
                    break;
                case EffectSchemaDraftCategory.Action:
                    catalog.Actions.Add(entry);
                    break;
            }
        }

        private static void SortEntries(List<EffectSchemaEntryDraft> entries)
        {
            entries.Sort((a, b) => string.Compare(a.TypeFullName, b.TypeFullName, StringComparison.Ordinal));
        }
    }
}