# SkadiNet v0.2 구조 요약

## 목표

v0.2는 v0.1의 안정형 코어 위에 aggressive 공격형 네트워크 기능 중 실제 네트워크 병목에 가장 직접적인 세 가지를 추가합니다.

1. negotiated compression
2. peer-baseline ZDO delta
3. whitelist-only RPC AoI

단, `ZoneSystem`, `ZNetScene.CreateDestroyObjects`, full server authority simulation은 넣지 않았습니다. 이들은 네트워크 최적화가 아니라 게임 시뮬레이션 구조 변경에 가까워 디버깅 난도가 높기 때문입니다.

## 모듈

| 모듈 | 역할 |
|---|---|
| `FeatureNegotiation` | `SkadiNet_Features` custom RPC 등록, peer별 capability 교환 |
| `CompressionPatches` | `ZSteamSocket.Send`/`Recv` magic header compression wrapper |
| `ZdoDeltaCodec` | `ZDOMan.SendZDOs` peer context + `ZDO.Serialize` partial snapshot |
| `RpcAoiRouter` | `ZRoutedRpc.RPC_RoutedRPC` whitelist AoI routing |
| `ZPackageTools` | reflection 기반 ZPackage 읽기/쓰기/압축 helper |
| `ZdoSchedulerPatches` | adaptive ZDO send scheduler |
| `PayloadReducerPatches` | LeanNet식 micro-update reducer |
| `PeerQualityMeter` | ServersideQoL식 ping EMA / jitter / quality |
| `OwnershipManager` | quality-gated CombatOwner식 ownership transfer |

## 패치 지점

| 기능 | Harmony target |
|---|---|
| Feature handshake | `ZNet.OnNewConnection(ZNetPeer)` postfix |
| Compression encode | `ZSteamSocket.Send(ZPackage)` prefix |
| Compression decode | `ZSteamSocket.Recv()` postfix |
| Adaptive scheduler | `ZDOMan.SendZDOToPeers2(float dt)` prefix |
| ZDO queue cap | `ZDOMan.SendZDOs` transpiler |
| ZDO delta context | `ZDOMan.SendZDOs(ZDOPeer, bool)` prefix/postfix |
| ZDO delta encode | `ZDO.Serialize(ZPackage)` prefix/postfix |
| RPC AoI | `ZRoutedRpc.RPC_RoutedRPC(ZRpc, ZPackage)` prefix |
| Steam send-rate | `ZSteamSocket.RegisterGlobalCallbacks` transpiler |
| Vector3 reducer | `ZDO.Set(int, Vector3)` prefix |
| Quaternion reducer | `ZDO.Set(int, Quaternion)` prefix |
| Peer quality | `ZRpc.ReceivePing` postfix |
| Combat ownership | `MonsterAI.SetTarget` postfix |

## Compression 안전장치

- handshake가 필요한 기본값입니다.
- magic header: `SKNC`
- protocol version 포함
- algorithm id 포함
- original size 포함
- threshold 미만 packet은 raw
- 압축 후 이득이 작으면 raw
- decode 실패 시 peer compression 비활성화

## ZDO delta 안전장치

- handshake가 필요한 기본값입니다.
- peer별 baseline dictionary를 사용합니다.
- baseline이 없으면 vanilla full serialize입니다.
- prefab이 바뀌면 full serialize입니다.
- 일정 시간이 지나면 full keyframe입니다.
- connection data가 있는 ZDO는 기본 full serialize입니다.
- group field 제거가 감지되면 full serialize입니다.
- group count가 vanilla byte limit을 넘으면 full serialize입니다.
- encoding error가 누적되면 peer delta를 끕니다.

## RPC AoI 안전장치

초기 whitelist:

- `DamageText`
- `TalkerSay`

선택적으로 켤 수 있으나 기본 off:

- `AddNoise`
- `TriggerAnimation`

항상 vanilla route 권장:

- `HealthChanged`
- `WNTHealthChanged`
- `SetTarget`
- `TriggerOnDeath`
- `SpawnedZone`
- unknown/modded RPC

## 권장 개발/테스트 순서

1. v0.1 안정형 코어만 테스트
2. compression 테스트
3. DamageText/TalkerSay RPC AoI 테스트
4. AddNoise/TriggerAnimation을 개별 테스트
5. ZDO delta를 소수 인원에서 테스트
6. 문제가 없을 때 full server에 적용

## 알려진 제한

- 이 소스는 reflection-heavy 방식으로 Valheim publicized assembly를 직접 참조하지 않습니다.
- 실제 Valheim/BepInEx 환경에서 빌드 및 런타임 검증이 필요합니다.
- Compression은 Deflate 기반입니다. Zstd/LZ4는 외부 dependency를 피하기 위해 아직 넣지 않았습니다.
- ZDO delta는 deletion semantics가 애매한 ZDOExtraData key removal을 partial packet으로 처리하지 않고 full fallback합니다.


## ClientStutterGuard

선택형 클라이언트 보조 모듈입니다. 네트워크 protocol이나 ZDO/RPC 의미론을 바꾸지 않고, 초기 동기화/포탈/던전 레이어 전환/ZDOData burst 같은 중요 구간에서 GC.Collect를 지연/병합합니다. `Resources.UnloadUnusedAssets`는 지연하거나 패치하지 않습니다. dedicated server에서는 자동 비활성화됩니다.
