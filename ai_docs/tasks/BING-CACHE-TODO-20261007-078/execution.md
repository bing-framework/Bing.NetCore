<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-TODO-20261007-078
AI_EXECUTION_FINISHED_AT: 2026-10-07T10:41:06.4435278+08:00
AI_EXECUTION_STARTED_AT: 2026-10-07T02:14:10.014Z

# 实施执行报告

## Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching.FreeRedis/Bing/Caching/FreeRedis/FreeRedisCacheManager.cs`、`framework/src/Bing.Caching.FreeRedis/Bing/Caching/FreeRedis/FreeRedisCacheManager.Stream.cs`，以及本任务的计划、TODO、执行记录、测试映射和权威索引。
- ChangedProjects：`Bing.Caching.FreeRedis` 生产批量读写路径、`Bing.Caching.FreeRedis.Tests.Integration`；`Bing.Caching.CSRedis.Tests.Integration` 仅运行现有门禁。
- ChangedPublicContracts：无；没有修改缓存接口、配置构造函数或 Provider 注册方式。
- ChangedRuntimePaths：FreeRedis 客户端分片异步批量写入按客户端调度，单键批量读取改用 `GET`；FreeRedis/CSRedis 双节点扫描、批量、流式、Clear 和失败边界的真实 Redis I/O。
- ChangedProviders：FreeRedis 生产代码改变；CSRedis 仅测试验证；EasyCaching、Core 和 SQL 不变。
- ChangedTFMs：FreeRedis/CSRedis 集成测试的 `net6.0`、`net8.0`；生产构建为 `netstandard2.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：两个 Redis Provider 的客户端分片集成测试及其 Redis 客户端连接。
- RiskLevel：MEDIUM。FreeRedis 同一客户端的批量写入改为按输入顺序串行，不同客户端仍按 `MaxConcurrency` 有界并发；单键 MGET 改用 GET，避免 FreeRedis 1.1.5 的空响应。可能降低单客户端写吞吐，但不改变公共 API。

## 实施结果

初次 FreeRedis net8 双节点完整门禁先后出现 6/7 和 5/7 失败，异常均为 MGET 响应数量与请求不一致；隔离流式用例进一步确认单键分组时为“请求 1 个键，返回 0 个值”。代码审计确认两个独立触发条件：`SetAllAsync` 在同一 `RedisClient` 上并发派发多个 `EvalAsync`，以及 FreeRedis 1.1.5 的单键 `MGetAsync` 返回空数组。

已完成以下修复：

- `SetAllAsync` 在 `ClientSharded` 拓扑先按物理键路由客户端分组；不同客户端按 `MaxConcurrency` 分窗并发，同一客户端按输入顺序串行写入，并在异常/取消时观察已经派发的任务。
- FreeRedis 原始批量读取对单键批次统一使用 `GET`，保留多键 `MGET` 和原有顺序、缺失值与预算校验语义；同步、异步、List 和 Stream 路径共用该边界。

## 验证证据

测试使用两个任务专用 Redis 5.0.12 容器：`localhost:6390` 和 `localhost:6391`；两者启动后 `redis-cli ping` 均返回 `PONG`。环境变量只在测试进程内设置，没有写入用户或系统环境。

| 项目 | TFM | 测试范围 | 结果 |
| --- | --- | --- | --- |
| `Bing.Caching.FreeRedis.Tests.Integration` | `net8.0` | `ShardedCacheTest` | 7 通过，0 失败，0 跳过 |
| `Bing.Caching.FreeRedis.Tests.Integration` | `net6.0` | `ShardedCacheTest` | 7 通过，0 失败，0 跳过 |
| `Bing.Caching.CSRedis.Tests.Integration` | `net8.0` | `ShardedCacheTest` | 7 通过，0 失败，0 跳过 |
| `Bing.Caching.CSRedis.Tests.Integration` | `net6.0` | `ShardedCacheTest` | 7 通过，0 失败，0 跳过 |

生产构建结果：`Bing.Caching.FreeRedis` 和 `Bing.Caching.CSRedis` 的 `netstandard2.0` Release 构建均为 0 警告、0 错误。`git diff --check` 通过。已删除 `bing-cache-shard-078-0`、`bing-cache-shard-078-1`，并确认原有 `redis` 容器仍监听 `6379`。

## 未覆盖和状态

- `BLOCKED_EXTERNAL`：Redis Cluster 三主三从、槽迁移/故障转移、FreeRedis/CSRedis 双进程回源、独立 EasyCaching 总线故障；本任务明确不以双节点客户端分片结果替代这些证据。
- `VERIFIED_BOUNDARY`：100,000 项性能矩阵和既有正式性能预算仍按缓存 TODO 索引中的边界结论处理。
- `DEFERRED`：同步分片清理并发、客户端内部队列/真实网络往返观测仍不在本任务范围。
