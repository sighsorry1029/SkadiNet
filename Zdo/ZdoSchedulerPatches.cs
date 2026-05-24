using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkadiNet
{
    [HarmonyPatch]
    internal static class ZDOManSendSchedulerPatch
    {
        private const double SummaryIntervalSeconds = 10.0;

        private sealed class LaggingPeerSkipState
        {
            public double FirstSkipTime;
            public double LastBackfillTime;
            public int SkipCount;
        }

        private static readonly Dictionary<long, LaggingPeerSkipState> LaggingPeerSkips = new Dictionary<long, LaggingPeerSkipState>();
        private static double _nextSummaryTime;
        private static int _summaryPasses;
        private static int _summaryAttempted;
        private static int _summarySent;
        private static int _summarySkipped;
        private static int _summaryBackfills;
        private static double _summaryElapsedSeconds;
        private static double _summaryMaxPassSeconds;
        private static double _summaryMaxPeerSeconds;
        private static long _summaryMaxPeerUid;
        private static int _summaryMaxQueueBefore;
        private static int _summaryMaxQueueAfter;
        private static int _lastPeerCount;
        private static int _lastPressuredPeers;
        private static int _lastMaxQueue;
        private static float _lastInterval;
        private static double _lastPassTime;
        private static double _lastPassSeconds;
        private static double _lastMaxPeerSeconds;
        private static int _lastBatch;
        private static int _lastAttempted;
        private static int _lastSent;
        private static int _lastSkipped;
        private static int _lastBackfilled;
        private static long _lastMaxPeerUid;
        private static int _lastMaxPeerQueueBefore;
        private static int _lastMaxPeerQueueAfter;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(ReflectionCache.ZDOManType ?? AccessTools.TypeByName("ZDOMan"), "SendZDOToPeers2", new[] { typeof(float) });
        }

        internal static void ClearPeer(long uid)
        {
            if (uid == 0) return;
            LaggingPeerSkips.Remove(uid);
        }

        internal static string DescribeRecentState()
        {
            double age = _lastPassTime > 0 ? Math.Max(0.0, UnityEngine.Time.realtimeSinceStartupAsDouble - _lastPassTime) : -1.0;
            string ageText = age >= 0 ? $"{age:F2}s" : "n/a";
            return
                $"schedulerRecent age={ageText} passMs={_lastPassSeconds * 1000.0:F2} maxPeerMs={_lastMaxPeerSeconds * 1000.0:F2} " +
                $"peers={_lastPeerCount} pressured={_lastPressuredPeers} maxQueue={FormatBytes(_lastMaxQueue)} interval={_lastInterval:F3}s " +
                $"batch={_lastBatch} attempted={_lastAttempted} sent={_lastSent} skipped={_lastSkipped} backfills={_lastBackfilled} " +
                $"maxPeer={_lastMaxPeerUid} queue={FormatBytes(_lastMaxPeerQueueBefore)}->{FormatBytes(_lastMaxPeerQueueAfter)}";
        }

        private static bool Prefix(object __instance, float __0)
        {
            if (!EffectiveConfig.SchedulerEnabled) return true;
            if (!NetReflection.IsServer()) return true;
            if (__instance == null || ZdoReflection.SendZDOsMethod == null || ReflectionCache.ZDOManPeersField == null) return true;

            try
            {
                float dt = __0;
                float timer = 0f;
                if (ReflectionCache.ZDOManSendTimerField != null)
                    timer = (float)ReflectionCache.ZDOManSendTimerField.GetValue(__instance);

                timer += dt;
                float interval = ComputeAdaptiveInterval(__instance);
                _lastInterval = interval;
                if (timer < interval)
                {
                    ReflectionCache.ZDOManSendTimerField?.SetValue(__instance, timer);
                    return false;
                }

                ReflectionCache.ZDOManSendTimerField?.SetValue(__instance, 0f);
                SendToPeerBatch(__instance);
                OwnershipManager.TickLightweight(__instance);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Adaptive scheduler failed; falling back to vanilla for this tick: {ex.Message}");
                return true;
            }
        }

        private static float ComputeAdaptiveInterval(object zdoMan)
        {
            int peerCount = 0;
            int pressured = 0;
            int maxQueue = 0;
            foreach (object peer in ZdoReflection.EnumeratePeers(zdoMan))
            {
                peerCount++;
                int queue = NetReflection.GetSendQueueSizeForPeer(peer);
                if (queue > EffectiveConfig.PeerQueueSoftLimitBytes) pressured++;
                if (queue > maxQueue) maxQueue = queue;
            }

            float baseInterval = EffectiveConfig.SendInterval;
            _lastPeerCount = peerCount;
            _lastPressuredPeers = pressured;
            _lastMaxQueue = maxQueue;
            if (peerCount == 0) return baseInterval;

            float pressureRatio = pressured / (float)Math.Max(1, peerCount);
            if (pressureRatio > 0.5f) return Math.Min(EffectiveConfig.MaxSendInterval, baseInterval * 1.5f);
            if (pressureRatio < 0.1f && peerCount > 4) return Math.Max(EffectiveConfig.MinSendInterval, baseInterval * 0.75f);
            return baseInterval;
        }

        private static void SendToPeerBatch(object zdoMan)
        {
            IList peers = ReflectionCache.ZDOManPeersField.GetValue(zdoMan) as IList;
            if (peers == null || peers.Count == 0) return;

            int next = 0;
            if (ReflectionCache.ZDOManNextSendPeerField != null)
            {
                try { next = (int)ReflectionCache.ZDOManNextSendPeerField.GetValue(zdoMan); }
                catch { next = 0; }
            }
            if (next < 0 || next >= peers.Count) next = 0;

            int batch = ComputePeerBatchSize(peers.Count);
            int attempted = 0;
            int sent = 0;
            int skipped = 0;
            int backfilled = 0;
            double started = UnityEngine.Time.realtimeSinceStartupAsDouble;
            double maxPeerSeconds = 0;
            long maxPeerUid = 0;
            int maxQueueBefore = 0;
            int maxQueueAfter = 0;

            while (attempted < peers.Count && sent < batch)
            {
                if (next >= peers.Count) next = 0;
                object peer = peers[next];
                next++;
                attempted++;

                int queue = NetReflection.GetSendQueueSizeForPeer(peer);
                if (ShouldSkipLaggingPeer(peer, queue, out bool wasBackfill))
                {
                    skipped++;
                    continue;
                }

                if (wasBackfill)
                    backfilled++;

                PeerQualityMeter.GetByPeer(peer); // ensure uid/rpc map exists
                NetReflection.TryGetPeerUid(peer, out long uid);
                double peerStarted = UnityEngine.Time.realtimeSinceStartupAsDouble;
                ZdoReflection.SendZDOsMethod.Invoke(zdoMan, new[] { peer, (object)false });
                sent++;
                double peerSeconds = UnityEngine.Time.realtimeSinceStartupAsDouble - peerStarted;
                int queueAfter = NetReflection.GetSendQueueSizeForPeer(peer);
                if (peerSeconds > maxPeerSeconds)
                {
                    maxPeerSeconds = peerSeconds;
                    maxPeerUid = uid;
                    maxQueueBefore = queue;
                    maxQueueAfter = queueAfter;
                }
            }

            if (ReflectionCache.ZDOManNextSendPeerField != null)
            {
                try { ReflectionCache.ZDOManNextSendPeerField.SetValue(zdoMan, next % Math.Max(1, peers.Count)); }
                catch { }
            }

            double elapsed = UnityEngine.Time.realtimeSinceStartupAsDouble - started;
            if (ModConfig.DebugLogging.Value)
                RecordSchedulerDiagnostics(peers.Count, batch, attempted, sent, skipped, backfilled, elapsed, maxPeerSeconds, maxPeerUid, maxQueueBefore, maxQueueAfter);
        }

        private static bool ShouldSkipLaggingPeer(object peer, int queue, out bool wasBackfill)
        {
            wasBackfill = false;
            if (queue <= EffectiveConfig.PeerQueueHardLimitBytes)
            {
                if (NetReflection.TryGetPeerUid(peer, out long healthyUid))
                    LaggingPeerSkips.Remove(healthyUid);
                return false;
            }

            if (!NetReflection.TryGetPeerUid(peer, out long uid) || uid == 0)
                return true;

            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            float maxSkipSeconds = Math.Max(0.1f, EffectiveConfig.LaggingPeerMaxSkipSeconds);
            if (!LaggingPeerSkips.TryGetValue(uid, out LaggingPeerSkipState state))
            {
                state = new LaggingPeerSkipState { FirstSkipTime = now };
                LaggingPeerSkips[uid] = state;
                return true;
            }

            state.SkipCount++;
            if (now - state.FirstSkipTime >= maxSkipSeconds && now - state.LastBackfillTime >= maxSkipSeconds)
            {
                state.FirstSkipTime = now;
                state.LastBackfillTime = now;
                state.SkipCount = 0;
                if (ModConfig.DebugLogging.Value)
                    Plugin.Log.LogDebug($"ZDO scheduler backfill for lagging peer={uid}, queue={queue}");
                wasBackfill = true;
                return false;
            }

            return true;
        }

        private static void RecordSchedulerDiagnostics(
            int peers,
            int batch,
            int attempted,
            int sent,
            int skipped,
            int backfilled,
            double elapsed,
            double maxPeerSeconds,
            long maxPeerUid,
            int maxQueueBefore,
            int maxQueueAfter)
        {
            _summaryPasses++;
            _summaryAttempted += attempted;
            _summarySent += sent;
            _summarySkipped += skipped;
            _summaryBackfills += backfilled;
            _summaryElapsedSeconds += elapsed;
            if (elapsed > _summaryMaxPassSeconds) _summaryMaxPassSeconds = elapsed;
            if (maxPeerSeconds > _summaryMaxPeerSeconds)
            {
                _summaryMaxPeerSeconds = maxPeerSeconds;
                _summaryMaxPeerUid = maxPeerUid;
                _summaryMaxQueueBefore = maxQueueBefore;
                _summaryMaxQueueAfter = maxQueueAfter;
            }

            _lastPassTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
            _lastPassSeconds = elapsed;
            _lastMaxPeerSeconds = maxPeerSeconds;
            _lastBatch = batch;
            _lastAttempted = attempted;
            _lastSent = sent;
            _lastSkipped = skipped;
            _lastBackfilled = backfilled;
            _lastMaxPeerUid = maxPeerUid;
            _lastMaxPeerQueueBefore = maxQueueBefore;
            _lastMaxPeerQueueAfter = maxQueueAfter;

            double slowThreshold = 0.008;
            if (elapsed >= slowThreshold)
            {
                Plugin.Log.LogInfo(
                    $"ZDO scheduler slow pass: elapsed={elapsed * 1000.0:F2}ms " +
                    $"peers={peers} batch={batch} attempted={attempted} sent={sent} skipped={skipped} backfills={backfilled} " +
                    $"maxPeer={maxPeerUid} maxPeerMs={maxPeerSeconds * 1000.0:F2} queue={maxQueueBefore}->{maxQueueAfter} " +
                    $"pressure={_lastPressuredPeers}/{_lastPeerCount} maxQueue={_lastMaxQueue} interval={_lastInterval:F3}s");
            }

            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (now < _nextSummaryTime) return;
            _nextSummaryTime = now + SummaryIntervalSeconds;

            if (_summaryPasses > 0)
            {
                Plugin.Log.LogInfo(
                    $"ZDO scheduler summary {SummaryIntervalSeconds:F0}s: passes={_summaryPasses} attempted={_summaryAttempted} sent={_summarySent} " +
                    $"skipped={_summarySkipped} backfills={_summaryBackfills} " +
                    $"avgPassMs={(_summaryElapsedSeconds / Math.Max(1, _summaryPasses)) * 1000.0:F2} maxPassMs={_summaryMaxPassSeconds * 1000.0:F2} " +
                    $"maxPeer={_summaryMaxPeerUid} maxPeerMs={_summaryMaxPeerSeconds * 1000.0:F2} queue={_summaryMaxQueueBefore}->{_summaryMaxQueueAfter} " +
                    $"lastPressure={_lastPressuredPeers}/{_lastPeerCount} lastMaxQueue={_lastMaxQueue}");
            }

            _summaryPasses = 0;
            _summaryAttempted = 0;
            _summarySent = 0;
            _summarySkipped = 0;
            _summaryBackfills = 0;
            _summaryElapsedSeconds = 0;
            _summaryMaxPassSeconds = 0;
            _summaryMaxPeerSeconds = 0;
            _summaryMaxPeerUid = 0;
            _summaryMaxQueueBefore = 0;
            _summaryMaxQueueAfter = 0;
        }

        private static int ComputePeerBatchSize(int peerCount)
        {
            int baseBatch = Math.Max(1, EffectiveConfig.BasePeersPerTick);
            int maxBatch = Math.Max(baseBatch, EffectiveConfig.MaxPeersPerTick);
            if (peerCount <= baseBatch) return peerCount;
            int scaled = baseBatch + Math.Max(0, peerCount - 4) / 4;
            return Math.Max(1, Math.Min(peerCount, Math.Min(maxBatch, scaled)));
        }

        private static string FormatBytes(int bytes)
        {
            if (bytes >= 1024 * 1024) return $"{bytes / (1024f * 1024f):F1}MB";
            if (bytes >= 1024) return $"{bytes / 1024f:F1}KB";
            return $"{bytes}B";
        }
    }

    [HarmonyPatch]
    internal static class ZDOManSendZDOsQueueLimitTranspiler
    {
        private static MethodBase TargetMethod()
        {
            Type zdoMan = ReflectionCache.ZDOManType ?? AccessTools.TypeByName("ZDOMan");
            if (zdoMan == null) return null;
            foreach (MethodBase method in zdoMan.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name == "SendZDOs" && method.GetParameters().Length == 2) return method;
            }
            return null;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int limitMatches = CountIntConstant(codes, 10240);
            int minPackageMatches = CountIntConstant(codes, 2048);

            if (limitMatches != 1 || minPackageMatches != 1)
            {
                Plugin.Log?.LogWarning($"Skipped SendZDOs queue-limit transpiler: expected one 10240 and one 2048 constant, found {limitMatches} and {minPackageMatches}.");
                return codes;
            }

            for (int index = 0; index < codes.Count; index++)
            {
                CodeInstruction code = codes[index];
                if (code.opcode == OpCodes.Ldc_I4 && code.operand is int i)
                {
                    if (i == 10240)
                    {
                        codes[index] = Replacement(code, nameof(ZdoQueueLimit));
                        continue;
                    }
                    if (i == 2048)
                    {
                        codes[index] = Replacement(code, nameof(ZdoQueueMinPackage));
                        continue;
                    }
                }
            }

            return codes;
        }

        private static int CountIntConstant(IEnumerable<CodeInstruction> codes, int value)
        {
            int count = 0;
            foreach (CodeInstruction code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4 && code.operand is int i && i == value)
                    count++;
            }
            return count;
        }

        private static CodeInstruction Replacement(CodeInstruction original, string propertyName)
        {
            var replacement = new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ZDOManSendZDOsQueueLimitTranspiler), propertyName));
            replacement.labels.AddRange(original.labels);
            replacement.blocks.AddRange(original.blocks);
            return replacement;
        }

        public static int ZdoQueueLimit => EffectiveConfig.SchedulerEnabled ? Math.Max(4096, EffectiveConfig.ZdoQueueLimitBytes) : 10240;
        public static int ZdoQueueMinPackage => EffectiveConfig.SchedulerEnabled ? Math.Max(512, EffectiveConfig.ZdoQueueMinPackageBytes) : 2048;
    }
}
