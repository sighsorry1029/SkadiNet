# Codex TODO

## 빌드 확인

1. `VALHEIM_DIR`을 실제 Valheim 설치 폴더로 지정한다.
2. `.\scripts\build.ps1` 또는 `dotnet build .\SkadiNet.sln -c Release`를 실행한다.
3. 결과물 `bin/Release/net48/SkadiNet.dll`을 확인한다.
4. Release 빌드 후 `ServerSync.dll`이 별도 출력물로 남지 않고 병합되는지 확인한다.

## 런타임 smoke test

1. dedicated server에서 기본 config로 부팅한다.
2. 클라이언트 1명 접속 후 handshake 로그를 확인한다.
3. compression만 켠 상태로 접속/이동/전투를 확인한다.
4. RPC AoI는 `DamageText`, `TalkerSay`부터 확인한다.
5. ZDO delta는 소수 인원 테스트 후 점진적으로 켠다.

## 문서/배포 정리

1. README의 버전, manifest version, assembly version을 함께 맞춘다.
2. plugin zip 구성에 `manifest.json`, README, CHANGELOG, `SkadiNet/SkadiNet.dll`이 들어가는지 확인한다.
3. `docs/CONFIG_RENAME_MAP_KR.md`와 실제 config key가 일치하는지 점검한다.
4. legacy archive는 reference 용도로만 두고 새 작업은 프로젝트 루트 기준으로 진행한다.

## 나중에 볼 것

- Profile B 또는 ServerAuthorityLite 계열 기능은 별도 브랜치/실험으로 분리
- config migration 자동화 여부
- 더 빠른 compression codec 도입 여부
- 런타임 진단 로그를 서버 운영자가 읽기 쉬운 형태로 정리
