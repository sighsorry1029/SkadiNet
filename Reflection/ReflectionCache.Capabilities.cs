using System;

namespace SkadiNet
{
    internal static partial class ReflectionCache
    {
        private static void LogCapabilitySummary()
        {
            try
            {
                if (ModConfig.DebugLogging == null || !ModConfig.DebugLogging.Value || Plugin.Log == null)
                    return;

                Plugin.Log.LogDebug(
                    "Reflection capabilities: " +
                    $"Net[{Status(ZNetType)} type, {Status(ZRpcGetSocketMethod)} rpcSocket, {Status(ZSteamSocketSendMethod)} socketSend] " +
                    $"ZDO[{Status(ZDOType)} type, {Status(SendZDOsMethod)} send, {Status(ZDOExtraDataGetFloatsMethod)} extraData] " +
                    $"RPC[{Status(ZRoutedRpcType)} type, {Status(RoutedRpcDataDeserializeMethod)} data, {Status(ZRoutedRpcRouteRPCMethod)} route] " +
                    $"Gameplay[{Status(ZNetViewType)} nview, {Status(PlayerGetPlayerIDMethod)} playerId, {Status(ZNetViewGetZDOMethod)} zdo]");
            }
            catch (Exception ex)
            {
                try { Plugin.Log?.LogDebug($"Reflection capability summary skipped: {ex.Message}"); } catch { }
            }
        }

        private static string Status(object value)
        {
            return value != null ? "ok" : "missing";
        }
    }
}
