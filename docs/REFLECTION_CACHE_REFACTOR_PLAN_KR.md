# ReflectionCache 리팩터링 계획

## 목표

`ReflectionCache`는 현재 Valheim/ZDO/RPC/Gameplay 심볼을 한 곳에서 모두 소유합니다. 이 구조는 호출부가 단순하다는 장점이 있지만, Valheim 업데이트 때 어떤 도메인이 깨졌는지 진단하기 어렵고, hot path helper가 계속 한 파일에 쌓이는 문제가 있습니다.

목표는 다음입니다.

```text
1. 기존 호출부를 한 번에 크게 흔들지 않는다.
2. ReflectionCache facade는 유지한다.
3. 내부를 도메인별 capability/cache로 나눈다.
4. 누락 심볼을 도메인별로 진단한다.
5. hot path에는 cached FieldInfo/accessor/delegate만 지나가게 한다.
```

## 권장 순서

### 1단계: partial 파일 분리

동작 변경 없이 파일만 나눕니다. 이 단계는 PR/패치 리뷰가 가장 쉽습니다.

```text
ReflectionCache.cs                 공통 Initialize, facade, shared helpers
ReflectionCache.Net.cs             ZNet, ZRpc, ZSteamSocket, peer helpers
ReflectionCache.Zdo.cs             ZDO, ZDOID, ZDOMan, ZDOExtraData
ReflectionCache.Rpc.cs             ZRoutedRpc, RoutedRPCData
ReflectionCache.Gameplay.cs        Player, MonsterAI, Character, Ship/LightFlicker 후보
ReflectionCache.ReflectionHotPath.cs cached fields, KeyValuePair accessors, method delegates
```

`internal static class ReflectionCache`를 `internal static partial class ReflectionCache`로 바꾸고, public/internal API 이름은 그대로 둡니다.

### 2단계: 도메인 capability 진단 추가

도메인별로 필수/선택 심볼을 나눠 초기화 결과를 남깁니다.

```text
NetCapabilities
  Required: ZNet, ZRpc, ZSteamSocket.Send/Recv
  Optional: send queue size

ZdoCapabilities
  Required: ZDO, ZDOID, ZDO.Serialize, ZDOExtraData maps
  Optional: ForceSendZDO, connection data

RpcCapabilities
  Required only when RPC AoI enabled

GameplayCapabilities
  Required only when ownership hints/client optimizer enabled
```

디버그 로그는 “missing symbol” 하나씩 흩뿌리지 않고 도메인 단위 요약으로 출력합니다.

### 3단계: 호출부를 도메인 helper로 서서히 이동

기존 호출부는 처음에는 그대로 두고, 새 코드부터 도메인 helper를 사용합니다.

```text
ReflectionCache.TryGetZdoIdKey(...)
→ ZdoReflection.TryGetIdKey(...)

ReflectionCache.GetPeerRefPos(...)
→ NetReflection.GetPeerRefPos(...)

ReflectionCache.RoutedRpcDataTargetPeerIdField
→ RpcReflection.SetTargetPeer(...)
```

이때 `ReflectionCache`는 호환 facade로 남겨서 한 번에 대량 수정하지 않습니다.

### 4단계: hot path delegate화

현재는 FieldInfo/MethodInfo 캐시까지 들어갔습니다. 다음 단계에서는 자주 쓰는 getter/setter만 delegate로 바꿉니다.

우선순위:

```text
ZDO.m_uid getter
ZDO.GetPosition
ZDO.GetOwner
ZDO.GetPrefab
ZDO.GetRotation
ZRpc.GetSocket
ZDOPeer.m_uid / m_peer / m_refPos
RoutedRPCData target/method hash fields
```

주의: Unity/Valheim 타입이 런타임 로드되는 환경이라 delegate 생성 실패 fallback을 반드시 유지합니다.

### 5단계: 실패 정책 정리

각 기능이 필요한 capability를 만족하지 못할 때의 동작을 명확히 합니다.

```text
Scheduler core missing       → feature off, warning
Compression send/recv missing→ compression off
ZDO delta missing maps       → ZDO delta off
RPC AoI route missing        → RPC AoI off
Client-only symbols missing  → 해당 client module off
```

## 당장 하지 않는 것

```text
ReflectionCache 완전 삭제
모든 호출부 일괄 변경
런타임 reflection fallback 제거
ZDO Delta 알고리즘 재작성
```

## 검증 기준

```text
1. dotnet build .\SkadiNet.sln -c Release
2. DebugLogging=true에서 도메인별 capability 요약 확인
3. DebugLogging=false에서 로그 소음 없음
4. EnableZdoDelta=false 기본값에서 기존 동작 변화 없음
5. EnableCompression/EnableRpcAoi/Profile A smoke test
```

## 구현 현황

2026-05-02 패치 기준으로 1단계 partial 파일 분리, 2단계 도메인 capability 요약 로그, 3단계 domain helper class 전환의 hot path 우선 적용, 4단계 delegate hot path 전환의 선별 적용을 반영했습니다.

```text
ReflectionCache.cs                 facade, shared fields, Initialize
ReflectionCache.Net.cs             ZNet/ZRpc/ZSteamSocket 초기화와 peer/network helpers
ReflectionCache.Zdo.cs             ZDO/ZDOID/ZDOMan/ZDOExtraData 초기화와 ZDO helpers
ReflectionCache.Rpc.cs             ZRoutedRpc/RoutedRPCData 초기화
ReflectionCache.Gameplay.cs        Player/MonsterAI/ZNetView 초기화와 gameplay helpers
ReflectionCache.ReflectionHotPath.cs cached field, key/value accessor, numeric conversion helpers
ReflectionCache.Capabilities.cs    DebugLogging용 도메인 capability 요약
NetReflection.cs                   ZNet/ZRpc/ZSteamSocket domain helper, peer delegate cache
ZdoReflection.cs                   ZDO/ZDOID/ZDOMan domain helper, selected ZDO delegate cache
RpcReflection.cs                   RoutedRPCData domain helper, field/action delegate cache
GameplayReflection.cs              Player/MonsterAI/ZNetView domain helper
ReflectionDelegateFactory.cs       guarded expression delegate factory with reflection fallback
```

현재 패치는 기존 `ReflectionCache` API를 호환 facade로 유지하면서 ownership, scheduler, ZDO delta, peer lifecycle, peer quality, RPC AoI처럼 반복 호출이 많은 경로를 새 domain helper로 이동했습니다.

4단계 delegate 전환은 전면 적용하지 않고 아래 항목만 선별했습니다.

```text
ZDO.m_uid
ZDO.GetPosition
ZDO.GetOwner
ZDO.GetPrefab / m_prefab
ZDO.GetRotation / m_rotation
ZDOPeer m_uid / m_peer / m_refPos
ZRpc.GetSocket
ZSteamSocket.GetSendQueueSize
RoutedRPCData sender/target/method fields
RoutedRPCData.Deserialize
ZRoutedRpc.RouteRPC
```

아직 남겨둔 작업은 기존 `ReflectionCache` facade 메서드 내부를 새 helper로 완전히 위임하는 정리와, `PayloadReducer`의 parameterized ZDO getters 같은 추가 후보를 실제 프로파일링 후 선택 적용하는 일입니다.
