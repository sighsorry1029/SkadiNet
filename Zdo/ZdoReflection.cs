using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SkadiNet
{
    internal static class ZdoReflection
    {
        private static Func<object, object> _zdoUidGetter;
        private static Func<object, object> _zdoOwnerGetter;
        private static Func<object, object> _zdoPrefabGetter;
        private static Func<object, object> _zdoPrefabFieldGetter;
        private static Func<object, object> _zdoPersistentGetter;
        private static Func<object, object> _zdoDistantGetter;
        private static Func<object, object> _zdoTypeGetter;
        private static Func<object, object> _connectionTypeGetter;
        private static Func<object, Vector3> _zdoPositionGetter;
        private static Func<object, Quaternion> _zdoRotationGetter;
        private static Func<object, Quaternion> _zdoRotationFieldGetter;

        internal static void Initialize()
        {
            _zdoUidGetter = ReflectionDelegateFactory.BoxedFieldGetter(ReflectionCache.ZDOUidField);
            _zdoOwnerGetter = ReflectionDelegateFactory.BoxedInstanceMethod(ReflectionCache.ZDOGetOwnerMethod);
            _zdoPrefabGetter = ReflectionDelegateFactory.BoxedInstanceMethod(ReflectionCache.ZDOGetPrefabMethod);
            _zdoPrefabFieldGetter = ReflectionDelegateFactory.BoxedFieldGetter(ReflectionCache.ZDOPrefabField);
            _zdoPersistentGetter = ReflectionDelegateFactory.BoxedInstanceMethod(ReflectionCache.ZDOPersistentGetter);
            _zdoDistantGetter = ReflectionDelegateFactory.BoxedInstanceMethod(ReflectionCache.ZDODistantGetter);
            _zdoTypeGetter = ReflectionDelegateFactory.BoxedInstanceMethod(ReflectionCache.ZDOTypeGetter);
            _connectionTypeGetter = null;
            _zdoPositionGetter = ReflectionDelegateFactory.Vector3InstanceMethod(ReflectionCache.ZDOGetPositionMethod);
            _zdoRotationGetter = ReflectionDelegateFactory.QuaternionInstanceMethod(ReflectionCache.ZDOGetRotationMethod);
            _zdoRotationFieldGetter = ReflectionDelegateFactory.QuaternionFieldGetter(ReflectionCache.ZDORotationField);
        }

        internal static Type ZDOManType => ReflectionCache.ZDOManType;
        internal static Type ZDOType => ReflectionCache.ZDOType;
        internal static Type ZDOIDType => ReflectionCache.ZDOIDType;
        internal static Type ZDOVarsType => ReflectionCache.ZDOVarsType;
        internal static MethodInfo SendZDOsMethod => ReflectionCache.SendZDOsMethod;

        internal static object ZDOManInstance => ReflectionCache.ZDOManInstanceField?.GetValue(null);

        internal static bool TryGetOwner(object zdo, out long owner)
        {
            owner = 0;
            if (zdo == null) return false;
            try
            {
                object value = TryGet(_zdoOwnerGetter, zdo) ?? ReflectionCache.ZDOGetOwnerMethod?.Invoke(zdo, null);
                return ReflectionCache.TryConvertToLong(value, out owner);
            }
            catch { return false; }
        }

        internal static bool TrySetOwner(object zdo, long uid)
        {
            try
            {
                if (zdo == null) return false;
                if (ReflectionCache.ZDOSetOwnerMethod != null)
                {
                    ReflectionCache.ZDOSetOwnerMethod.Invoke(zdo, new object[] { uid });
                    return true;
                }
                if (ReflectionCache.ZDOSetOwnerInternalMethod != null)
                {
                    ReflectionCache.ZDOSetOwnerInternalMethod.Invoke(zdo, new object[] { uid });
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (ModConfig.DebugLogging.Value) Plugin.Log.LogWarning($"SetOwner failed: {ex.Message}");
            }
            return false;
        }

        internal static object GetIdObject(object zdo)
        {
            if (zdo == null) return null;
            try { return TryGet(_zdoUidGetter, zdo) ?? ReflectionCache.ZDOUidField?.GetValue(zdo); }
            catch { return null; }
        }

        internal static bool TryGetIdKey(object zdo, out ZdoIdKey key)
        {
            key = default;
            if (zdo == null) return false;

            object id = GetIdObject(zdo);
            if (TryGetIdKeyFromId(id, out key))
                return true;

            key = ZdoIdKey.FromRuntimeObject(zdo);
            return true;
        }

        internal static bool TryGetIdKeyFromId(object zdoId, out ZdoIdKey key)
        {
            key = default;
            if (zdoId == null) return false;

            try
            {
                object rawUser = ReflectionCache.ZDOIDUserIDGetter?.Invoke(zdoId, null);
                object rawId = ReflectionCache.ZDOIDIDGetter?.Invoke(zdoId, null);
                if (ReflectionCache.TryConvertToLong(rawUser, out long userId) && ReflectionCache.TryConvertToUInt(rawId, out uint id))
                {
                    key = new ZdoIdKey(userId, id);
                    return true;
                }
            }
            catch { }

            try
            {
                string text = zdoId.ToString();
                int split = text.IndexOf(':');
                if (split > 0 &&
                    long.TryParse(text.Substring(0, split), out long userId) &&
                    uint.TryParse(text.Substring(split + 1), out uint id))
                {
                    key = new ZdoIdKey(userId, id);
                    return true;
                }
            }
            catch { }

            return false;
        }

        internal static void ForceSend(object zdo)
        {
            try
            {
                object zdoMan = ZDOManInstance;
                object id = GetIdObject(zdo);
                if (zdoMan == null || id == null || ReflectionCache.ZDOManForceSendZDOMethod == null) return;
                ReflectionCache.ZDOManForceSendZDOMethod.Invoke(zdoMan, new[] { id });
            }
            catch { }
        }

        internal static IEnumerable<object> EnumeratePeers(object zdoMan)
        {
            if (zdoMan == null || ReflectionCache.ZDOManPeersField == null) yield break;
            IEnumerable peers = null;
            try { peers = ReflectionCache.ZDOManPeersField.GetValue(zdoMan) as IEnumerable; }
            catch { }
            if (peers == null) yield break;
            foreach (object peer in peers) yield return peer;
        }

        internal static object GetById(object zdoId)
        {
            try
            {
                object man = ZDOManInstance;
                if (man == null || zdoId == null || ReflectionCache.ZDOManGetZDOMethod == null) return null;
                return ReflectionCache.ZDOManGetZDOMethod.Invoke(man, new[] { zdoId });
            }
            catch { return null; }
        }

        internal static bool IsIdNone(object zdoId)
        {
            try
            {
                if (zdoId == null) return true;
                if (ReflectionCache.ZDOIDIsNoneMethod != null && ReflectionCache.ZDOIDIsNoneMethod.Invoke(zdoId, null) is bool b) return b;
            }
            catch { }
            return false;
        }

        internal static bool TryGetPersistent(object zdo, out bool persistent)
        {
            persistent = false;
            if (zdo == null) return false;
            try
            {
                object raw = TryGet(_zdoPersistentGetter, zdo) ?? ReflectionCache.ZDOPersistentGetter?.Invoke(zdo, null);
                if (raw is bool b)
                {
                    persistent = b;
                    return true;
                }
            }
            catch { }
            return false;
        }

        internal static bool TryGetDistant(object zdo, out bool distant)
        {
            distant = false;
            if (zdo == null) return false;
            try
            {
                object raw = TryGet(_zdoDistantGetter, zdo) ?? ReflectionCache.ZDODistantGetter?.Invoke(zdo, null);
                if (raw is bool b)
                {
                    distant = b;
                    return true;
                }
            }
            catch { }
            return false;
        }

        internal static bool TryGetServerSessionId(out long uid)
        {
            uid = 0;
            try
            {
                object zdoMan = ZDOManInstance;
                if (zdoMan != null && ReflectionCache.ZDOManSessionIdField != null)
                {
                    object raw = ReflectionCache.ZDOManSessionIdField.GetValue(zdoMan);
                    if (ReflectionCache.TryConvertToLong(raw, out uid) && uid != 0) return true;
                }

                if (ReflectionCache.ZDOManGetSessionIdMethod != null)
                {
                    object raw = ReflectionCache.ZDOManGetSessionIdMethod.Invoke(null, null);
                    if (ReflectionCache.TryConvertToLong(raw, out uid) && uid != 0) return true;
                }
            }
            catch { }
            return false;
        }

        internal static int GetPrefabHash(object zdo)
        {
            try
            {
                if (zdo == null) return 0;
                object raw = TryGet(_zdoPrefabGetter, zdo) ?? TryGet(_zdoPrefabFieldGetter, zdo);
                if (raw is int i) return i;
                if (raw != null) return Convert.ToInt32(raw);
            }
            catch { }
            return 0;
        }

        internal static Quaternion GetRotation(object zdo, Quaternion fallback)
        {
            try
            {
                if (zdo == null) return fallback;
                if (TryGetQuaternion(_zdoRotationGetter, zdo, out Quaternion methodRot)) return methodRot;
                if (TryGetQuaternion(_zdoRotationFieldGetter, zdo, out Quaternion fieldRot)) return fieldRot;
                object raw = ReflectionCache.ZDOGetRotationMethod?.Invoke(zdo, null) ?? ReflectionCache.ZDORotationField?.GetValue(zdo);
                if (raw is Quaternion q) return q;
            }
            catch { }
            return fallback;
        }

        internal static int GetTypeValue(object zdo)
        {
            try
            {
                if (zdo == null) return 0;
                object raw = TryGet(_zdoTypeGetter, zdo) ?? ReflectionCache.ZDOTypeGetter?.Invoke(zdo, null);
                return raw != null ? Convert.ToInt32(raw) : 0;
            }
            catch { }
            return 0;
        }

        internal static bool TryGetConnectionType(object connection, out int type)
        {
            type = 0;
            if (connection == null) return false;
            try
            {
                object raw = TryGet(_connectionTypeGetter, connection);
                if (raw == null)
                {
                    FieldInfo typeField = ReflectionCache.CachedField(connection.GetType(), "m_type");
                    _connectionTypeGetter = ReflectionDelegateFactory.BoxedFieldGetter(typeField);
                    raw = TryGet(_connectionTypeGetter, connection) ?? typeField?.GetValue(connection);
                }
                if (raw == null) return false;
                type = Convert.ToInt32(raw);
                return true;
            }
            catch { return false; }
        }

        internal static Vector3 GetPosition(object zdo, Vector3 fallback)
        {
            try
            {
                if (zdo == null) return fallback;
                if (TryGetVector3(_zdoPositionGetter, zdo, out Vector3 p)) return p;
                if (ReflectionCache.ZDOGetPositionMethod?.Invoke(zdo, null) is Vector3 reflected) return reflected;
            }
            catch { }
            return fallback;
        }

        private static object TryGet(Func<object, object> getter, object instance)
        {
            try { return getter != null && instance != null ? getter(instance) : null; }
            catch { return null; }
        }

        private static bool TryGetVector3(Func<object, Vector3> getter, object instance, out Vector3 value)
        {
            value = Vector3.zero;
            try
            {
                if (getter == null || instance == null) return false;
                value = getter(instance);
                return true;
            }
            catch { return false; }
        }

        private static bool TryGetQuaternion(Func<object, Quaternion> getter, object instance, out Quaternion value)
        {
            value = Quaternion.identity;
            try
            {
                if (getter == null || instance == null) return false;
                value = getter(instance);
                return true;
            }
            catch { return false; }
        }
    }
}
