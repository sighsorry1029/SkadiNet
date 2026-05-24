# Config Rename Map / 0.6.4-alpha

0.6.4-alpha는 기능 로직을 바꾸지 않고, 사용자에게 노출되는 config key를 더 일반적인 이름으로 정리한 버전입니다.
기존 0.6.3-alpha 이하 config 파일을 그대로 쓰는 경우, 아래 항목은 새 이름으로 옮겨 적어야 합니다. BepInEx는 새 key를 새 항목으로 생성합니다.

## Peer Quality

| 이전 이름 | 새 이름 |
|---|---|
| `PingEMAHalfLifeSeconds` | `PeerPingEmaHalfLifeSeconds` |
| `PingStatisticsWindow` | `PeerPingSampleWindow` |
| `ConnectionQualityPingMeanWeight` | `PeerQualityMeanWeight` |
| `ConnectionQualityPingStdDevWeight` | `PeerQualityStdDevWeight` |
| `ConnectionQualityPingJitterWeight` | `PeerQualityJitterWeight` |
| `ConnectionQualityPingEMAWeight` | `PeerQualityEmaWeight` |

## Ownership

| 이전 이름 | 새 이름 |
|---|---|
| `EnableCombatOwnership` / `EnableTargetOwnershipHints` | `EnableOwnerHints` |
| `ReassignOwnershipConnectionQualityHysteresis` | `OwnershipRelativeHysteresis` |
| `OwnershipCooldownSeconds` | `OwnerSwitchCooldownSeconds` |
| `CombatOwnershipCooldownSeconds` / `TargetOwnerSwitchCooldownSeconds` | `OwnerHintSwitchCooldownSeconds` |
| `ShipOwnershipCooldownSeconds` | `ShipOwnerSwitchCooldownSeconds` |
| `MaxCandidatePingEMAMs` | `MaxCandidatePingMs` |

## Adaptive Ownership

Section 이름도 `41 - Profile A Adaptive Ownership`에서 `41 - Adaptive Ownership`으로 바뀌었습니다.

| 이전 이름 | 새 이름 |
|---|---|
| `AdaptiveOwnershipScanPerTick` | `OwnershipScanBudget` |
| `AdaptiveOwnershipStride` | `OwnershipScanStride` |
| `AdaptiveOwnershipScanIntervalSeconds` | `OwnershipScanIntervalSeconds` |
| `AdaptiveOwnershipRadius` / `GeneralOwnerRadius` | `OwnershipCandidateRadius` |
| `CombatOwnershipRadius` / `TargetHintRadius` | `OwnerHintCandidateRadius` |
| `CombatTargetBonusMs` / `TargetOwnerBonusMs` | `OwnerHintScoreBonusMs` |
| `CombatTargetHintLifetimeSeconds` / `TargetHintLifetimeSeconds` | `OwnerHintLifetimeSeconds` |
| `RecentlySwitchedPenaltyMs` / `RecentSwitchPenaltyMs` | removed in pre-1.0.0 simplification |
| `ServerOwnerScorePenaltyMs` / `ServerOwnerPenaltyMs` | `ServerFallbackPenaltyMs` |
| `AdaptiveOwnershipSkipPersistentZdos` / `SkipPersistentZdos` | `SkipPersistentObjects` |
| `AdaptiveOwnershipAllowShips` / `AllowShipOwnershipChanges` | `AllowShipOwnership` |
| `AdaptiveOwnershipReassignUnownedDynamic` / `AssignUnownedDynamicZdos` | `AssignUnownedDynamicObjects` |
| `AdaptiveOwnershipChallengeHealthyOwners` / `ChallengeHealthyOwners` | `AllowHealthyOwnerChallenge` |
| `RecoverDisconnectedOwnerZDOs` | `RecoverDisconnectedOwnerObjects` |
| `RecoverLongUnownedPersistentZDOs` | `RecoverLongUnownedPersistentObjects` |
| `RecoverPersistentToServerWhenNoClient` / `RecoverPersistentToServerFallback` | `AllowServerFallbackForPersistentRecovery` |
| `ForceServerOwnerForAllUnowned` / `ForceServerOwnerForAllUnownedObjects` | `UseServerFallbackForAllUnownedObjects` |

## Legacy ownership migration

| 이전 이름 | 새 이름 |
|---|---|
| `EnableExperimentalQualityOwnership` | removed in pre-1.0.0 simplification |
| `ExperimentalOwnershipScanPerTick` | removed in pre-1.0.0 simplification |
| `ExperimentalOwnershipRadius` | removed in pre-1.0.0 simplification |

## 변경하지 않은 것

다음 항목은 이미 충분히 일반적이거나 Valheim/BepInEx의 실제 개념명이라 유지했습니다.

- `ZDO`, `ZNet`, `ZRpc`, `ZRoutedRpc`, `LightFlicker` 등 Valheim class/method 기반 명칭
- `EnableCompression`, `EnableZdoDelta`, `EnableRpcAoi`
- `ClientStutterGuard`, `ClientRenderOptimizer`
