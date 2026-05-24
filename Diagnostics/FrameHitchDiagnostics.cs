using System;
using System.Collections;
using UnityEngine;

namespace SkadiNet
{
    internal static class FrameHitchDiagnostics
    {
        private const double HitchThresholdSeconds = 0.12;
        private const double SevereHitchThresholdSeconds = 0.25;
        private const double SummaryIntervalSeconds = 10.0;
        private const double HitchLogCooldownSeconds = 0.75;
        private const int MaxZdoObjectsSnapshotCount = 50000;

        private static double _lastRealtime;
        private static double _lastHitchLogTime;
        private static double _nextSummaryTime;
        private static double _summarySeconds;
        private static int _summaryFrames;
        private static int _summaryHitches;
        private static int _summarySevereHitches;
        private static double _summaryMaxFrameSeconds;

        internal static void Update()
        {
            if (!DiagnosticsEnabled()) return;

            double now = Time.realtimeSinceStartupAsDouble;
            if (_lastRealtime <= 0)
            {
                _lastRealtime = now;
                _nextSummaryTime = now + SummaryIntervalSeconds;
                return;
            }

            double realtimeDelta = Math.Max(0.0, now - _lastRealtime);
            _lastRealtime = now;
            double frameDelta = Math.Max(Time.unscaledDeltaTime, realtimeDelta);

            _summaryFrames++;
            _summarySeconds += frameDelta;
            if (frameDelta > _summaryMaxFrameSeconds) _summaryMaxFrameSeconds = frameDelta;
            if (frameDelta >= HitchThresholdSeconds) _summaryHitches++;
            if (frameDelta >= SevereHitchThresholdSeconds) _summarySevereHitches++;

            if (frameDelta >= HitchThresholdSeconds && now - _lastHitchLogTime >= HitchLogCooldownSeconds)
            {
                _lastHitchLogTime = now;
                LogFrameSnapshot(frameDelta, realtimeDelta, frameDelta >= SevereHitchThresholdSeconds ? "severe" : "mild");
            }

            if (now >= _nextSummaryTime)
            {
                LogSummary();
                _nextSummaryTime = now + SummaryIntervalSeconds;
            }
        }

        private static bool DiagnosticsEnabled()
        {
            try
            {
                return Plugin.Log != null
                       && ModConfig.Enabled != null && ModConfig.Enabled.Value
                       && ModConfig.DebugLogging != null && ModConfig.DebugLogging.Value;
            }
            catch { return false; }
        }

        private static void LogFrameSnapshot(double frameDelta, double realtimeDelta, string severity)
        {
            NetworkSnapshot network = CaptureNetworkSnapshot();
            string scheduler = ZDOManSendSchedulerPatch.DescribeRecentState();
            string ownership = OwnershipManager.DescribeRecentState();
            Plugin.Log.LogInfo(
                $"Frame hitch {severity}: frameMs={frameDelta * 1000.0:F1} realtimeGapMs={realtimeDelta * 1000.0:F1} " +
                $"instantFps={SafeFps(frameDelta):F1} avgFps10s={AverageFps():F1} " +
                $"{network} {scheduler} {ownership} sliders[scheduler={Value(ModConfig.SchedulerThroughput)} payload={Value(ModConfig.PayloadReducerStrength)} " +
                $"compression={Value(ModConfig.CompressionAggression)} ownership={Value(ModConfig.OwnershipIntensity)} rpcAoi={Value(ModConfig.RpcAoiAggression)} " +
                $"stutterGuard={Value(ModConfig.ClientStutterGuardStrength)}]");
        }

        private static void LogSummary()
        {
            if (_summaryFrames <= 0) return;

            NetworkSnapshot network = CaptureNetworkSnapshot();
            Plugin.Log.LogInfo(
                $"Frame diagnostics {SummaryIntervalSeconds:F0}s: frames={_summaryFrames} avgFps={AverageFps():F1} " +
                $"maxFrameMs={_summaryMaxFrameSeconds * 1000.0:F1} hitches>={HitchThresholdSeconds * 1000.0:F0}ms={_summaryHitches} " +
                $"severe>={SevereHitchThresholdSeconds * 1000.0:F0}ms={_summarySevereHitches} {network}");

            _summaryFrames = 0;
            _summarySeconds = 0;
            _summaryHitches = 0;
            _summarySevereHitches = 0;
            _summaryMaxFrameSeconds = 0;
        }

        private static double AverageFps()
        {
            return _summarySeconds > 0 ? _summaryFrames / _summarySeconds : 0;
        }

        private static double SafeFps(double frameDelta)
        {
            return frameDelta > 0 ? 1.0 / frameDelta : 0;
        }

        private static int Value(BepInEx.Configuration.ConfigEntry<int> entry)
        {
            try { return entry?.Value ?? 0; }
            catch { return 0; }
        }

