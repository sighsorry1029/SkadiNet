using System;
using BepInEx.Configuration;

namespace SkadiNet
{
    internal static class EffectiveConfig
    {
        internal static bool SchedulerEnabled => ModConfig.Enabled.Value && IsPositive(ModConfig.SchedulerThroughput);
        internal static bool PayloadReducerEnabled => ModConfig.Enabled.Value && IsPositive(ModConfig.PayloadReducerStrength);
        internal static bool CompressionEnabled => ModConfig.Enabled.Value && IsPositive(ModConfig.CompressionAggression);
        internal static bool RpcAoiEnabled => ModConfig.Enabled.Value && IsPositive(ModConfig.RpcAoiAggression);
        internal static bool ClientStutterGuardEnabled => ModConfig.Enabled.Value && IsPositive(ModConfig.ClientStutterGuardStrength);
        internal static bool AdaptiveOwnershipEnabled => ModConfig.Enabled.Value && IsPositive(ModConfig.OwnershipIntensity);
        internal static bool PeerQualityEnabled => AdaptiveOwnershipEnabled;
        internal static bool OwnerHintsEnabled => AdaptiveOwnershipEnabled;

        internal static float SendInterval => Map(ModConfig.SchedulerThroughput, 0.10f, 0.05f, 0.02f);
        internal static float MinSendInterval => Map(ModConfig.SchedulerThroughput, 0.06f, 0.03f, 0.015f);
        internal static float MaxSendInterval => Map(ModConfig.SchedulerThroughput, 0.20f, 0.10f, 0.045f);
        internal static int BasePeersPerTick => MapInt(ModConfig.SchedulerThroughput, 1, 4, 12);
        internal static int MaxPeersPerTick => MapInt(ModConfig.SchedulerThroughput, 4, 12, 32);
        internal static int ZdoQueueLimitBytes => MapInt(ModConfig.SchedulerThroughput, 10 * 1024, 64 * 1024, 192 * 1024);
        internal static int ZdoQueueMinPackageBytes => MapInt(ModConfig.SchedulerThroughput, 2048, 2048, 768);
        // Fixed baseline policy values. These are not slider-derived tuning knobs.
        internal const int SteamSendRateBytes = 36_000_000;
        internal const int CompressionFailureLimitPerPeer = 1;

        internal static int PeerQueueSoftLimitBytes => MapInt(ModConfig.SchedulerThroughput, 1536 * 1024, 512 * 1024, 192 * 1024);
        internal static int PeerQueueHardLimitBytes => MapInt(ModConfig.SchedulerThroughput, 6 * 1024 * 1024, 2 * 1024 * 1024, 768 * 1024);
        internal static float LaggingPeerMaxSkipSeconds => Map(ModConfig.SchedulerThroughput, 3.0f, 1.0f, 0.20f);

        internal static float PayloadVec3CullSize => Map(ModConfig.PayloadReducerStrength, 0.005f, 0.04f, 0.10f);
        internal static float PayloadQuaternionDotThreshold => Map(ModConfig.PayloadReducerStrength, 0.9998f, 0.995f, 0.990f);
        internal static float PayloadForceRefreshSeconds => Map(ModConfig.PayloadReducerStrength, 0.15f, 1.0f, 2.0f);

        internal static int CompressionThresholdBytes => MapInt(ModConfig.CompressionAggression, 8192, 1024, 256);
        internal static float CompressionMinUsefulRatio => Map(ModConfig.CompressionAggression, 0.70f, 0.90f, 0.97f);

        internal static float RpcAoiVisualRadius => Map(ModConfig.RpcAoiAggression, 640f, 192f, 64f);

        internal static float ClientStutterInitialSyncWindowSeconds => Map(ModConfig.ClientStutterGuardStrength, 2.0f, 10.0f, 24.0f);
        internal static float ClientStutterTeleportWindowSeconds => Map(ModConfig.ClientStutterGuardStrength, 1.0f, 5.0f, 14.0f);
        internal static float ClientStutterFullSnapshotWindowSeconds => Map(ModConfig.ClientStutterGuardStrength, 0.25f, 1.5f, 5.0f);
        internal static float ClientStutterCombatWindowSeconds => Map(ModConfig.ClientStutterGuardStrength, 0.35f, 2.0f, 6.0f);
        internal static float ClientStutterShipWindowSeconds => Map(ModConfig.ClientStutterGuardStrength, 0.35f, 2.0f, 6.0f);
        internal static float ClientStutterMaxDelaySeconds => Map(ModConfig.ClientStutterGuardStrength, 6.0f, 30.0f, 60.0f);
        internal static int ClientStutterMemoryPressureThresholdPercent => MapInt(ModConfig.ClientStutterGuardStrength, 55, 75, 90);
        internal static int ClientStutterMinimumFreeMemoryMB => MapInt(ModConfig.ClientStutterGuardStrength, 6144, 2048, 1024);
        internal static float ClientStutterIdleCleanupPollSeconds => Map(ModConfig.ClientStutterGuardStrength, 0.25f, 1.0f, 4.0f);

