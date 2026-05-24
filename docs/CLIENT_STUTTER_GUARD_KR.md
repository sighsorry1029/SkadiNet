# ClientStutterGuard

`ClientStutterGuard`는 ResourceUnloadOptimizer에서 가져온 아이디어를 네트워크 모드에 맞게 축소한 선택형 클라이언트 보조 모듈입니다.

이 모듈은 ZDO, RPC, ownership, socket protocol을 바꾸지 않습니다. 서버 필수 기능도 아니며, dedicated server에서는 기본적으로 꺼집니다.

## 가져온 기능

구현한 항목은 다음입니다.

```text
1. 네트워크 중요 구간 중 GC.Collect 지연
2. 메모리 압박 기반 cleanup 허용/스킵
3. 여러 cleanup 요청을 한 번으로 합치는 coalescing
4. idle/safe window에서 cleanup 실행
5. Resources.UnloadUnusedAssets는 지연/패치하지 않음
```

## 기본값

```toml
[General]
ClientStutterGuardStrength = 50
```

## 네트워크 중요 구간

다음 상황에서 cleanup delay window가 열립니다.

```text
ZNet.OnNewConnection
ZDOMan.RPC_ZDOData 수신
ZNetScene.InLoadingScreen == true
MonsterAI.SetTarget 전투 상태 변화
움직이는 Ship.CustomFixedUpdate 감지
```

## 메모리 압박 게이트

OS 메모리 정보를 읽을 수 있으면 다음 조건을 사용합니다.

```text
메모리 사용률 < MemoryPressureThresholdPercent
그리고 사용 가능 메모리 >= MinimumFreeMemoryMB
→ 메모리 여유 있음, cleanup 지연 가능

메모리 사용률 높음 또는 사용 가능 메모리 부족
→ cleanup 허용
```

Windows에서는 `GlobalMemoryStatusEx`, Linux에서는 `/proc/meminfo`를 사용합니다. 정보를 읽을 수 없는 플랫폼에서는 메모리 여유가 있다고 가정하지 않고, 중요 구간이 아니면 cleanup을 허용합니다.

## Resources.UnloadUnusedAssets

`Resources.UnloadUnusedAssets`는 지연하거나 패치하지 않습니다. Config에 노출되는 stutter guard 조절값은 `ClientStutterGuardStrength` 하나입니다.

## 주의

이 기능은 네트워크 protocol 기능이 아니므로 서버 접속 조건으로 삼지 않습니다. ResourceUnloadOptimizer와 동시에 쓰면 같은 cleanup 경로를 중복 후킹하므로 같이 쓰지 않는 것을 권장합니다.
