# SkadiNet Unified Slider Reference

![](https://github.com/sighsorry1029/SkadiNet/blob/main/Images/SkadiNet.png?raw=true)

Reduces network pressure and desync with adaptive ZDO scheduling, smarter ownership recovery, payload reduction, compression, and safe visual RPC filtering, all controlled by simple unified sliders. <br>

## Practical Slider Values

Use these values as active tuning points. `0` still means disabled.

| Slider | Conservative | Recommended Default | Worth Trying | Aggressive | Very Aggressive |
|---|---:|---:|---:|---:|---:|
| `SchedulerThroughput` | 20 | 35 | 50 | 70 | 85 |
| `PayloadReducerStrength` | 15 | 30 | 45 | 60 | 75 |
| `CompressionAggression` | 25 | 50 | 65 | 80 | 90 |
| `OwnershipIntensity` | 20 | 45 | 60 | 75 | 90 |
| `ClientStutterGuardStrength` | 25 | 50 | 65 | 80 | 90 |
| `RpcAoiAggression` | 15 | 35 | 50 | 65 | 80 |

`Very Aggressive` is intended as a practical upper preset for strong servers and stable clients, not as the absolute maximum. Push individual sliders closer to 100 only when you are testing a specific bottleneck.

## SchedulerThroughput

Controls the adaptive ZDO send scheduler: send timing, peer batch size, ZDO package caps, lagging-peer queue thresholds, and lagging-peer backfill timing. It does not control Steam send-rate.

Steam send-rate is intentionally not exposed as a tuning slider. When SkadiNet is enabled, the Steam socket send-rate ceiling is fixed at 36 MB/s as part of the baseline network profile.

### Toward 1

The scheduler sends less aggressively. Send intervals are longer, fewer peers are processed per tick, ZDO package caps stay close to vanilla, and lagging peers are allowed a wider backfill window before the scheduler forces another send attempt.

Expected effect: lower server CPU pressure and lower network burst pressure. This side is safer for hosted solo play, weak dedicated servers, limited upload bandwidth, unstable clients, or large modded worlds that create many dirty ZDOs at once. Clients may receive ZDO updates less quickly during heavy world activity.

### Toward 100

The scheduler sends more aggressively. Send intervals become shorter, more peers can be processed per tick, ZDO package limits grow, the minimum package size shrinks, and lagging peers receive backfill attempts sooner.

Expected effect: lower visible latency and faster catch-up for clients that can keep up. This side can improve responsiveness on strong servers with good upload bandwidth, but it spends more CPU and network capacity and may pressure weak clients harder.

## PayloadReducerStrength

Controls how strongly SkadiNet suppresses tiny repeated Vector3 and Quaternion updates for non-critical ZDO data.

### Toward 1

The payload reducer becomes very conservative. Only extremely tiny position and rotation changes are suppressed, and forced refreshes happen frequently.

Expected effect: sync fidelity stays close to vanilla while still trimming the smallest repeated micro-updates. This side is best when avoiding visual drift matters more than bandwidth savings.

### Toward 100

The payload reducer becomes more aggressive. Larger tiny-position changes and smaller rotation differences can be suppressed, and forced refreshes happen less often.

Expected effect: lower ZDO data churn and less bandwidth spent on small repeated movement or rotation noise. This side can help busy bases and long-running servers, but it may make some non-critical motion look slightly less granular until the next forced refresh.

## CompressionAggression

Controls negotiated package compression. Compression is only used with peers that support SkadiNet's feature handshake, and a peer is disabled for compression after a compression/decompression failure.

### Toward 1

Compression only attempts larger packages and requires a stronger size reduction before replacing the original package.

Expected effect: minimal CPU overhead and very low chance of wasting time on packages that do not compress well. This side is best when server CPU is more precious than bandwidth.

### Toward 100

Compression considers smaller packages and accepts smaller compression wins.

Expected effect: more network payloads may be compressed, which can reduce bandwidth on busy servers or constrained uplinks. This side costs more CPU and may spend effort on packages with only modest savings.

## OwnershipIntensity

Controls adaptive client ownership, peer-quality gates, candidate reach, switch conservatism, disconnected-owner recovery, long-unowned persistent recovery, and combat owner hints. SkadiNet does not make the server own every object; it scores nearby client candidates and nudges selected ZDO ownership toward the best candidate. In hosted solo or server states with no ZDO peers, background ownership scans are skipped.

### Toward 1

Adaptive ownership stays very conservative. Scans are smaller and slower, candidate radius is narrower, peer-quality gates are forgiving, ownership hints are weaker, and recovery from bad owners is slower.

Expected effect: low server overhead and low ownership churn. This side reduces the chance of unnecessary owner movement, but disconnected-owner recovery, combat ownership improvement, and long-unowned persistent recovery can take longer or happen less often.

### Toward 100

Adaptive ownership becomes more assertive. Scans cover more objects more often, candidate radius grows, combat owner hints become stronger, owner-load penalty is reduced, distance penalty is reduced, and peer-quality gates reject high ping or high jitter candidates more strictly.

Expected effect: faster recovery from bad or disconnected owners and more willingness to move ownership toward better nearby clients. This side can reduce owner-related desync and combat latency on healthy networks, but it costs more server CPU and may create more owner transitions.

## ClientStutterGuardStrength

Controls the client-side stutter guard. It delays `GC.Collect` during sensitive gameplay/network windows such as initial sync, teleport/loading, full snapshot bursts, combat target changes, and active ship travel. `Resources.UnloadUnusedAssets` is not delayed by this guard. The guard is disabled automatically on dedicated servers and batch-mode/headless clients.

### Toward 1

The guard uses short protection windows and allows pending GC cleanup sooner. Memory pressure checks are stricter, so cleanup is allowed earlier when free memory is not abundant.

Expected effect: smaller changes to vanilla cleanup behavior. This side is safer for clients with limited memory or users who prefer fewer delayed cleanup operations, but it may not hide as many GC-related stutters.

### Toward 100

The guard keeps longer protection windows and allows GC cleanup to be delayed longer when memory appears plentiful.

Expected effect: fewer GC spikes during sensitive gameplay and network moments. This side can feel smoother on clients with enough memory, but delayed cleanup can keep memory pressure higher for longer.

## RpcAoiAggression



Controls conservative area-of-interest routing for a small whitelist of safe visual RPCs, currently visual feedback such as damage text and talk messages. Unknown, directed, unresolved, global, animation/noise, and state-critical RPCs always use vanilla routing. If every peer would receive the RPC anyway, SkadiNet also falls back to vanilla fanout.

### Toward 1

RPC AoI keeps a large visual radius for eligible visual RPCs. Only clients relatively far from the resolved ZDO origin of the visual event are excluded.

Expected effect: lower risk of missing nearby visual feedback while still reducing some unnecessary fanout.

### Toward 100

RPC AoI uses a smaller visual radius for eligible visual RPCs. More clients outside the local event area are excluded from receiving those RPCs.

Expected effect: lower routed RPC fanout for visual events. This side can reduce network noise in crowded worlds, but it has a higher chance of hiding visual feedback for players near the edge of the chosen radius.
