using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SkadiNet
{
    internal static class PeerLifecycle
    {
        internal static void ClearDisconnectedPeer(object zdoPeer)
        {
            long uid = 0;
            NetReflection.TryGetPeerUid(zdoPeer, out uid);

            FeatureNegotiation.ClearPeer(zdoPeer, uid);
            PeerQualityMeter.ClearPeer(zdoPeer, uid);
            OwnershipManager.ClearPeer(uid);
            ZDOManSendSchedulerPatch.ClearPeer(uid);
        }
    }

    [HarmonyPatch]
    internal static class PeerLifecycleDisconnectPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var targets = new HashSet<MethodBase>();
            AddNamedMethods(targets, ReflectionCache.ZNetType ?? AccessTools.TypeByName("ZNet"), "Disconnect", "OnDisconnected", "OnPeerDisconnected", "RemovePeer", "RPC_Disconnect");
            AddNamedMethods(targets, ReflectionCache.ZDOManType ?? AccessTools.TypeByName("ZDOMan"), "RemovePeer", "RemoveZdoPeer", "DisconnectPeer");

            foreach (MethodBase target in targets)
                yield return target;
        }

        private static void AddNamedMethods(HashSet<MethodBase> targets, Type type, params string[] names)
        {
            if (targets == null || type == null || names == null) return;

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (method.Name == names[i])
                    {
                        targets.Add(method);
                        break;
                    }
                }
            }
        }

        private static void Postfix(object[] __args)
        {
            if (__args == null) return;
            for (int i = 0; i < __args.Length; i++)
                TryClear(__args[i]);
        }

        private static void TryClear(object candidate)
        {
            if (candidate == null) return;

            bool hasUid = NetReflection.TryGetPeerUid(candidate, out long uid) && uid != 0;
            bool hasRpc = NetReflection.GetPeerRpc(candidate) != null;
            bool isRpc = ReflectionCache.ZRpcType != null && ReflectionCache.ZRpcType.IsInstanceOfType(candidate);

            if (hasUid || hasRpc || isRpc)
                PeerLifecycle.ClearDisconnectedPeer(candidate);
        }
    }
}
