<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-HARDENING-20261004-001
AI_EXECUTION_FINISHED_AT: 2026-10-04T04:08:38Z

# 实施执行报告

## 执行结论

核心隔离风险、两个原生 Redis 适配器的写入/TTL/扫描/回源问题和 Core 集合并发问题已修复。EasyCaching Redis/Hybrid 已接入 Redis 租约和原子写回，Hybrid 回源提交后发布失效通知。旧版性能对照、隔离 Cluster 故障测试和独立复审已完成；`review.md` 为 PASS，`OPEN_ACTIONABLE=0`。发布仍需维护者决定已测出的性能回退预算。

## 任务信息

- Task ID：BING-CACHE-HARDENING-20261004-001。
- 范围：Bing.Caching、FreeRedis、CSRedis、EasyCaching、Bing.Core 集合缓存、两个 admin 注册入口。
- 非目标：SQL 内部缓存、旧注释内存管理器、其他模块分布式锁。

## 计划执行情况

| 项目 | 状态 | 证据或限制 |
| --- | --- | --- |
| P1 配置/键/生命周期 | COMPLETED | 不可变实例配置、版本化物理键、前缀检查、admin 注入客户端 |
| P2 公共行为 | COMPLETED | 覆盖写、毫秒 TTL、命中区分、批量顺序、字面量前缀、命名空间 Clear |
| P3 Redis 性能结构 | COMPLETED | 取消后停止派发、有界并发、逐页扫描；两个原生 Provider 非 Cluster 批量读分批执行；实测性能成本见 `performance.md` |
| P4 Cluster/分片 | COMPLETED | 三主三从稳定路径、主节点离线显式失败、副本提升后的全槽读取/扫描/清理、遗漏在线主节点拒绝部分结果、两节点分片数量校验及单分片失败已验证 |
| P5 跨进程回源 | COMPLETED | FreeRedis、CSRedis、EasyCaching Redis 的正常合并、退出、超时、失租和等待超时双进程矩阵通过 |
| P6 EasyCaching | COMPLETED | DI、命名空间、Hybrid 操作、Redis/Hybrid 租约原子提交、跨实例总线失效、分页扫描和独立进程故障矩阵通过 |
| P7 Core 集合 | COMPLETED | 稳定同步对象、返回快照、并发添加和返回值修正 |
| P8 验证 | COMPLETED | 单节点、Cluster 故障转移、分片、双框架及三个 Redis Provider 双进程故障矩阵通过；独立复审 PASS |
| P9 性能 | COMPLETED | 固定旧版提交与同构程序，1千/1万项多轮对照、分位数、命令数、分配量及峰值内存见 `performance.md`；未设置发布阈值 |

## 已完成事项

- 两个 Redis 适配器改为实例命名空间，仅扫描/删除自身键；`Clear` 不再调用 `FLUSHDB`。
- FreeRedis `SetAllAsync` 覆盖已有值，异步 TTL 保留毫秒；两个适配器用同槽 Lua 脚本进行带所有权的回源写回。
- Cluster 显式遍历主节点；CSRedis 自动发现节点后按地址去重；客户端分片按物理键路由。
- EasyCaching 内存/Redis/Hybrid 注册恢复，Hybrid 批量操作走 Hybrid Provider，进程内同键回源合并。
- Core 列表和字典缓存改为锁内判断、快照读取；两个 admin 注册入口迁移到实例配置。
- 迁移说明与包 README 更新，旧键按冷缓存处理。

## 部分/未完成事项

- Cluster 测试证明静态主节点清单失效时扫描/清理显式报错，并证明调用方更新为提升后的主节点清单后恢复完整操作；运行中的自动拓扑发现不属于当前契约。
- Cluster 扫描前后核对当前主节点身份与 0..16383 槽位覆盖，拒绝遗漏在线节点、故障未切主、重复节点及拓扑变化；客户端分片通过受信 `expectedShardCount` 与 Redis `run_id` 拒绝数量不足或重复实例。
- 旧实现性能对照已落盘；候选实现的批量读/扫描延迟与分配量仍高于旧版，详见 `performance.md`，不宣称全面性能收益。
- 首次独立审查 `review.md` 的 FIX-001 至 FIX-004 已关闭；性能预算单列 `BLOCKED_APPROVAL`，不计入代码缺陷。

## 修改文件

- 公共契约：`framework/src/Bing.Caching/Bing/Caching/` 中的配置、键格式、异步接口与 NullCache。
- Redis 实现：`framework/src/Bing.Caching.FreeRedis/`、`framework/src/Bing.Caching.CSRedis/`。
- EasyCaching：`framework/src/Bing.EasyCaching/`；Core：`framework/src/Bing.Core/Bing/Caching/`。
- 测试：对应 Caching/Core/EasyCaching 测试项目和 `Bing.Caching.ProcessProbe`。
- 接入与文档：两个 admin CacheModule/appsettings、缓存 README、`docs/migrations/cache-namespace-v2.md`、`docs/guides/横切能力文档.md`。

