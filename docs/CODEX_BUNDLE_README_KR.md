# SkadiNet 1.0.0 Codex Build Package

이 폴더는 SkadiNet을 이어서 개발하거나 로컬에서 빌드할 수 있도록 정리한 작업 패키지입니다.

## 구성

```text
./
  실제 C# 프로젝트와 소스 코드

docs/
  개발 문서, 인계 문서, 구조 문서

scripts/
  build.ps1
  build.sh

artifacts/
  기존 배포 zip, reference DLL, 새 빌드 산출물 위치
```

## 빌드

Windows PowerShell:

```powershell
$env:VALHEIM_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
.\scripts\build.ps1
```

Linux:

```bash
export VALHEIM_DIR="/path/to/Valheim"
./scripts/build.sh
```

또는 솔루션을 직접 빌드할 수 있습니다.

```powershell
dotnet build .\SkadiNet.sln -c Release
```

결과물:

```text
bin/Release/net48/SkadiNet.dll
```

## ServerSync

`Libs/ServerSync.dll`은 빌드 참조로 사용됩니다. Release 빌드에서는 `ILRepack.targets`가 `ServerSync.dll`을 최종 `SkadiNet.dll` 안으로 병합하도록 구성되어 있습니다.

## 정리 메모

- 예전 `source/SkadiNet` 경로는 SecretRecipes처럼 프로젝트 루트로 정리했습니다.
- 예전 `context` 폴더는 루트 `docs`로 통합했습니다.
- `bin`/`obj`는 루트에 생성되며 `.gitignore` 대상입니다.
