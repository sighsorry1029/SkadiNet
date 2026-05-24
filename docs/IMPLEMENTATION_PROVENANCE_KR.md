# 구현 출처와 재사용 범위

## 결론

이 `SkadiNet` 소스는 참고 DLL들의 변수명과 구현 로직을 그대로 복사한 것이 아니라, 각 모드의 아이디어와 패치 지점을 분석한 뒤 새 프로젝트 구조에 맞게 재구현한 것입니다.

다만 Valheim 모딩 특성상 다음 이름들은 피할 수 없습니다.

```text
ZDOMan
ZDO
ZNet
ZRpc
ZSteamSocket
ZRoutedRpc
ZNetScene
LightFlicker
SendZDOs
CreateSyncList
CustomUpdate
```

이들은 참고 모드의 고유 변수명이 아니라 Valheim 본체 publicized assembly의 class/method/field 이름입니다. Harmony patch target 또는 reflection target으로 사용해야 하기 때문에 그대로 사용했습니다.

## 참고 모드별 반영 방식

### NetworkTweaks

가져온 것:

```text
ZDO peer batching을 통해 한 tick에 더 많은 peer에게 SendZDOs를 호출하는 아이디어
Steam send-rate 제한 완화 아이디어
초기 ZDOData race/buffer 문제의식
```

재구현 방식:

```text
고정 PeersPerUpdate 복사 대신 adaptive scheduler 구조로 새로 구현
config 이름과 scheduler 상태 구조는 새로 작성
```

### LeanNet

가져온 것:

```text
Vector3 / Quaternion micro-update culling
physics/NPC update noise를 줄이는 방향
```

재구현 방식:

```text
전역 revision freeze를 그대로 쓰지 않고 payload reducer/forced refresh 방향으로 새 구현
object guard와 config를 새로 작성
```

### VBNetTweaks

가져온 것:

```text
ZDO queue limit 설정화
teleport/loading window 필요성
metrics/config sync 구조 아이디어
```

재구현 방식:

```text
compression subsystem은 그대로 쓰지 않음
RemoveObjects 대체나 ship sync broad patch는 도입하지 않음
```

### VAGhettoNetworking

가져온 것:

```text
transport compression
ZDO delta
RPC AoI
ZDO priority/throttling 아이디어
```

재구현 방식:

```text
feature negotiation, fallback, vanilla group format 유지, whitelist-only RPC AoI 중심으로 새 구현
full server authority, ZoneSystem/ZNetScene broad override는 도입하지 않음
```

### ServersideQoL

가져온 것:

```text
Ping EMA
jitter/stddev 기반 connection quality
ownership hysteresis
owner cooldown 아이디어
```

재구현 방식:

```text
ExtendedZDO subclass나 ZDOPool.Get transpiler를 그대로 사용하지 않음
ZDOID sidecar state / Profile A owner score 방식으로 새 구현
```

### CombatOwner

가져온 것:

```text
전투 target player를 owner 후보로 강하게 보는 아이디어
```

재구현 방식:

```text
target에게 즉시 owner를 넘기지 않고 combat owner hint / score bonus로만 반영
connection quality, cooldown, hysteresis를 통과해야 owner 변경
```

### DedicatedServer

가져온 것:

```text
검토만 수행. Profile A에는 strong server-owner 기능 미도입
```

재구현 방식:

```text
ReleaseNearbyZDOS 차단, 모든 unowned ZDO server owner 강제, SpawnSystem patch는 구현하지 않음
```

### DungeonSplitter

가져온 것:

```text
height-based dungeon/ground layer awareness
layer-aware ZDO filtering
layer-aware ownership candidate filtering
transition grace period
AlwaysSend/AlwaysLoad whitelist
RPC AoI layer filter
ZDO delta baseline reset on layer change
```

재구현 방식:

```text
DungeonSplitter식 FindObjects/FindDistantObjects 전체 대체와 ReleaseNearbyZDOS 전체 대체는 하지 않음
현재 모드의 scheduler/ownership/RPC/delta 구조에 맞춰 새로 통합
```

### ResourceUnloadOptimizer

가져온 것:

```text
GC.Collect 지연
cleanup coalescing
memory pressure gate
network-critical window 중 cleanup 지연
```

재구현 방식:

```text
dedicated server에서는 hard-disable
Resources.UnloadUnusedAssets 완전 차단이나 NativeDetour 강제는 하지 않음
```

### FramePerSecondPlus

가져온 것:

```text
LightFlicker.CustomUpdate 비용 감소 아이디어
```

재구현 방식:

```text
LightFlicker 전체 무조건 차단 대신 RespectAccessibility / Static / LowFrequency 모드로 새 구현
QualitySettingsProfile은 0.6.1에서 제거
Smoke, torch particle, skip intro는 구현하지 않음
```

### lighttweaks

가져온 것:

```text
현재 구현 없음
```

검토 결과:

```text
Demister particle 비활성화는 후보였지만 사용자가 추가 구현을 원하지 않아 미구현
light range/intensity/shadow/mist/fire warmth/obliterator/building restriction은 최적화 코어에 부적합하거나 vanilla option과 목적 중복이라 미도입
```

## 코드 재사용 원칙

```text
1. third-party DLL 코드를 verbatim 복사하지 않는다.
2. 패치 대상 class/method/field 이름은 Valheim API 이름이므로 사용할 수 있다.
3. 모드별 고유 구현 구조는 그대로 가져오지 않고 SkadiNet 구조로 새로 작성한다.
4. 위험 기능은 handshake/fallback/config gate를 둔다.
5. vanilla 그래픽 옵션과 중복되는 client optimization은 제거한다.
```

## 현재 0.6.4 확인 사항

```text
src/ 안에는 QualitySettingsProfile 관련 code reference가 남아 있지 않다.
ClientRenderOptimizer는 LightFlickerOptimizer와 ClutterOptimizer만 포함한다.
```