## API/数据/配置变化

- Redis 管理器构造函数现在要求 `CacheConfiguration`；多节点需要指定拓扑和主节点扫描客户端。客户端前缀必须为空。
- 物理键切换为 `bing:v2:`，不会回读或自动删除旧键；部署将产生冷缓存回源压力。
- 新增带 `CancellationToken` 的异步回源委托重载；显式 TTL 必须大于零并向上取整到毫秒。
- `Clear` 仅清理本实例命名空间，扫描清理非原子；并发写入期间不保证最终为空。

## 测试结果

- `Bing.Caching.Tests`：net6/net8 各 105/105。
- `Bing.Core.Tests`：net6/net8 各 249/249。
- `Bing.EasyCaching.Tests`：net6/net8 各 22/22，含隔离 Redis、Hybrid 跨实例失效及总线发布故障注入。
- Redis 单节点及双分片：FreeRedis net6/net8 各 75 通过、5 门控跳过；CSRedis net6/net8 各 72 通过、4 门控跳过。门控项已在隔离 Cluster 与基准环境单独执行。
- 三主三从 Cluster：两个适配器稳定路径各 1/1；主节点离线和副本提升后的故障路径分别为 FreeRedis 3/3、CSRedis 2/2（net6/net8），覆盖跨槽读取、遗漏在线主节点拒绝部分结果、全主节点扫描及命名空间清理。
- 两个独立进程：FreeRedis、CSRedis 与 EasyCaching Redis 均完成正常同键合并、持有者退出、超时、失租及等待者超时矩阵；脚本见 `Bing.Caching.ProcessProbe/Run-ProcessProbe.ps1`。
- 两个独立单节点的客户端分片：两个适配器各 1/1。
- `RUN_INTEGRATION_TESTS` 门控保留；上述 Redis 均为本机隔离 Docker 测试实例。

## Build/Typecheck/Lint/Format

- 四个生产缓存项目 netstandard2.0 构建通过，0 错误。
- 两个 admin 项目构建通过，0 错误；已有依赖/目标框架警告仍存在。
- `Bing.All.sln --no-restore` 构建通过，0 错误、134 个已有依赖/目标框架警告（最新运行）。
- `git diff --check` 通过；无独立 Lint/Format 命令。

## 性能证据

旧实现的冻结基准为 Git commit `24c93cb1a5355797223637e2d42ac3dce968d7ef`，FreeRedis 源 Blob `26ca707089da1acbb658e220c15a2747f5e3ff08`，CSRedis 源 Blob `9d2b0fcf49c628d92a28ce02b0d2da0e0d1f2e75`。

固定旧版提交、同一机器与 Redis、同一基准程序的 1千/1万项对照见 `performance.md` 和 `bench/*-v2.json`。正常同键回源次数从每轮 16 降至 1；新物理键与协调使批量读、扫描和分配量高于旧版。Redis 命令统计包含 Lua 内部命令，不等于网络往返。无预设发布阈值；返回列表仍需要容纳最终结果。

## 最终生产符号 → 测试方法

