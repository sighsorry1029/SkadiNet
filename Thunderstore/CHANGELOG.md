# Changelog

## 1.0.0

- Consolidated the remaining config surface into a single `General` section.
- Set config defaults to the recommended general-purpose slider profile: `SchedulerThroughput=35`, `PayloadReducerStrength=30`, `CompressionAggression=50`, `OwnershipIntensity=45`, `ClientStutterGuardStrength=50`, and `RpcAoiAggression=35`.
- Added frame-hitch diagnostics to `DebugLogging`, including FPS, peer send queues, approximate ZDO sector/object counts, memory, and recent scheduler state at the hitch moment.
- Skipped adaptive ownership background scans when a hosted solo/server instance has no ZDO peers, and added recent ownership scan state to hitch diagnostics.
- Added ConfigurationManager ordering metadata so the single `General` section displays operational toggles first, stable sliders next, and experimental sliders last.
- Changed the default `ClientStutterGuardStrength` to `50`, enabling the client stutter guard at balanced strength on clients while keeping it disabled on dedicated servers.
- Decoupled Steam send-rate from `SchedulerThroughput` and fixed it internally at 36 MB/s while SkadiNet is enabled, with no separate config toggle.
- Fixed Profile A peer-quality sampling on current Valheim by reading socket `GetConnectionQuality` ping instead of relying on the removed `ZRpc.m_ping` field.
- Removed the ZDO Delta feature path and `ZdoDeltaAggression` config entry; SkadiNet no longer patches `ZDO.Serialize` for delta payloads.
- Changed `RpcAoiAggression` to a conservative default of `35` so whitelist-only visual RPC AoI is enabled by default.
- Hardened RPC AoI for always-on use by requiring a resolved target ZDO origin, falling back to vanilla when all peers would receive the RPC, and keeping unknown, directed, unresolved, global, animation/noise, and state-critical RPCs on vanilla routing.
- Lowered the `SchedulerThroughput` low endpoint so `1` keeps ZDO package caps and minimum package size much closer to vanilla while preserving the existing `50` and `100` behavior.
- Restored the original internal scheduler mapping shape while keeping stall diagnostics available through `DebugLogging`.
- Added high-signal debug diagnostics for scheduler stalls, scheduler 10-second summaries, compression 10-second summaries, effective slider mappings, and slow adaptive ownership scans.
- Removed ClientRenderOptimizer and its LightFlicker config; render-distance/light/graphics tuning is left to dedicated client render mods.
- Removed the remaining ownership safety toggles from config; ship ownership and healthy-owner challenge are internally disabled, while persistent recovery server fallback remains internally enabled.
- Removed the per-domain `UseAdvanced...Tuning` switches and the detailed manual numeric tuning entries behind unified sliders.
- Collapsed the ownership-domain sliders into `OwnershipIntensity`, which controls Profile A ownership, peer-quality gates, candidate reach, switch conservatism, and combat owner hints together.
- Removed the separate feature enable toggles for scheduler, payload reducer, compression, RPC AoI, client stutter guard, peer quality, owner hints, and Profile A ownership; their feature-owning sliders now use `0` as disabled and `1-100` as enabled strength.
- Removed `SchedulerDesyncSafety`; `SchedulerThroughput` now controls scheduler send pressure, peer queue thresholds, and lagging-peer backfill timing together.
- Removed the one-option `OwnershipMode` config entry; Profile A is controlled directly by `OwnershipIntensity`.
- Added simple tuning sliders for scheduler throughput/desync safety, payload reducer strength, compression aggression, RPC AoI aggression, and client stutter guard strength, with sliders bound first in their sections.
- Removed always-applied safety/diagnostic numeric entries from config; compression failure cutoff is now a fixed internal fail-safe.
- Lowered the default PayloadReducer strength toward the safer side.
- Widened unified slider endpoints slightly so the removed detailed values still have practical tuning range without exposing dozens of low-level fields.
- Changed the BepInEx plugin version to loader-safe `1.0.0`.
- Changed the BepInEx plugin GUID to `sighsorry.SkadiNet` so the generated config file is `sighsorry.SkadiNet.cfg`.
- Changed plugin author namespace/assembly metadata to `sighsorry` and marked the old `com.openai.valheim.skadinet` GUID incompatible to avoid duplicate installs.
- Added ServerSync-backed config/version sync with `LockServerConfig`.
- Server-critical network, ownership, compression, and RPC AoI config entries are synchronized from the server.
- Kept `DebugLogging` and `ClientStutterGuard` local-only.
- Changed SkadiNet feature handshake semantics from config-enabled flags to code capability flags to avoid config sync timing races.
- Switched config creation to a ServerSync-style bind wrapper that marks each entry as synced or local at creation time.
- Added disconnected-peer cleanup for feature negotiation, peer quality, and ownership hints.
- Removed legacy/reserved/dead config entries that no longer affected runtime behavior.
- Removed DungeonLayerAwareness, its config entries, patches, and `dungeon_splitter` incompatibility.
- Removed the ClientStutterGuard dungeon-layer transition window.
- Replaced stringified ZDOID cache keys with structured `ZdoIdKey` keys for ownership state and payload reducer refresh tracking.
- Added small reflection hot-path caches for per-type fields and ZDO extra-data key/value entry accessors.
- Replaced boolean config sync registration with explicit `ConfigSyncScope` values: `ServerSynced`, `ClientLocal`, and `MigrationOnly`.
- Added a ReflectionCache domain-splitting refactor plan in `docs/REFLECTION_CACHE_REFACTOR_PLAN_KR.md`.
- Split ReflectionCache into domain partials for Net, ZDO, routed RPC, gameplay, hot-path reflection helpers, and capability diagnostics.
- Added NetReflection, ZdoReflection, RpcReflection, and GameplayReflection helper classes and moved the hottest ownership, scheduler, peer lifecycle, peer quality, and RPC AoI callers onto them.
- Added guarded delegate caches for selected hot-path reflection access: ZDO id/owner/prefab/rotation/position, ZDOPeer uid/rpc/refPos, ZRpc socket lookup, socket queue size, and routed RPC data fields/actions.
- Hardened RPC AoI by precomputing recipients before sending, falling back to vanilla if any candidate lacks AoI capability, and using vanilla routing when the origin cannot be resolved.
- Added lagging-peer scheduler backfill via `LaggingPeerMaxSkipSeconds` so hard queue skipping cannot starve a client indefinitely.
- Added TTL/cap pruning for payload reducer and ownership state caches on long-running servers.
- Added a persistent sector cursor to Profile A ownership scanning to avoid repeatedly spending the scan budget on early sectors.
- Updated build target to `net48` to match ServerSync.
- Merged ServerSync into `SkadiNet.dll` with ILRepack so installs only need the SkadiNet plugin DLL.

