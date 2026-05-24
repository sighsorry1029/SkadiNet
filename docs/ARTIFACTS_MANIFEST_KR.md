# Artifacts Manifest

이 문서는 정리된 SkadiNet 작업 폴더에서 어떤 파일이 소스이고 어떤 파일이 산출물인지 구분하기 위한 메모입니다.

## 소스

```text
SkadiNet/
  SkadiNet.csproj
  Plugin.cs
  Config/
  Diagnostics/
  Gameplay/
  Networking/
  Reflection/
  Zdo/
  Libs/ServerSync.dll
```

프로젝트 루트가 실제로 빌드되는 BepInEx 플러그인 프로젝트입니다. C# SDK 프로젝트이므로 하위 폴더의 `.cs` 파일은 자동으로 컴파일에 포함됩니다.

## 문서

```text
docs/
  ARCHITECTURE_KR.md
  CLIENT_STUTTER_GUARD_KR.md
  CONFIG_RENAME_MAP_KR.md
  REFLECTION_CACHE_REFACTOR_PLAN_KR.md
  SERVERSYNC_INTEGRATION_PLAN_KR.md
```

`docs`는 개발/인계 문서의 단일 위치입니다. 예전 `context` 폴더와 프로젝트 내부 `docs` 폴더는 루트 `docs`로 통합했습니다.

## 빌드 산출물

```text
bin/
obj/
```

빌드 산출물은 SecretRecipes와 같은 루트 `bin`/`obj` 구조를 사용합니다. 이 경로는 `.gitignore` 대상입니다.

## 배포/참조 파일

```text
artifacts/SkadiNet-1.0.0-bepinex-plugin.zip
artifacts/reference-dlls/ServerSync.dll
artifacts/ValheimUltimateNetwork-0.6.4-alpha-generic-config-source.zip
```

`SkadiNet-1.0.0-bepinex-plugin.zip`은 기존 배포 패키지입니다. `ValheimUltimateNetwork-0.6.4-alpha-generic-config-source.zip`은 rename 이전의 legacy reference archive로 보관합니다.
