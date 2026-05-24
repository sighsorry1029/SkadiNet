# ServerSync Integration Plan

## 목표

SkadiNet의 server-critical config와 버전 정보를 ServerSync로 동기화하되, 네트워크 기능 handshake 자체는 `SkadiNet_Features` capability 교환으로 유지합니다.

핵심 원칙은 다음과 같습니다.

```text
ServerSync = 서버 config와 버전 동기화
SkadiNet_Features = peer가 특정 wire protocol을 처리할 수 있는지 확인
EffectiveConfig = 서버 config와 peer capability를 합쳐 실제 활성 여부 계산
```

## 파일 위치

```text
Libs/ServerSync.dll
Config/ConfigSyncManager.cs
Config/ModConfig.cs
Config/EffectiveConfig.cs
Networking/FeatureNegotiation.cs
ILRepack.targets
```

`ServerSync.dll`은 빌드 시 참조되고, Release 빌드에서는 `ILRepack.targets`를 통해 최종 `SkadiNet.dll` 내부로 병합됩니다.

## 동기화 대상

서버/클라이언트 의미가 달라지면 안 되는 항목은 ServerSync synchronized config로 둡니다.

- compression enable/threshold/ratio
- ZDO delta enable/keyframe/fallback 관련 값
- RPC AoI enable/whitelist 정책
- adaptive ownership 관련 server-side 값
- protocol/version compatibility에 영향을 주는 값

클라이언트 로컬 보조 기능은 synchronized false로 유지합니다.

- `ClientStutterGuard`
- 로컬 진단/로그 출력
- 클라이언트 UX 또는 렌더링 보조 설정

## 활성화 판단

각 기능은 단순히 config가 true라고 바로 켜지면 안 됩니다. 다음 조건을 모두 만족해야 합니다.

```text
server-synced config enabled
local code capability exists
remote peer capability exists
runtime fallback/error state allows it
```

이 판단은 `EffectiveConfig` 계층에서 모아두는 편이 좋습니다. 이렇게 하면 compression, ZDO delta, RPC AoI가 서로 다른 시점에 config와 handshake를 읽더라도 동일한 정책을 공유할 수 있습니다.

## 안전장치

- handshake 이전 peer에는 vanilla path를 사용합니다.
- decode/serialize 오류가 반복되면 해당 peer 기능을 비활성화합니다.
- unknown 또는 modded RPC는 vanilla routing을 우선합니다.
- ServerSync 패키지가 compression handshake보다 먼저 오갈 수 있으므로 초기 패킷은 보수적으로 처리합니다.

## 검증 순서

1. ServerSync DLL 참조와 Release 빌드 확인
2. `ServerSync.dll`이 최종 plugin zip에 별도 포함되지 않고 `SkadiNet.dll`에 병합되는지 확인
3. 서버 config 변경이 클라이언트 메모리 값에 반영되는지 확인
4. compression만 켠 상태로 접속/해제 테스트
5. ZDO delta를 소규모 환경에서 테스트
6. RPC AoI whitelist를 `DamageText`, `TalkerSay`부터 검증
7. fallback 로그가 과도하지 않은지 확인
