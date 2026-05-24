# Codex Handoff

## 현재 상태

- 프로젝트명: `SkadiNet`
- 버전: `1.0.0`
- 대상: BepInEx 5 / Harmony / Valheim / `net48`
- 솔루션: `SkadiNet.sln`
- 프로젝트: `SkadiNet.csproj`

이 작업 폴더는 source bundle 형태에서 SecretRecipes와 비슷한 루트형 모드 프로젝트 구조로 정리되었습니다. 실제 빌드 프로젝트는 루트에 있으며, 문서는 `docs`, 스크립트는 `scripts`, 기존 배포/참조 산출물은 `artifacts`에 둡니다.

## 주요 기능 경계

```text
Config/
  ModConfig, EffectiveConfig, ServerSync config binding

Networking/
  compression, feature negotiation, RPC AoI, peer lifecycle/quality

Zdo/
  ZDO scheduler, ZDO key policy, payload reducer patches

Reflection/
  reflection cache and reflection helper classes

Gameplay/
  ownership and client-side stutter guard

Diagnostics/
  frame hitch diagnostics
```

## 빌드

빌드 전 `VALHEIM_DIR`이 Valheim 설치 폴더를 가리켜야 합니다.

```powershell
$env:VALHEIM_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
.\scripts\build.ps1
```

결과 DLL은 다음 위치에 생성됩니다.

```text
bin/Release/net48/SkadiNet.dll
```

## ServerSync 메모

- 참조 DLL: `Libs/ServerSync.dll`
- 원본 보관 위치: `artifacts/reference-dlls/ServerSync.dll`
- 통합 담당 파일: `ILRepack.targets`
- 설정 동기화 담당 코드: `Config/ConfigSyncManager.cs`

`SkadiNet_Features`는 config enabled 여부 자체가 아니라 코드 capability handshake를 나타내는 방향으로 유지하는 것이 안전합니다. 실제 활성화 여부는 서버 동기화 config와 peer capability를 함께 봐야 합니다.

## 남은 확인

1. 실제 Valheim/BepInEx 환경에서 Release 빌드 확인
2. ServerSync 병합 후 플러그인 zip 구성 확인
3. compression, ZDO delta, RPC AoI를 소수 인원 환경에서 순차 검증
4. config rename 안내와 README 버전 표기 일치 여부 확인
