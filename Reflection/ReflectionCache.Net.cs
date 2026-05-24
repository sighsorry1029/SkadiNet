using System.Reflection;
using HarmonyLib;

namespace SkadiNet
{
    internal static partial class ReflectionCache
    {
        private static void InitializeNetReflection()
        {
            ZNetType = AccessTools.TypeByName("ZNet");
            ZNetPeerType = AccessTools.TypeByName("ZNetPeer");
            ZRpcType = AccessTools.TypeByName("ZRpc");
            ZPackageType = AccessTools.TypeByName("ZPackage");
            ZSteamSocketType = AccessTools.TypeByName("ZSteamSocket");

            ZNetInstanceField = SilentField(ZNetType, "instance") ?? SilentField(ZNetType, "m_instance") ?? SilentField(ZNetType, "s_instance");
            ZNetPeersField = SilentField(ZNetType, "m_peers");

            PeerRpcField = FieldByQualifiedName("ZDOMan+ZDOPeer:m_peer") ?? FieldByQualifiedName("ZDOPeer:m_peer") ?? FieldByQualifiedName("ZDOMan+ZDOPeer:m_rpc") ?? FieldByQualifiedName("ZDOPeer:m_rpc");
            PeerUidField = FieldByQualifiedName("ZDOMan+ZDOPeer:m_uid") ?? FieldByQualifiedName("ZDOPeer:m_uid");
            PeerRefPosField = FieldByQualifiedName("ZDOMan+ZDOPeer:m_refPos") ?? FieldByQualifiedName("ZDOPeer:m_refPos");

            ZNetPeerRpcField = SilentField(ZNetPeerType, "m_rpc");
            ZNetPeerUidField = SilentField(ZNetPeerType, "m_uid");
            ZNetPeerRefPosField = SilentField(ZNetPeerType, "m_refPos");
            ZSteamSocketSendQueueField = SilentField(ZSteamSocketType, "m_sendQueue");

            ZNetIsServerMethod = AccessTools.Method(ZNetType, "IsServer");
            ZNetIsDedicatedMethod = AccessTools.Method(ZNetType, "IsDedicated");
            ZNetGetPeersMethod = AccessTools.Method(ZNetType, "GetPeers");
            ZNetGetReferencePositionMethod = AccessTools.Method(ZNetType, "GetReferencePosition");
            ZRpcGetSocketMethod = AccessTools.Method(ZRpcType, "GetSocket");
            ZRpcInvokeMethod = AccessTools.Method(ZRpcType, "Invoke", new[] { typeof(string), typeof(object[]) });
            ZRpcUnregisterMethod = AccessTools.Method(ZRpcType, "Unregister", new[] { typeof(string) });
            ZRpcRegisterGenericPackageMethod = FindZRpcPackageRegisterMethod();
            ZSteamSocketSendMethod = AccessTools.Method(ZSteamSocketType, "Send", new[] { ZPackageType }) ?? AccessTools.Method(ZSteamSocketType, "Send");
            ZSteamSocketRecvMethod = AccessTools.Method(ZSteamSocketType, "Recv");
            ZSteamSocketGetSendQueueSizeMethod = AccessTools.Method(ZSteamSocketType, "GetSendQueueSize");
        }

        private static MethodInfo FindZRpcPackageRegisterMethod()
        {
            if (ZRpcType == null || ZPackageType == null) return null;

            foreach (MethodInfo m in ZRpcType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != "Register" || !m.IsGenericMethodDefinition || m.GetGenericArguments().Length != 1)
                    continue;

                ParameterInfo[] p = m.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(string))
                    return m.MakeGenericMethod(ZPackageType);
            }

            return null;
        }
    }
}
