# Profile A / AdaptiveClientOwner

이 빌드 implements Profile A as the default ownership policy.

## 목표

Profile A keeps Valheim's client-ownership model, but makes owner selection smarter. It does **not** force the server to own everything.

Owner candidates are scored with:

```text
score =
    connection quality
  + distance penalty
  + owner load penalty
  - combat target bonus
```

Lower score is better. A candidate must beat the current owner by relative/absolute hysteresis and pass cooldown checks.

## Implemented

- `OwnershipMode = AdaptiveClientOwner`
- `EnableAdaptiveClientOwnership = true`
- budgeted ZDO sector scanning
- quality gates based on Ping EMA and jitter
- combat owner hints from `MonsterAI.SetTarget`
- owner switch cooldown
- combat owner switch cooldown
- owner-load penalty
- disconnected owner recovery
- long-unowned persistent ZDO recovery
- player-like ZDO guard
- ship-like ZDO guard, disabled by default
- force full sync after owner change when `ZDOMan.ForceSendZDO` is available

## Intentionally not implemented

These are Profile B / ServerAuthorityLite concepts and are intentionally not part of this build:

- block `ZDOMan.ReleaseNearbyZDOS` peer assignment
- force all unowned ZDOs to server ownership
- treat server-owned ZDOs as globally active
- replace `FindSectorObjects`
- patch `SpawnSystem.UpdateSpawning`
- override `ZoneSystem` / `ZNetScene` active area behavior

## Suggested initial settings

```toml
[General]
OwnershipIntensity = 45
```

Internal ownership safety policy keeps ship ownership disabled, keeps healthy-owner challenge disabled for generic scans, and allows temporary server-owner fallback for long-unowned persistent recovery when no good client candidate exists.