        internal static float PeerPingEmaHalfLifeSeconds => Map(ModConfig.OwnershipIntensity, 6.0f, 2.5f, 0.75f);
        internal static int PeerPingSampleWindow => MapInt(ModConfig.OwnershipIntensity, 120, 60, 20);
        internal static float PeerQualityMeanWeight => Map(ModConfig.OwnershipIntensity, 0.35f, 0.0f, 0.0f);
        internal static float PeerQualityStdDevWeight => Map(ModConfig.OwnershipIntensity, 0.10f, 0.25f, 0.70f);
        internal static float PeerQualityJitterWeight => Map(ModConfig.OwnershipIntensity, 0.20f, 0.50f, 1.20f);
        internal static float PeerQualityEmaWeight => 1.0f;
        internal static float MaxCandidatePingMs => Map(ModConfig.OwnershipIntensity, 320.0f, 220.0f, 140.0f);
        internal static float MaxCandidateJitterMs => Map(ModConfig.OwnershipIntensity, 180.0f, 100.0f, 45.0f);

        internal static int OwnershipScanBudget => MapInt(ModConfig.OwnershipIntensity, 24, 96, 256);
        internal static int OwnershipScanStride => MapInt(ModConfig.OwnershipIntensity, 10, 4, 1);
        internal static float OwnershipScanIntervalSeconds => Map(ModConfig.OwnershipIntensity, 3.0f, 1.0f, 0.25f);

        internal static float OwnershipRelativeHysteresis => Map(ModConfig.OwnershipIntensity, 0.03f, 0.15f, 0.40f);
        internal static float OwnershipAbsoluteHysteresisMs => Map(ModConfig.OwnershipIntensity, 5.0f, 20.0f, 90.0f);
        internal static float OwnerSwitchCooldownSeconds => Map(ModConfig.OwnershipIntensity, 0.75f, 3.0f, 12.0f);
        internal static float OwnerHintSwitchCooldownSeconds => Map(ModConfig.OwnershipIntensity, 1.0f, 5.0f, 16.0f);
        internal static float ShipOwnerSwitchCooldownSeconds => Map(ModConfig.OwnershipIntensity, 3.0f, 8.0f, 24.0f);
        internal static float RecoverUnownedAfterSeconds => Map(ModConfig.OwnershipIntensity, 0.75f, 2.0f, 6.0f);

        internal static float OwnershipCandidateRadius => Map(ModConfig.OwnershipIntensity, 80.0f, 160.0f, 360.0f);
        internal static float OwnerHintCandidateRadius => Map(ModConfig.OwnershipIntensity, 128.0f, 256.0f, 640.0f);
        internal static float OwnershipDistanceScoreWeight => Map(ModConfig.OwnershipIntensity, 0.40f, 0.20f, 0.06f);
        internal static float OwnershipLoadPenaltyPerZdo => Map(ModConfig.OwnershipIntensity, 0.50f, 0.35f, 0.15f);
        internal static float ServerFallbackPenaltyMs => Map(ModConfig.OwnershipIntensity, 450.0f, 650.0f, 1000.0f);

        internal static float OwnerHintScoreBonusMs => Map(ModConfig.OwnershipIntensity, 20.0f, 90.0f, 240.0f);
        internal static float OwnerHintLifetimeSeconds => Map(ModConfig.OwnershipIntensity, 2.0f, 8.0f, 20.0f);

        private static bool IsPositive(ConfigEntry<int> entry)
        {
            return Clamp(entry?.Value ?? 0, 0, 100) > 0;
        }

        private static float Strength(ConfigEntry<int> entry)
        {
            int value = Clamp(entry?.Value ?? 50, 0, 100);
            return value / 100f;
        }

        private static float Map(ConfigEntry<int> entry, float safe, float current, float aggressive)
        {
            float t = Strength(entry);
            return t <= 0.5f
                ? Lerp(safe, current, t * 2f)
                : Lerp(current, aggressive, (t - 0.5f) * 2f);
        }

        private static int MapInt(ConfigEntry<int> entry, int safe, int current, int aggressive)
        {
            return (int)Math.Round(Map(entry, safe, current, aggressive));
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return min;
            return Math.Max(min, Math.Min(max, value));
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Max(0f, Math.Min(1f, t));
        }
    }
}