| 符号/关键行为 | 测试项目和方法 |
| --- | --- |
| `CacheKeyFormat.GetPhysicalKey` / 命名空间与特殊字符 | `Bing.Caching.Tests.CacheConfigurationAndKeyFormatTest.PhysicalKey_Isolated_AndRoundTripsSpecialCharacters` |
| `CacheConfiguration.GetExpiration` / TTL 校验 | `Bing.Caching.Tests.CacheConfigurationAndKeyFormatTest.Configuration_ValidatesAndRoundsExpiration` |
| `CacheClusterTopology.Validate` / Cluster 完整性 | `Bing.Caching.Tests.CacheClusterTopologyTest.Validate_RequiresEveryActiveMasterExactlyOnce`；`Bing.Caching.FreeRedis.Tests.ClusterFailureTest.OmittedOnlineMaster_RejectsScanAndClearBeforePartialSuccess` |
| `CacheConfiguration.ExpectedShardCount`、`CacheShardTopology.Validate` / 分片完整性 | 两个原生 Provider 的 `ShardedCacheTest.OmittedOnlineShard_IsRejectedAtConstruction`、`RepeatedShard_IsRejectedEvenWhenExpectedCountMatches`；`Bing.Caching.Tests.CacheConfigurationAndKeyFormatTest.ShardTopology_RejectsRepeatedServerIdentity` |
| `FreeRedisCacheManager.SetAllAsync` / 覆盖与毫秒 TTL | `Bing.Caching.FreeRedis.Tests.NamespaceAndCoordinationTest.SetAllAsync_OverwritesAndKeepsMillisecondTtl` |
| `FreeRedisCacheManager.ClearAsync` / 命名空间保护 | `Bing.Caching.FreeRedis.Tests.NamespaceAndCoordinationTest.ClearAndBroadPattern_KeepOtherNamespace` |
| `FreeRedisCacheManager.GetAsync` / 失租和同键合并 | `Bing.Caching.FreeRedis.Tests.NamespaceAndCoordinationTest.LostLease_CannotOverwriteNewOwner`、`TwoManagers_CoordinateSameMiss` |
| `FreeRedisCacheManager.GetAsync` / 运行中取消 | `Bing.Caching.FreeRedis.Tests.TtlCancellationRecoveryTest.CallerCancellation_PropagatesOperationCanceled_WhenRetrieverIgnoresToken_AndRecovers` |
| `CSRedisCacheManager.SetAllAsync` / 覆盖与 TTL | `Bing.Caching.CSRedis.Tests.NamespaceIsolationTest.SetAllAsync_Overwrites_AndPreservesSubsecondTtl` |
| `CSRedisCacheManager.ClearAsync` / 命名空间保护 | `Bing.Caching.CSRedis.Tests.NamespaceIsolationTest.Clear_AndWildcardPrefix_KeepOtherNamespace` |
| `CSRedisCacheManager.GetAsync` / 失租和同键合并 | `Bing.Caching.CSRedis.Tests.NamespaceIsolationTest.LostLease_CannotOverwriteNewOwner`、`TwoManagers_UseOneRetrieverForSameKey` |
| 两个 Redis 管理器 Cluster/分片扫描 | 各自 `ClusterCacheTest.ThreeMasters_CrossSlotReadScanAndClear`、`ShardedCacheTest.TwoShards_ScanAndClearAcrossBothNodes` |
| `CacheManager` / Hybrid 批量与清理 | `Bing.EasyCaching.Tests.CacheManagerTests.Hybrid_SetAll_UsesHybridProviderWithNamespacedKeys`、`Clear_RemovesOnlyCurrentNamespaceAndNeverFlushes` |
| `CacheManager.GetAsync` / 进程内合并 | `Bing.EasyCaching.Tests.CacheManagerTests.MemoryRetriever_CoalescesAcrossManagerInstances` |
| `RedisLeaseCoordinator` / 同键合并、超时旧持有者、异常恢复 | `Bing.EasyCaching.Tests.RedisCoordinatorIntegrationTest.RedisRetriever_CoalescesAcrossManagerInstances`、`RedisRetriever_RejectsExpiredOwnerCommit`、`RedisRetriever_ReleasesLeaseAfterFactoryErrorAndCancellation` |
| `HybridCacheManager` / 跨实例失效 | `Bing.EasyCaching.Tests.RedisCoordinatorIntegrationTest.HybridRetriever_PublishesInvalidationToOtherInstance` |
| `HybridCacheManager` / 总线发布失败 | `Bing.EasyCaching.Tests.RedisCoordinatorIntegrationTest.HybridRetriever_BusFailureReportsCommittedValueAndClearsLocal` |
| `CachePattern` / 三 Provider 模式契约 | `Bing.Caching.Tests.CachePatternTest` 及三个适配器的转义通配测试 |
| `ICache` / 统一覆盖写、NX、默认值、批量顺序 | 三个适配器的 `Provider_SatisfiesSharedContract` |
| Core 集合缓存读取/写入 | `Bing.Core.Tests.Caching.LocalMemoryCacheBaseTest` 中的并发添加、快照和删除测试 |

## 计划偏差、基线问题与已知风险

- EasyCaching Redis/Hybrid 采用基于其 Provider `IDatabase` 和序列化器的协调组件，并使用 Lua 原子提交；组件实现不同于 FreeRedis/CSRedis，但租约语义一致。
- 性能旧版基线由固定 Git 提交构建并以同构程序采集；新实现批量路径仍存在已记录的性能成本。
- `Bing.Caching.Tests` 原有两个同名测试类导致编译冲突，已仅重命名测试类以恢复构建。
- Cluster 节点列表与客户端分片顺序属于部署契约；拓扑变化需重新配置，过期清单下扫描显式失败。
- EasyCaching Hybrid 为最终一致缓存；总线发布失败时 Redis 已提交的值仍保留，本地层清除且异常显式传播，其他实例可能暂时持有旧值。

## Reviewer 注意事项与 Git 状态

- 优先审查 EasyCaching 租约与 Hybrid 失效传播、Cluster 拓扑变化时的扫描完整性、故障矩阵及性能证据。
- 所有修改仍在工作区；未自动 `git add`、`git commit`、`git push` 或创建 PR。