## 0.6.4-alpha

- Renamed user-facing config keys to more generic names.
- Generalized peer-quality and adaptive-ownership config keys, for example `PeerPingEmaHalfLifeSeconds`, `PeerQualityEmaWeight`, `OwnershipRelativeHysteresis`, `EnableOwnerHints`, `OwnershipScanBudget`, and `OwnerHintScoreBonusMs`.
- Removed source-mod-style wording from config descriptions where practical.
- No feature logic was added or removed in this revision.

## 0.6.2-alpha

- Removed `QualitySettingsProfile` from `ClientRenderOptimizer`.
- Removed config/code for `DisableRealtimeReflectionProbes`, `DisableSoftParticles`, and `DisableSoftVegetation`.
- No new optimization features were added in this revision.

# 0.6.0-alpha

- Added optional client-side `ClientRenderOptimizer`, inspired by FramePerSecondPlus, default off.
- Implemented the requested modules: `LightFlickerOptimizer`, `ClutterOptimizer`, and `QualitySettingsProfile`.
- Added `LightFlickerMode` with `RespectAccessibility`, `Static`, and `LowFrequency` options.
- Added configurable `ClutterSystem.m_grassPatchSize`, default 16.
- Added QualitySettings profile toggles for realtime reflection probes, soft particles, and soft vegetation.
- Deliberately did not implement smoke optimization, torch particle edits, intro skipping, particle raycast budget settings.
- Declared incompatibility with `vsp.FramePerSecondPlus` to avoid double render patches.

# 0.5.0-alpha

- Added optional client-side `ClientStutterGuard`, inspired by ResourceUnloadOptimizer, default off.
- Added GC.Collect defer/coalescing during network-critical windows.
- Added memory-pressure gate using Windows GlobalMemoryStatusEx or Linux /proc/meminfo when available.
- Added idle/safe-window cleanup scheduler with max delay guard.
- Added best-effort Resources.UnloadUnusedAssets delay, default off, with optional NativeDetour attempt when enabled.
- Added network window markers for initial sync, ZDOData receive bursts, loading/teleport, dungeon layer transition, combat target changes, and active ship movement.
- Declared incompatibility with `Azumatt.ResourceUnloadOptimizer` to avoid double GC/unload hooks.

# 0.4.0-alpha

- Added optional `DungeonLayerAwareness`, inspired by Dungeon Splitter, default off.
- Added layer-aware ZDO sync list filtering via `ZDOMan.FindObjects` / `FindDistantObjects` postfix filtering.
- Added layer-aware Profile A ownership candidate filtering.
- Added 2.5s default ground/dungeon transition grace window.
- Added AlwaysSend/AlwaysLoad prefab hash whitelists for players, LocationProxy, portals, _ZoneCtrl, _TerrainCompiler, and custom prefab names.
- Added RPC AoI layer recipient filtering.
- Added ZDO delta baseline reset and full-sync grace after peer layer changes.
- Declared incompatibility with `dungeon_splitter` to avoid double filtering.


## 0.3.0-alpha

Profile A / AdaptiveClientOwner implementation.

- Promoted AdaptiveClientOwner to the default ownership mode.
- Added quality + distance + owner-load scoring for ownership candidates.
- Added MonsterAI ownership hints as a score bonus, not an unconditional transfer.
- Added ownership hysteresis/cooldown gating around owner changes.
- Added conservative generic ownership scanner with budget/stride/interval controls.
- Added disconnected-owner recovery and long-unowned persistent ZDO recovery.
- Added player-like and ship-like ZDO guards using ZDOVars hash detection.
- Earlier alpha builds exposed `OwnershipMode = AdaptiveClientOwner`; 1.0.0 removed it because no second ownership mode is implemented.
- Marked Smoothbrain DedicatedServer as incompatible because it conflicts with Profile A ownership policy.

## 0.2.0-alpha

- Added negotiated Deflate compression with magic wrapper and raw fallback.
- Added whitelist-only RPC AoI for DamageText/TalkerSay, with AddNoise/TriggerAnimation optional.
- Added optional ZDO delta baseline codec, disabled by default.

## 0.1.0-alpha

- Initial stable core: adaptive ZDO scheduler, queue cap patch, micro-update reducer, peer quality metric, combat ownership gate.
