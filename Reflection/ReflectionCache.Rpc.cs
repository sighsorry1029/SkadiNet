using HarmonyLib;

namespace SkadiNet
{
    internal static partial class ReflectionCache
    {
        private static void InitializeRpcReflection()
        {
            ZRoutedRpcType = AccessTools.TypeByName("ZRoutedRpc");
            RoutedRPCDataType = AccessTools.TypeByName("ZRoutedRpc+RoutedRPCData") ?? AccessTools.TypeByName("RoutedRPCData");

            RoutedRpcDataSenderPeerIdField = SilentField(RoutedRPCDataType, "m_senderPeerID");
            RoutedRpcDataTargetPeerIdField = SilentField(RoutedRPCDataType, "m_targetPeerID");
            RoutedRpcDataTargetZdoField = SilentField(RoutedRPCDataType, "m_targetZDO");
            RoutedRpcDataMethodHashField = SilentField(RoutedRPCDataType, "m_methodHash");
            RoutedRpcDataParametersField = SilentField(RoutedRPCDataType, "m_parameters");

            RoutedRpcDataSerializeMethod = AccessTools.Method(RoutedRPCDataType, "Serialize");
            RoutedRpcDataDeserializeMethod = AccessTools.Method(RoutedRPCDataType, "Deserialize");
            ZRoutedRpcRouteRPCMethod = AccessTools.Method(ZRoutedRpcType, "RouteRPC", new[] { RoutedRPCDataType }) ?? AccessTools.Method(ZRoutedRpcType, "RouteRPC");
        }
    }
}
