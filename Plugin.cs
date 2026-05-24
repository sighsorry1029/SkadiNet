using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SkadiNet
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "sighsorry.SkadiNet";
        public const string PluginName = "SkadiNet";
        public const string PluginVersion = "1.0.1";

        internal static ManualLogSource Log;
        private Harmony _harmony;
        internal static Plugin Instance;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigSyncManager.Initialize(Config);
            ModConfig.Bind(Config);
            ReflectionCache.Initialize();
            FeatureNegotiation.Initialize();
            PeerQualityMeter.Initialize();
            ZdoKeyPolicy.Initialize();
            OwnershipManager.Initialize();
            RpcAoiRouter.Initialize();
            ClientStutterGuard.Initialize(this);

            if (!ModConfig.Enabled.Value)
            {
                Log.LogInfo("SkadiNet is disabled by configuration.");
                return;
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Stable core: adaptive scheduler, queue patch, micro-update reducer, peer quality, adaptive client ownership, optional client stutter guard. ServerSync config lock={(ModConfig.LockServerConfig.Value ? "on" : "off")}. Slider-gated features: scheduler={(EffectiveConfig.SchedulerEnabled ? "on" : "off")}, payload reducer={(EffectiveConfig.PayloadReducerEnabled ? "on" : "off")}, compression={(EffectiveConfig.CompressionEnabled ? "on" : "off")}, RPC AoI={(EffectiveConfig.RpcAoiEnabled ? "on" : "off")}, ClientStutterGuard={(EffectiveConfig.ClientStutterGuardEnabled ? "on" : "off")}.");
            LogDebugConfigSnapshot();
        }

        private static void LogDebugConfigSnapshot()
        {
            if (!ModConfig.DebugLogging.Value) return;

            Log.LogDebug(
                $"SkadiNet sliders: SchedulerThroughput={ModConfig.SchedulerThroughput.Value}, PayloadReducerStrength={ModConfig.PayloadReducerStrength.Value}, " +
                $"CompressionAggression={ModConfig.CompressionAggression.Value}, OwnershipIntensity={ModConfig.OwnershipIntensity.Value}, " +
                $"RpcAoiAggression={ModConfig.RpcAoiAggression.Value}, ClientStutterGuardStrength={ModConfig.ClientStutterGuardStrength.Value}");

            Log.LogDebug(
                $"SkadiNet scheduler effective: interval={EffectiveConfig.SendInterval:F3}s min={EffectiveConfig.MinSendInterval:F3}s max={EffectiveConfig.MaxSendInterval:F3}s " +
                $"peersPerTick={EffectiveConfig.BasePeersPerTick}-{EffectiveConfig.MaxPeersPerTick} " +
                $"zdoQueueLimit={FormatBytes(EffectiveConfig.ZdoQueueLimitBytes)} minPackage={FormatBytes(EffectiveConfig.ZdoQueueMinPackageBytes)} " +
                $"peerQueueSoft={FormatBytes(EffectiveConfig.PeerQueueSoftLimitBytes)} peerQueueHard={FormatBytes(EffectiveConfig.PeerQueueHardLimitBytes)} " +
                $"lagBackfill={EffectiveConfig.LaggingPeerMaxSkipSeconds:F2}s fixedSteamRate={FormatBytes(EffectiveConfig.SteamSendRateBytes)}/s");

            Log.LogDebug(
                $"SkadiNet other effective: payloadVec3={EffectiveConfig.PayloadVec3CullSize:F4} payloadQuatDot={EffectiveConfig.PayloadQuaternionDotThreshold:F4} payloadRefresh={EffectiveConfig.PayloadForceRefreshSeconds:F2}s " +
                $"compressionThreshold={FormatBytes(EffectiveConfig.CompressionThresholdBytes)} compressionRatio={EffectiveConfig.CompressionMinUsefulRatio:F2} " +
                $"ownershipScanBudget={EffectiveConfig.OwnershipScanBudget} ownershipScanInterval={EffectiveConfig.OwnershipScanIntervalSeconds:F2}s ownershipCandidateRadius={EffectiveConfig.OwnershipCandidateRadius:F0} " +
                $"rpcAoiRadius={EffectiveConfig.RpcAoiVisualRadius:F0}");
        }

        private static string FormatBytes(int bytes)
        {
            if (bytes >= 1024 * 1024) return $"{bytes / (1024f * 1024f):F1}MB";
            if (bytes >= 1024) return $"{bytes / 1024f:F1}KB";
            return $"{bytes}B";
        }

        private void Update()
        {
            FrameHitchDiagnostics.Update();
        }

        private void OnDestroy()
        {
            try
            {
                ClientStutterGuard.Shutdown();
                _harmony?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"Failed to unpatch cleanly: {ex}");
            }
            finally
            {
                if (ReferenceEquals(Instance, this)) Instance = null;
            }
        }
    }
}
