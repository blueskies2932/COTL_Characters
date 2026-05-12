using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiWorldStateReflection
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var direct = assembly.GetType(typeName, false);
                if (direct != null)
                    return direct;

                foreach (var type in SafeTypes(assembly))
                {
                    if (type == null)
                        continue;

                    if (string.Equals(type.Name, typeName, StringComparison.Ordinal) ||
                        string.Equals(type.FullName, typeName, StringComparison.Ordinal))
                        return type;
                }
            }

            return null;
        }

        internal static object GetStaticMemberValue(Type type, params string[] memberNames)
        {
            if (type == null)
                return null;

            foreach (var name in memberNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var property = type.GetProperty(name, StaticFlags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        return property.GetValue(null, null);
                    }
                    catch
                    {
                        continue;
                    }
                }

                var field = type.GetField(name, StaticFlags);
                if (field != null)
                {
                    try
                    {
                        return field.GetValue(null);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            return null;
        }

        internal static bool TryReadStaticFloat(string typeName, string memberName, out float value)
        {
            value = 0f;
            var type = FindType(typeName);
            if (type == null)
                return false;

            return TryConvertFloat(GetStaticMemberValue(type, memberName), out value);
        }

        internal static bool TryReadFloatMember(object instance, out float value, params string[] memberNames)
        {
            value = 0f;
            if (instance == null)
                return false;

            foreach (var name in memberNames ?? Array.Empty<string>())
            {
                if (TryConvertFloat(GetInstanceMemberValue(instance, name), out value))
                    return true;
            }

            return false;
        }

        internal static bool TryReadBoolMember(object instance, out bool value, params string[] memberNames)
        {
            value = false;
            if (instance == null)
                return false;

            foreach (var name in memberNames ?? Array.Empty<string>())
            {
                var raw = GetInstanceMemberValue(instance, name);
                if (raw is bool boolValue)
                {
                    value = boolValue;
                    return true;
                }

                var text = raw?.ToString();
                if (bool.TryParse(text, out value))
                    return true;
            }

            return false;
        }

        internal static string ReadTextMember(object instance, params string[] memberNames)
        {
            if (instance == null)
                return string.Empty;

            foreach (var name in memberNames ?? Array.Empty<string>())
            {
                var text = ConvertToStableText(GetInstanceMemberValue(instance, name));
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return string.Empty;
        }

        internal static string ReadStaticTextMember(Type type, params string[] memberNames)
        {
            if (type == null)
                return string.Empty;

            foreach (var name in memberNames ?? Array.Empty<string>())
            {
                var text = ConvertToStableText(GetStaticMemberValue(type, name));
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return string.Empty;
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return (ex.Types ?? Array.Empty<Type>()).Where(type => type != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static object GetInstanceMemberValue(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
                return null;

            var type = instance.GetType();

            var property = type.GetProperty(memberName, InstanceFlags);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return property.GetValue(instance, null);
                }
                catch
                {
                    return null;
                }
            }

            var field = type.GetField(memberName, InstanceFlags);
            if (field == null)
                return null;

            try
            {
                return field.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryConvertFloat(object raw, out float value)
        {
            value = 0f;
            if (raw == null)
                return false;

            switch (raw)
            {
                case float floatValue:
                    value = floatValue;
                    return true;
                case double doubleValue:
                    value = (float)doubleValue;
                    return true;
                case int intValue:
                    value = intValue;
                    return true;
                case long longValue:
                    value = longValue;
                    return true;
            }

            var text = raw.ToString();
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                   float.TryParse(text, out value);
        }

        private static string ConvertToStableText(object value)
        {
            if (value == null)
                return string.Empty;

            var text = value.ToString();
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        }
    }
}
