using BepInEx.Configuration;

namespace SkadiNet
{
    internal static class ModConfig
    {
        private const string GeneralSection = "General";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> LockServerConfig;
        internal static ConfigEntry<bool> DebugLogging;

        internal static ConfigEntry<int> SchedulerThroughput;

        internal static ConfigEntry<int> PayloadReducerStrength;

        internal static ConfigEntry<int> OwnershipIntensity;

        internal static ConfigEntry<int> CompressionAggression;

        internal static ConfigEntry<int> RpcAoiAggression;

        internal static ConfigEntry<int> ClientStutterGuardStrength;

        internal static void Bind(ConfigFile config)
        {
            Enabled = ConfigSyncManager.Bind(config, GeneralSection, "Enabled", true, Description("Master switch.", 100), ConfigSyncScope.ServerSynced);
            LockServerConfig = ConfigSyncManager.LockServerConfig;
            DebugLogging = ConfigSyncManager.Bind(config, GeneralSection, "DebugLogging", false, Description("Diagnostic log output. Enable temporarily to inspect frame hitches, FPS, network queues, scheduler stalls, compression cost, and ownership scan cost; keep this off during normal live play.", 80), ConfigSyncScope.ClientLocal);

            SchedulerThroughput = ConfigSyncManager.Bind(config, GeneralSection, "SchedulerThroughput", 35, FeatureSliderDescription("0 disables the adaptive scheduler. 1 keeps package caps close to vanilla while using the gentlest adaptive scheduler; 35 is recommended; 50 is balanced; 100 favors lower latency, higher ZDO throughput, and faster lagging-peer backfill. Steam send-rate is fixed internally at 36 MB/s while SkadiNet is enabled.", 70), ConfigSyncScope.ServerSynced);

            PayloadReducerStrength = ConfigSyncManager.Bind(config, GeneralSection, "PayloadReducerStrength", 30, FeatureSliderDescription("0 disables the payload reducer. 1 favors sync fidelity; 50 is balanced; 100 applies stronger Vector3/Quaternion micro-update reduction.", 60), ConfigSyncScope.ServerSynced);

            CompressionAggression = ConfigSyncManager.Bind(config, GeneralSection, "CompressionAggression", 50, FeatureSliderDescription("0 disables negotiated package compression. 1 compresses only large/high-value packets; 50 is balanced; 100 considers smaller packets and smaller savings.", 50), ConfigSyncScope.ServerSynced);

            OwnershipIntensity = ConfigSyncManager.Bind(config, GeneralSection, "OwnershipIntensity", 45, FeatureSliderDescription("0 disables Profile A adaptive ownership, peer-quality gates, and combat owner hints. 1 is very conservative with low CPU, narrow candidate reach, weak hints, and forgiving peer quality; 45 is recommended; 50 is balanced; 100 scans farther/faster, uses stronger hints, and rejects poor ping/jitter candidates more aggressively.", 40), ConfigSyncScope.ServerSynced);

            ClientStutterGuardStrength = ConfigSyncManager.Bind(config, GeneralSection, "ClientStutterGuardStrength", 50, FeatureSliderDescription("0 disables the client stutter guard. 50 is the balanced default. 1 runs cleanup sooner under pressure; 100 protects longer against cleanup stutter.", 30), ConfigSyncScope.ClientLocal);

            RpcAoiAggression = ConfigSyncManager.Bind(config, GeneralSection, "RpcAoiAggression", 35, FeatureSliderDescription("0 disables RPC AoI. 35 is the conservative default. 1 keeps a larger radius for safe visual RPCs; 50 is balanced; 100 routes eligible visual RPCs to smaller local areas. Unknown, unresolved, global, animation, noise, and state-critical RPCs always use vanilla routing.", 20), ConfigSyncScope.ServerSynced);
        }

        private static ConfigDescription Description(string description, int order)
        {
            return new ConfigDescription(description, null, new ConfigurationManagerAttributes { Order = order });
        }

        private static ConfigDescription FeatureSliderDescription(string description, int order)
        {
            return new ConfigDescription(description, new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = order });
        }
    }
}
