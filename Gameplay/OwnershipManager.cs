using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SkadiNet
{
    internal enum OwnershipCandidateReason
    {
        Generic,
        CombatTarget,
        DisconnectedOwner,
        LongUnownedPersistent
    }

    internal sealed class OwnerState
    {
        public long CurrentOwner;
        public long PreviousOwner;
        public double LastOwnerChangeTime;
        public double LastCombatOwnerChangeTime;
        public double LastSeenUnownedTime;
        public double LastTouchedTime;
        public int ChangeCount;
        public long CombatTargetUid;
        public double CombatTargetHintTime;
    }

    internal sealed class OwnerCandidate
    {
        public long Uid;
        public object Peer;
        public float Distance;
        public float Quality;
        public float Score;
        public int EstimatedLoad;
        public bool IsCombatTarget;
    }

    internal static class OwnershipManager
    {
        private const int MaxOwnerStates = 50000;
        private const double OwnerStateTtlSeconds = 600.0;
        private const double OwnerStatePruneIntervalSeconds = 30.0;
        private const bool AllowShipOwnership = false;
        private const bool AllowHealthyOwnerChallenge = false;
        private const bool AllowServerFallbackForPersistentRecovery = true;

        private static readonly Dictionary<ZdoIdKey, OwnerState> ByZdoId = new Dictionary<ZdoIdKey, OwnerState>();
        private static readonly Dictionary<long, int> OwnerLoadEstimate = new Dictionary<long, int>();
        private static double _nextProfileAScan;
        private static double _nextOwnerStatePrune;
        private static int _scanCursor;
        private static int _sectorCursor;
        private static double _lastScanTime;
        private static double _lastScanSeconds;
        private static int _lastScanVisited;
        private static int _lastScanBudget;
        private static int _lastScanOwnerChanges;
        private static int _lastScanPeerCount;
        private static int _lastScanSectorCursor;
        private static bool _lastScanSkippedNoPeers;

        internal static void Initialize()
        {
            ByZdoId.Clear();
            OwnerLoadEstimate.Clear();
            _nextProfileAScan = 0;
            _nextOwnerStatePrune = 0;
            _scanCursor = 0;
            _sectorCursor = 0;
            _lastScanTime = 0;
            _lastScanSeconds = 0;
            _lastScanVisited = 0;
            _lastScanBudget = 0;
            _lastScanOwnerChanges = 0;
            _lastScanPeerCount = 0;
            _lastScanSectorCursor = 0;
            _lastScanSkippedNoPeers = false;
        }

        internal static bool ProfileAEnabled
        {
            get
            {
                if (!EffectiveConfig.AdaptiveOwnershipEnabled || !NetReflection.IsServer()) return false;
                return true;
            }
        }

        internal static bool TryTransferCombatOwnership(object monsterAI, object target)
        {
            if (!ProfileAEnabled || !EffectiveConfig.OwnerHintsEnabled) return false;
            if (!GameplayReflection.LooksLikePlayer(target)) return false;
            if (!GameplayReflection.TryGetPlayerId(target, out long candidateUid) || candidateUid == 0) return false;

            object zdo = GameplayReflection.GetZdoFromCharacterLike(monsterAI);
            if (zdo == null) return false;

            OwnerState state = GetOwnerState(zdo);
            state.CombatTargetUid = candidateUid;
            state.CombatTargetHintTime = Time.realtimeSinceStartupAsDouble;

            return TryMaybeImproveOwner(zdo, OwnershipCandidateReason.CombatTarget);
        }

        internal static void ClearPeer(long uid)
        {
            if (uid == 0) return;

            OwnerLoadEstimate.Remove(uid);
            foreach (OwnerState state in ByZdoId.Values)
            {
                if (state.CombatTargetUid == uid)
                {
                    state.CombatTargetUid = 0;
                    state.CombatTargetHintTime = 0;
                }
            }
        }

        internal static void TickLightweight(object zdoMan)
        {
            if (!ProfileAEnabled) return;
            if (!HasAnyZdoPeer(zdoMan, out int peerCount))
            {
                _lastScanSkippedNoPeers = true;
                _lastScanPeerCount = 0;
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            PruneOwnerStatesIfDue(now);
            if (now < _nextProfileAScan) return;
            _nextProfileAScan = now + Math.Max(0.1f, EffectiveConfig.OwnershipScanIntervalSeconds);
            _lastScanPeerCount = peerCount;
            TryProfileAScan(zdoMan);
        }

        internal static string DescribeRecentState()
        {
            double age = _lastScanTime > 0 ? Math.Max(0.0, Time.realtimeSinceStartupAsDouble - _lastScanTime) : -1.0;
            string ageText = age >= 0 ? $"{age:F2}s" : "n/a";
            return
                $"ownershipRecent age={ageText} scanMs={_lastScanSeconds * 1000.0:F2} visited={_lastScanVisited}/{_lastScanBudget} " +
                $"ownerChanges={_lastScanOwnerChanges} zdoPeers={_lastScanPeerCount} sectorCursor={_lastScanSectorCursor} " +
                $"skippedNoPeers={_lastScanSkippedNoPeers}";
        }

        private static bool HasAnyZdoPeer(object zdoMan, out int peerCount)
        {
            peerCount = 0;
            try
            {
                if (zdoMan == null || ReflectionCache.ZDOManPeersField == null) return false;
                object raw = ReflectionCache.ZDOManPeersField.GetValue(zdoMan);
                if (raw is ICollection collection)
                {
                    peerCount = collection.Count;
                    return peerCount > 0;
                }
                if (raw is IEnumerable enumerable)
                {
                    foreach (object _ in enumerable)
                    {
                        peerCount++;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static void TryProfileAScan(object zdoMan)
        {
            try
            {
                double started = Time.realtimeSinceStartupAsDouble;
                OwnerLoadEstimate.Clear();
                object sectors = ReflectionCache.ZDOObjectsBySectorField?.GetValue(zdoMan);
                if (sectors == null) return;

                int visited = 0;
                int acted = 0;
                int budget = Math.Max(1, EffectiveConfig.OwnershipScanBudget);

                ScanSectorBuckets(sectors, budget, ref visited, ref acted);

                double elapsed = Time.realtimeSinceStartupAsDouble - started;
                _lastScanTime = Time.realtimeSinceStartupAsDouble;
                _lastScanSeconds = elapsed;
                _lastScanVisited = visited;
                _lastScanBudget = budget;
                _lastScanOwnerChanges = acted;
                _lastScanSectorCursor = _sectorCursor;
                _lastScanSkippedNoPeers = false;

                if (ModConfig.DebugLogging.Value && (acted > 0 || visited > 0))
                {
                    bool slow = elapsed >= 0.008;
                    if (acted > 0 || slow)
                        Plugin.Log.LogInfo($"Adaptive ownership scan: elapsed={elapsed * 1000.0:F2}ms slow={slow} visited={visited}/{budget}, ownerChanges={acted}, zdoPeers={_lastScanPeerCount}, loadOwners={OwnerLoadEstimate.Count}, sectorCursor={_sectorCursor}, stride={EffectiveConfig.OwnershipScanStride}");
                }
            }
            catch (Exception ex)
            {
                if (ModConfig.DebugLogging.Value) Plugin.Log.LogWarning($"Adaptive ownership scan failed: {ex.Message}");
            }
        }

        private static void ScanSectorBuckets(object sectors, int budget, ref int visited, ref int acted)
        {
            if (sectors is IList indexedBuckets)
            {
                ScanIndexedBuckets(indexedBuckets, budget, ref visited, ref acted);
                return;
            }

            if (sectors is IDictionary dictionary)
            {
                ScanEnumerableBuckets(dictionary.Values, dictionary.Count, budget, ref visited, ref acted);
                return;
            }

            if (sectors is ICollection collection && sectors is IEnumerable countedEnumerable)
            {
                ScanEnumerableBuckets(countedEnumerable, collection.Count, budget, ref visited, ref acted);
                return;
            }

            if (sectors is IEnumerable enumerable)
                ScanEnumerableBuckets(enumerable, 0, budget, ref visited, ref acted);
        }

        private static void ScanIndexedBuckets(IList buckets, int budget, ref int visited, ref int acted)
        {
            if (buckets == null || buckets.Count <= 0) return;

            if (_sectorCursor < 0 || _sectorCursor >= buckets.Count)
                _sectorCursor = 0;

            int start = _sectorCursor;
            for (int offset = 0; offset < buckets.Count; offset++)
            {
                int index = (start + offset) % buckets.Count;
                if (ScanBucket(buckets[index], budget, ref visited, ref acted))
                {
                    _sectorCursor = (index + 1) % buckets.Count;
                    return;
                }
            }

            _sectorCursor = 0;
        }

        private static void ScanEnumerableBuckets(IEnumerable buckets, int bucketCount, int budget, ref int visited, ref int acted)
        {
            if (buckets == null) return;

            if (bucketCount <= 0)
            {
                foreach (object bucket in buckets)
                {
                    if (ScanBucket(bucket, budget, ref visited, ref acted))
                        return;
                }
                _sectorCursor = 0;
                return;
            }

            if (_sectorCursor < 0 || _sectorCursor >= bucketCount)
                _sectorCursor = 0;

            int start = _sectorCursor;
            int index = 0;
            foreach (object bucket in buckets)
            {
                if (index++ < start) continue;
                if (ScanBucket(bucket, budget, ref visited, ref acted))
                {
                    _sectorCursor = index % bucketCount;
                    return;
                }
            }

            if (start > 0)
            {
                index = 0;
                foreach (object bucket in buckets)
                {
                    if (index >= start) break;
                    index++;
                    if (ScanBucket(bucket, budget, ref visited, ref acted))
                    {
                        _sectorCursor = index % bucketCount;
                        return;
                    }
                }
            }

            _sectorCursor = 0;
        }

        private static bool ScanBucket(object bucket, int budget, ref int visited, ref int acted)
        {
            bucket = GetBucketValue(bucket);
            if (!(bucket is IEnumerable objects)) return false;
            foreach (object zdo in objects)
            {
                if (zdo == null) continue;

                // Spread work over time without keeping brittle enumerators alive.
                _scanCursor++;
                int stride = Math.Max(1, EffectiveConfig.OwnershipScanStride);
                if ((_scanCursor % stride) != 0) continue;

                TrackCurrentOwnerLoad(zdo);
                visited++;

                if (TryMaybeImproveOwner(zdo, OwnershipCandidateReason.Generic))
                    acted++;

                if (visited >= budget)
                    return true;
            }

            return false;
        }

        private static object GetBucketValue(object bucket)
        {
            if (bucket == null) return null;
            if (bucket is DictionaryEntry entry) return entry.Value;

            try
            {
                Type type = bucket.GetType();
                if (type.IsGenericType && type.FullName != null && type.FullName.StartsWith("System.Collections.Generic.KeyValuePair", StringComparison.Ordinal))
                    return type.GetProperty("Value")?.GetValue(bucket, null) ?? bucket;
            }
            catch { }

            return bucket;
        }

        private static void TrackCurrentOwnerLoad(object zdo)
        {
            if (ZdoReflection.TryGetOwner(zdo, out long owner) && owner != 0)
            {
                OwnerLoadEstimate.TryGetValue(owner, out int count);
                OwnerLoadEstimate[owner] = count + 1;
            }
        }

        private static bool TryMaybeImproveOwner(object zdo, OwnershipCandidateReason reason)
        {
            if (zdo == null || ReflectionCache.ZDOGetPositionMethod == null) return false;
            if (!ZdoReflection.TryGetOwner(zdo, out long currentOwner)) currentOwner = 0;

            bool persistent = ZdoReflection.TryGetPersistent(zdo, out bool p) && p;
            bool playerLike = ZdoKeyPolicy.LooksPlayerLike(zdo);
            bool shipLike = ZdoKeyPolicy.LooksShipLike(zdo);

            // Profile A does not steal player ZDOs. Ship ownership stays conservative by default.
            if (playerLike) return false;
            if (shipLike && !AllowShipOwnership && reason != OwnershipCandidateReason.DisconnectedOwner) return false;

            OwnerState state = GetOwnerState(zdo);
            double now = Time.realtimeSinceStartupAsDouble;

            bool currentConnected = currentOwner != 0 && IsUidCurrentlyConnected(currentOwner);
            bool currentIsServer = currentOwner != 0 && ZdoReflection.TryGetServerSessionId(out long serverUid) && currentOwner == serverUid;

            if (currentOwner == 0)
            {
                if (state.LastSeenUnownedTime <= 0) state.LastSeenUnownedTime = now;
                if (persistent && now - state.LastSeenUnownedTime >= EffectiveConfig.RecoverUnownedAfterSeconds)
                    reason = OwnershipCandidateReason.LongUnownedPersistent;
            }
            else
            {
                state.LastSeenUnownedTime = 0;
            }

            if (persistent && reason != OwnershipCandidateReason.LongUnownedPersistent && reason != OwnershipCandidateReason.DisconnectedOwner)
                return false;

            if (!currentConnected && currentOwner != 0 && !currentIsServer)
                reason = OwnershipCandidateReason.DisconnectedOwner;

            Vector3 pos = ZdoReflection.GetPosition(zdo, Vector3.zero);

            // Generic scanning is intentionally conservative: do not churn a healthy owner.
            if (reason == OwnershipCandidateReason.Generic && currentConnected && !AllowHealthyOwnerChallenge)
                return false;

            OwnerCandidate best = FindBestCandidate(zdo, pos, state, reason);
            if (best == null)
            {
                if (reason == OwnershipCandidateReason.LongUnownedPersistent && AllowServerFallbackForPersistentRecovery)
                    return RecoverToServerOwner(zdo, state, currentOwner, reason);
                return false;
            }

            if (best.Uid == currentOwner) return false;

            float currentScore = ComputeCurrentOwnerScore(currentOwner, pos, state, reason, currentConnected, currentIsServer);
            if (!IsCandidateBetter(best.Score, currentScore, reason)) return false;

            float cooldown = reason == OwnershipCandidateReason.CombatTarget ? EffectiveConfig.OwnerHintSwitchCooldownSeconds : EffectiveConfig.OwnerSwitchCooldownSeconds;
            if (shipLike) cooldown = Math.Max(cooldown, EffectiveConfig.ShipOwnerSwitchCooldownSeconds);
            if (now - state.LastOwnerChangeTime < cooldown) return false;
            if (reason == OwnershipCandidateReason.CombatTarget && now - state.LastCombatOwnerChangeTime < EffectiveConfig.OwnerHintSwitchCooldownSeconds) return false;

            if (!ZdoReflection.TrySetOwner(zdo, best.Uid)) return false;

            state.PreviousOwner = currentOwner;
            state.CurrentOwner = best.Uid;
            state.LastOwnerChangeTime = now;
            if (reason == OwnershipCandidateReason.CombatTarget) state.LastCombatOwnerChangeTime = now;
            state.ChangeCount++;
            ZdoReflection.ForceSend(zdo);

            if (ModConfig.DebugLogging.Value)
                Plugin.Log.LogDebug($"ProfileA owner {reason}: {currentOwner}->{best.Uid}, best={best.Score:F1}, current={currentScore:F1}, q={best.Quality:F1}, dist={best.Distance:F1}, load={best.EstimatedLoad}");

            return true;
        }

        private static OwnerCandidate FindBestCandidate(object zdo, Vector3 zdoPosition, OwnerState state, OwnershipCandidateReason reason)
        {
            OwnerCandidate best = null;
            float radius = reason == OwnershipCandidateReason.CombatTarget
                ? Math.Max(EffectiveConfig.OwnershipCandidateRadius, EffectiveConfig.OwnerHintCandidateRadius)
                : EffectiveConfig.OwnershipCandidateRadius;

            foreach (object peer in ZdoReflection.EnumeratePeers(ZdoReflection.ZDOManInstance))
            {
                if (!NetReflection.TryGetPeerUid(peer, out long uid) || uid == 0) continue;

                Vector3 refPos = NetReflection.GetPeerRefPos(peer);
                float distance = Vector3.Distance(zdoPosition, refPos);
                if (distance > radius) continue;

                PeerQualityState quality = PeerQualityMeter.UpdateFromPeer(peer) ?? PeerQualityMeter.GetByUid(uid) ?? PeerQualityMeter.GetByPeer(peer);
                if (!CandidateConnectionAllowed(quality, reason)) continue;

                int load = EstimateOwnerLoad(uid);
                float score = ComputeCandidateScore(uid, distance, quality, load, state, reason);
                if (best == null || score < best.Score)
                {
                    best = new OwnerCandidate
                    {
                        Uid = uid,
                        Peer = peer,
                        Distance = distance,
                        Quality = quality?.ConnectionQualityMs ?? 999f,
                        Score = score,
                        EstimatedLoad = load,
                        IsCombatTarget = uid == state.CombatTargetUid && IsCombatHintFresh(state)
                    };
                }
            }
            return best;
        }

        private static float ComputeCandidateScore(long uid, float distance, PeerQualityState quality, int load, OwnerState state, OwnershipCandidateReason reason)
        {
            float q = quality?.ConnectionQualityMs ?? 999f;
            float score = q;
            score += distance * Math.Max(0f, EffectiveConfig.OwnershipDistanceScoreWeight);
            score += load * Math.Max(0f, EffectiveConfig.OwnershipLoadPenaltyPerZdo);

            if (uid == state.CombatTargetUid && IsCombatHintFresh(state))
                score -= Math.Max(0f, EffectiveConfig.OwnerHintScoreBonusMs);

            return score;
        }

        private static float ComputeCurrentOwnerScore(long currentOwner, Vector3 zdoPosition, OwnerState state, OwnershipCandidateReason reason, bool currentConnected, bool currentIsServer)
        {
            if (currentOwner == 0) return 999f;
            if (!currentConnected && !currentIsServer) return 999f;
            if (currentIsServer) return EffectiveConfig.ServerFallbackPenaltyMs;

            PeerQualityState q = PeerQualityMeter.GetByUid(currentOwner);
            object peer = FindZdoPeerByUid(currentOwner);
            float distance = peer != null ? Vector3.Distance(zdoPosition, NetReflection.GetPeerRefPos(peer)) : 0f;
            return ComputeCandidateScore(currentOwner, distance, q, EstimateOwnerLoad(currentOwner), state, reason);
        }

        private static bool RecoverToServerOwner(object zdo, OwnerState state, long currentOwner, OwnershipCandidateReason reason)
        {
            if (!ZdoReflection.TryGetServerSessionId(out long serverUid) || serverUid == 0) return false;
            if (!ZdoReflection.TrySetOwner(zdo, serverUid)) return false;

            double now = Time.realtimeSinceStartupAsDouble;
            state.PreviousOwner = currentOwner;
            state.CurrentOwner = serverUid;
            state.LastOwnerChangeTime = now;
            state.ChangeCount++;
            ZdoReflection.ForceSend(zdo);

            if (ModConfig.DebugLogging.Value)
                Plugin.Log.LogDebug($"ProfileA recovery to server owner {reason}: {currentOwner}->{serverUid}");
            return true;
        }

        private static int EstimateOwnerLoad(long uid)
        {
            if (uid == 0) return 0;
            if (OwnerLoadEstimate.TryGetValue(uid, out int load)) return load;
            PeerQualityState q = PeerQualityMeter.GetByUid(uid);
            return q?.OwnedDynamicEstimate ?? 0;
        }

        private static bool CandidateConnectionAllowed(PeerQualityState candidate, OwnershipCandidateReason reason)
        {
            if (!EffectiveConfig.PeerQualityEnabled) return true;
            if (candidate == null || !candidate.HasAnySample) return false;
            if (candidate.PingEmaMs > EffectiveConfig.MaxCandidatePingMs) return false;
            if (candidate.PingJitterMs > EffectiveConfig.MaxCandidateJitterMs) return false;
            return true;
        }

        private static bool IsCandidateBetter(float candidateScore, float currentScore, OwnershipCandidateReason reason)
        {
            if (currentScore <= 0f || currentScore >= 900f) return true;

            float relative = Math.Max(0f, EffectiveConfig.OwnershipRelativeHysteresis);
            float absolute = Math.Max(0f, EffectiveConfig.OwnershipAbsoluteHysteresisMs);
            if (reason == OwnershipCandidateReason.DisconnectedOwner || reason == OwnershipCandidateReason.LongUnownedPersistent)
                absolute = Math.Min(absolute, 5f);
            float required = Math.Max(absolute, currentScore * relative);
            return currentScore - candidateScore >= required;
        }

        private static OwnerState GetOwnerState(object zdo)
        {
            if (!ZdoReflection.TryGetIdKey(zdo, out ZdoIdKey id))
                id = ZdoIdKey.FromRuntimeObject(zdo);
            if (!ByZdoId.TryGetValue(id, out OwnerState state))
            {
                state = new OwnerState();
                ByZdoId[id] = state;
            }
            state.LastTouchedTime = Time.realtimeSinceStartupAsDouble;
            return state;
        }

        private static void PruneOwnerStatesIfDue(double now)
        {
            if (now < _nextOwnerStatePrune) return;
            _nextOwnerStatePrune = now + OwnerStatePruneIntervalSeconds;

            var expired = new List<ZdoIdKey>();
            foreach (KeyValuePair<ZdoIdKey, OwnerState> pair in ByZdoId)
            {
                OwnerState state = pair.Value;
                if (state == null || (state.LastTouchedTime > 0 && now - state.LastTouchedTime >= OwnerStateTtlSeconds))
                    expired.Add(pair.Key);
            }

            foreach (ZdoIdKey id in expired)
                ByZdoId.Remove(id);

            while (ByZdoId.Count > MaxOwnerStates && RemoveOldestOwnerState())
            {
            }
        }

        private static bool RemoveOldestOwnerState()
        {
            ZdoIdKey oldestKey = default;
            double oldest = double.MaxValue;
            bool found = false;

            foreach (KeyValuePair<ZdoIdKey, OwnerState> pair in ByZdoId)
            {
                double touched = pair.Value?.LastTouchedTime ?? 0;
                if (touched < oldest)
                {
                    oldest = touched;
                    oldestKey = pair.Key;
                    found = true;
                }
            }

            if (!found) return false;
            ByZdoId.Remove(oldestKey);
            return true;
        }

        private static bool IsCombatHintFresh(OwnerState state)
        {
            if (state == null || state.CombatTargetUid == 0) return false;
            return Time.realtimeSinceStartupAsDouble - state.CombatTargetHintTime <= Math.Max(0.1f, EffectiveConfig.OwnerHintLifetimeSeconds);
        }

        private static bool IsUidCurrentlyConnected(long uid)
        {
            return FindZdoPeerByUid(uid) != null;
        }

        private static object FindZdoPeerByUid(long uid)
        {
            if (uid == 0) return null;
            foreach (object peer in ZdoReflection.EnumeratePeers(ZdoReflection.ZDOManInstance))
            {
                if (NetReflection.TryGetPeerUid(peer, out long peerUid) && peerUid == uid)
                    return peer;
            }
            return null;
        }
    }
}
