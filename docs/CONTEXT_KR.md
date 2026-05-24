# 전체 작업 맥락 요약

## 목표

Valheim dedicated server에서 desync를 줄이고 성능을 균형 있게 개선하는 궁극형 네트워크 모드 개발.

핵심 방향:

```text
서버가 모든 ZDO를 강제로 소유하지 않는다.
대신 연결 품질, 거리, 전투 타겟, owner load, cooldown, hysteresis를 보고
가장 적절한 클라이언트가 owner가 되도록 유도한다.
```

기본 프로필은 `Profile A / AdaptiveClientOwner`입니다. DedicatedServer식 `ServerAuthorityLite/Profile B`는 현재 구현하지 않았습니다.

## 참고한 모드와 흡수한 아이디어

```text
NetworkTweaks
  - ZDO peer send batching / Steam rate tuning 아이디어

LeanNet
  - Vector3/Quaternion micro-update reducer 아이디어

VBNetTweaks
  - queue limit 설정화 / teleport-loading guard / metrics 구조 참고

VAGhettoNetworking / FiresGhettoNetworking
  - compression, ZDO delta, RPC AoI 아이디어

ServersideQoL
  - Ping EMA, jitter, connection quality, ownership hysteresis 아이디어

CombatOwner
  - combat target owner hint 아이디어

DedicatedServer
  - 검토만 수행. Profile A에는 강한 server authority 기능 미도입

ResourceUnloadOptimizer
  - ClientStutterGuard: GC 지연, memory pressure gate, cleanup coalescing

FramePerSecondPlus
  - ClientRenderOptimizer: LightFlickerOptimizer만 남김

lighttweaks
  - 검토 결과 현재 추가 구현 안 함
```

## 버전별 요약

```text
0.1-alpha
  - 안정형 core: scheduler, reducer, peer quality, combat ownership hint

0.2-alpha
  - compression, RPC AoI, optional ZDO delta

0.3-alpha
  - Profile A / AdaptiveClientOwner 정리

0.5-alpha
  - ClientStutterGuard 추가

0.6-alpha
  - ClientRenderOptimizer 추가: LightFlicker, QualitySettingsProfile

0.6.2-alpha
  - QualitySettingsProfile 제거
  - ClientRenderOptimizer는 LightFlickerOptimizer만 유지
```

## 현재 기능 목록

```text
Adaptive ZDO scheduler
Steam send-rate patch
SendZDOs queue cap patch
LeanNet식 payload reducer
Peer feature negotiation
Deflate compression
Whitelist-only RPC AoI
Optional ZDO delta, 기본 off
Profile A adaptive ownership
Ping EMA / jitter / connection quality
Combat ownership hint
ClientStutterGuard, 기본 off
ClientRenderOptimizer, 기본 off
```

## 의도적으로 하지 않는 것

```text
모든 owner=0 ZDO를 server owner로 강제
ZDOMan.ReleaseNearbyZDOS peer assignment 차단
ZDO.Load 후 unowned를 무조건 server owner로 강제
ZoneSystem / ZNetScene / SpawnSystem 강제 서버 권한화
QualitySettings override
Smoke / torch / skip intro / demister 추가 기능
```
