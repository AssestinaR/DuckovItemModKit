using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace ItemModKit.Adapters.Duckov
{
    /// <summary>
    /// Effects schema/type-support：
    /// 统一 effect / effect component 的可持久化成员筛选、成员抓取、值转换与成员写回。
    /// </summary>
    internal static class DuckovEffectSchemaSupport
    {
        public static bool IsSchemaPrimitive(Type type)
        {
            if (type == null) return false;
            if (type.IsEnum) return true;
            return type == typeof(string)
                   || type == typeof(int)
                   || type == typeof(float)
                   || type == typeof(double)
                   || type == typeof(bool)
                   || type == typeof(long)
                   || type == typeof(short)
                   || type == typeof(byte);
        }

        public static Dictionary<string, object> CapturePrimitiveMembers(object instance)
        {
            var result = new Dictionary<string, object>();
            if (instance == null) return result;

            try
            {
                var instanceType = instance.GetType();
                foreach (var property in instanceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!ShouldCaptureProperty(property)) continue;
                    try { result[property.Name] = property.GetValue(instance, null); } catch { }
                }

                foreach (var field in instanceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (!ShouldCaptureField(field)) continue;
                    try { result[field.Name] = field.GetValue(instance); } catch { }
                }
            }
            catch
            {
            }

            return result;
        }

        public static bool ShouldCaptureProperty(PropertyInfo property)
        {
            if (property == null) return false;
            if (!property.CanRead || !property.CanWrite) return false;
            if (property.GetIndexParameters().Length != 0) return false;
            if (!IsSchemaPrimitive(property.PropertyType)) return false;
            if (IsUnityFrameworkType(property.DeclaringType)) return false;
            return true;
        }

        public static bool ShouldCaptureField(FieldInfo field)
        {
            if (field == null) return false;
            if (field.IsStatic || field.IsInitOnly) return false;
            if (!IsSchemaPrimitive(field.FieldType)) return false;
            if (field.Name.StartsWith("<", StringComparison.Ordinal)) return false;
            if (IsUnityFrameworkType(field.DeclaringType)) return false;
            if (field.IsPublic) return true;
            return field.GetCustomAttribute<UnityEngine.SerializeField>() != null;
        }

        public static bool TryAssignMember(object target, string memberName, object value)
        {
            if (target == null || string.IsNullOrEmpty(memberName)) return false;

            try
            {
                var targetType = target.GetType();
                var property = ResolveProperty(targetType, memberName);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(target, ConvertValue(value, property.PropertyType), null);
                    return true;
                }

                var field = ResolveField(targetType, memberName);
                if (field != null)
                {
                    field.SetValue(target, ConvertValue(value, field.FieldType));
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public static void TryAssignMembers(object target, JObject properties)
        {
            try
            {
                if (target == null || properties == null) return;
                foreach (var property in properties.Properties())
                {
                    TryAssignMember(target, property.Name, property.Value);
                }
            }
            catch
            {
            }
        }

        public static object ConvertValue(object value, Type targetType)
        {
            try
            {
                if (targetType == null) return null;
                if (value == null) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

                if (value is JToken token)
                {
                    return ConvertToken(token, targetType);
                }

                if (targetType.IsInstanceOfType(value)) return value;
                if (targetType.IsEnum) return Enum.Parse(targetType, Convert.ToString(value), true);
                return Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
        }

        public static object ConvertToken(JToken token, Type targetType)
        {
            try
            {
                if (token == null || targetType == null) return null;
                if (targetType.IsEnum) return Enum.Parse(targetType, token.ToString(), true);
                if (targetType == typeof(string)) return token.ToString();
                if (targetType == typeof(int)) return token.Value<int>();
                if (targetType == typeof(float)) return token.Value<float>();
                if (targetType == typeof(double)) return token.Value<double>();
                if (targetType == typeof(bool)) return token.Value<bool>();
                if (targetType == typeof(long)) return token.Value<long>();
                if (targetType == typeof(short)) return token.Value<short>();
                if (targetType == typeof(byte)) return token.Value<byte>();
                return token.ToObject(targetType);
            }
            catch
            {
                return null;
            }
        }

        private static PropertyInfo ResolveProperty(Type targetType, string memberName)
        {
            return targetType.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                   ?? targetType.GetProperty(memberName.ToLowerInvariant(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                   ?? targetType.GetProperty(char.ToUpperInvariant(memberName[0]) + memberName.Substring(1), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static bool IsUnityFrameworkType(Type type)
        {
            if (type == null) return false;
            var ns = type.Namespace;
            return !string.IsNullOrEmpty(ns) && ns.StartsWith("UnityEngine", StringComparison.Ordinal);
        }

        private static FieldInfo ResolveField(Type targetType, string memberName)
        {
            return targetType.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                   ?? targetType.GetField(memberName.ToLowerInvariant(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                   ?? targetType.GetField(char.ToUpperInvariant(memberName[0]) + memberName.Substring(1), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
}