        private static NetworkSnapshot CaptureNetworkSnapshot()
        {
            NetworkSnapshot snapshot = new NetworkSnapshot
            {
                IsServer = NetReflection.IsServer(),
                IsDedicated = NetReflection.IsDedicatedServer()
            };

            object zdoMan = ZdoReflection.ZDOManInstance;
            foreach (object peer in ZdoReflection.EnumeratePeers(zdoMan))
            {
                snapshot.ZdoPeers++;
                int queue = NetReflection.GetSendQueueSizeForPeer(peer);
                snapshot.QueueTotalBytes += queue;
                if (queue > snapshot.QueueMaxBytes) snapshot.QueueMaxBytes = queue;
                if (queue > EffectiveConfig.PeerQueueSoftLimitBytes) snapshot.QueueSoftPeers++;
                if (queue > EffectiveConfig.PeerQueueHardLimitBytes) snapshot.QueueHardPeers++;
            }

            foreach (object _ in NetReflection.EnumerateZNetPeers())
            {
                snapshot.ZNetPeers++;
            }

            CountZdoSectors(zdoMan, ref snapshot);
            snapshot.ManagedMemoryBytes = GC.GetTotalMemory(false);
            return snapshot;
        }

        private static void CountZdoSectors(object zdoMan, ref NetworkSnapshot snapshot)
        {
            try
            {
                if (zdoMan == null || ReflectionCache.ZDOObjectsBySectorField == null) return;
                object raw = ReflectionCache.ZDOObjectsBySectorField.GetValue(zdoMan);
                if (raw == null) return;

                if (raw is IDictionary dictionary)
                {
                    snapshot.ZdoSectors = dictionary.Count;
                    foreach (object values in dictionary.Values)
                    {
                        int remaining = MaxZdoObjectsSnapshotCount - snapshot.ZdoObjectsApprox;
                        if (remaining <= 0)
                        {
                            snapshot.ZdoObjectCountCapped = true;
                            break;
                        }
                        snapshot.ZdoObjectsApprox += CountEnumerable(values, remaining, ref snapshot.ZdoObjectCountCapped);
                    }
                    return;
                }

                if (raw is IEnumerable sectors)
                {
                    foreach (object sector in sectors)
                    {
                        snapshot.ZdoSectors++;
                        int remaining = MaxZdoObjectsSnapshotCount - snapshot.ZdoObjectsApprox;
                        if (remaining <= 0)
                        {
                            snapshot.ZdoObjectCountCapped = true;
                            break;
                        }
                        snapshot.ZdoObjectsApprox += CountSectorEntry(sector, remaining, ref snapshot.ZdoObjectCountCapped);
                    }
                }
            }
            catch { }
        }

        private static int CountSectorEntry(object entry, int cap, ref bool capped)
        {
            if (entry == null) return 0;
            try
            {
                object value = entry;
                Type type = entry.GetType();
                if (type.IsGenericType && type.FullName != null && type.FullName.StartsWith("System.Collections.Generic.KeyValuePair", StringComparison.Ordinal))
                {
                    value = type.GetProperty("Value")?.GetValue(entry, null) ?? entry;
                }
                return CountEnumerable(value, cap, ref capped);
            }
            catch { return 0; }
        }

        private static int CountEnumerable(object value, int cap, ref bool capped)
        {
            if (value == null) return 0;
            if (value is ICollection collection)
            {
                if (collection.Count > cap) capped = true;
                return Math.Min(collection.Count, cap);
            }
            if (!(value is IEnumerable enumerable)) return 0;

            int count = 0;
            foreach (object _ in enumerable)
            {
                count++;
                if (count >= cap)
                {
                    capped = true;
                    break;
                }
            }
            return count;
        }

        private struct NetworkSnapshot
        {
            public bool IsServer;
            public bool IsDedicated;
            public int ZNetPeers;
            public int ZdoPeers;
            public int QueueTotalBytes;
            public int QueueMaxBytes;
            public int QueueSoftPeers;
            public int QueueHardPeers;
            public int ZdoSectors;
            public int ZdoObjectsApprox;
            public bool ZdoObjectCountCapped;
            public long ManagedMemoryBytes;

            public override string ToString()
            {
                string capped = ZdoObjectCountCapped ? "+" : "";
                return
                    $"network[server={IsServer} dedicated={IsDedicated} znetPeers={ZNetPeers} zdoPeers={ZdoPeers} " +
                    $"queueTotal={FormatBytes(QueueTotalBytes)} queueMax={FormatBytes(QueueMaxBytes)} queueSoftPeers={QueueSoftPeers} queueHardPeers={QueueHardPeers} " +
                    $"zdoSectors={ZdoSectors} zdoObjects~={ZdoObjectsApprox}{capped} managedMem={FormatBytes(ManagedMemoryBytes)}]";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return $"{bytes / (1024f * 1024f * 1024f):F2}GB";
            if (bytes >= 1024L * 1024L) return $"{bytes / (1024f * 1024f):F1}MB";
            if (bytes >= 1024L) return $"{bytes / 1024f:F1}KB";
            return $"{bytes}B";
        }
    }
}
