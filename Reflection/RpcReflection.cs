using System;
using HarmonyLib;

namespace SkadiNet
{
    internal static class RpcReflection
    {
        private static Func<object, object> _senderPeerIdGetter;
        private static Func<object, object> _targetPeerIdGetter;
        private static Action<object, object> _targetPeerIdSetter;
        private static Func<object, object> _targetZdoGetter;
        private static Func<object, object> _methodHashGetter;
        private static Action<object, object> _deserializeAction;
        private static Action<object, object> _routeRpcAction;

        internal static void Initialize()
        {
            _senderPeerIdGetter = ReflectionDelegateFactory.BoxedFieldGetter(ReflectionCache.RoutedRpcDataSenderPeerIdField);
            _targetPeerIdGetter = ReflectionDelegateFactory.BoxedFieldGetter(ReflectionCache.RoutedRpcDataTargetPeerIdField);
            _targetPeerIdSetter = ReflectionDelegateFactory.BoxedFieldSetter(ReflectionCache.RoutedRpcDataTargetPeerIdField);
            _targetZdoGetter = ReflectionDelegateFactory.BoxedFieldGetter(ReflectionCache.RoutedRpcDataTargetZdoField);
            _methodHashGetter = ReflectionDelegateFactory.BoxedFieldGetter(ReflectionCache.RoutedRpcDataMethodHashField);
            _deserializeAction = ReflectionDelegateFactory.InstanceAction1(ReflectionCache.RoutedRpcDataDeserializeMethod);
            _routeRpcAction = ReflectionDelegateFactory.InstanceAction1(ReflectionCache.ZRoutedRpcRouteRPCMethod);
        }

        internal static Type ZRoutedRpcType => ReflectionCache.ZRoutedRpcType;
        internal static Type RoutedRPCDataType => ReflectionCache.RoutedRPCDataType;

        internal static object CreateRoutedRpcData()
        {
            try { return RoutedRPCDataType != null ? Activator.CreateInstance(RoutedRPCDataType) : null; }
            catch { return null; }
        }

        internal static bool DeserializeRoutedRpcData(object routedData, object package)
        {
            if (routedData == null || package == null) return false;
            try
            {
                if (_deserializeAction != null)
                    _deserializeAction(routedData, package);
                else
                    ReflectionCache.RoutedRpcDataDeserializeMethod?.Invoke(routedData, new[] { package });
                return true;
            }
            catch { return false; }
        }

        internal static int GetMethodHash(object routedData)
        {
            object raw = TryGet(_methodHashGetter, routedData);
            if (raw == null)
            {
                try { raw = ReflectionCache.RoutedRpcDataMethodHashField?.GetValue(routedData); } catch { }
            }
            return raw != null ? Convert.ToInt32(raw) : 0;
        }

        internal static long GetTargetPeerId(object routedData)
        {
            object raw = TryGet(_targetPeerIdGetter, routedData);
            if (raw == null)
            {
                try { raw = ReflectionCache.RoutedRpcDataTargetPeerIdField?.GetValue(routedData); } catch { }
            }
            return raw != null ? Convert.ToInt64(raw) : 0L;
        }

        internal static void SetTargetPeerId(object routedData, long uid)
        {
            try
            {
                if (_targetPeerIdSetter != null)
                    _targetPeerIdSetter(routedData, uid);
                else
                    ReflectionCache.RoutedRpcDataTargetPeerIdField?.SetValue(routedData, uid);
            }
            catch { }
        }

        internal static object GetTargetZdo(object routedData)
        {
            object raw = TryGet(_targetZdoGetter, routedData);
            if (raw != null) return raw;
            try { return ReflectionCache.RoutedRpcDataTargetZdoField?.GetValue(routedData); }
            catch { return null; }
        }

        internal static long GetSenderPeerId(object routedData)
        {
            object raw = TryGet(_senderPeerIdGetter, routedData);
            if (raw == null)
            {
                try { raw = ReflectionCache.RoutedRpcDataSenderPeerIdField?.GetValue(routedData); } catch { }
            }
            return raw != null ? Convert.ToInt64(raw) : 0L;
        }

        internal static bool RouteRpc(object routedRpc, object routedData)
        {
            if (routedRpc == null || routedData == null) return false;
            try
            {
                if (_routeRpcAction != null)
                    _routeRpcAction(routedRpc, routedData);
                else
                    ReflectionCache.ZRoutedRpcRouteRPCMethod?.Invoke(routedRpc, new[] { routedData });
                return true;
            }
            catch { return false; }
        }

        private static object TryGet(Func<object, object> getter, object instance)
        {
            try { return getter != null && instance != null ? getter(instance) : null; }
            catch { return null; }
        }
    }
}
