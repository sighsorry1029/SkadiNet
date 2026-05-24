using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SkadiNet
{
    internal static partial class ReflectionCache
    {
        internal static bool TryConvertToLong(object value, out long result)
        {
            result = 0;
            if (value is long l) { result = l; return true; }
            if (value is ulong ul) { result = unchecked((long)ul); return true; }
            if (value is int i) { result = i; return true; }
            if (value is uint ui) { result = ui; return true; }
            if (value is short s) { result = s; return true; }
            if (value is ushort us) { result = us; return true; }
            return false;
        }

        internal static bool TryConvertToUInt(object value, out uint result)
        {
            result = 0;
            if (value is uint ui) { result = ui; return true; }
            if (value is int i && i >= 0) { result = (uint)i; return true; }
            if (value is ulong ul && ul <= uint.MaxValue) { result = (uint)ul; return true; }
            if (value is long l && l >= 0 && l <= uint.MaxValue) { result = (uint)l; return true; }
            if (value is ushort us) { result = us; return true; }
            if (value is short s && s >= 0) { result = (uint)s; return true; }
            if (value is byte b) { result = b; return true; }
            return false;
        }

        internal static FieldInfo CachedField(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;

            lock (FieldCache)
            {
                if (!FieldCache.TryGetValue(type, out Dictionary<string, FieldInfo> byName))
                {
                    byName = new Dictionary<string, FieldInfo>();
                    FieldCache[type] = byName;
                }

                if (!byName.TryGetValue(name, out FieldInfo field))
                {
                    field = SilentField(type, name);
                    byName[name] = field;
                }

                return field;
            }
        }

        internal static bool TryReadKeyValueEntry(object entry, out int key, out object value)
        {
            key = 0;
            value = null;
            if (entry == null) return false;

            try
            {
                KeyValueEntryAccessors accessors = GetKeyValueAccessors(entry.GetType());
                if (accessors?.Key == null || accessors.Value == null) return false;
                object rawKey = accessors.Key.GetValue(entry, null);
                key = Convert.ToInt32(rawKey);
                value = accessors.Value.GetValue(entry, null);
                return true;
            }
            catch { return false; }
        }

        internal static bool TryReadKeyValueKey(object entry, out int key)
        {
            key = 0;
            if (entry == null) return false;

            try
            {
                KeyValueEntryAccessors accessors = GetKeyValueAccessors(entry.GetType());
                if (accessors?.Key == null) return false;
                object rawKey = accessors.Key.GetValue(entry, null);
                key = Convert.ToInt32(rawKey);
                return true;
            }
            catch { return false; }
        }

        private static KeyValueEntryAccessors GetKeyValueAccessors(Type type)
        {
            if (type == null) return null;

            lock (KeyValueAccessorsByType)
            {
                if (!KeyValueAccessorsByType.TryGetValue(type, out KeyValueEntryAccessors accessors))
                {
                    accessors = new KeyValueEntryAccessors(
                        type.GetProperty("Key"),
                        type.GetProperty("Value")
                    );
                    KeyValueAccessorsByType[type] = accessors;
                }

                return accessors;
            }
        }

        private sealed class KeyValueEntryAccessors
        {
            internal readonly PropertyInfo Key;
            internal readonly PropertyInfo Value;

            internal KeyValueEntryAccessors(PropertyInfo key, PropertyInfo value)
            {
                Key = key;
                Value = value;
            }
        }
    }
}
