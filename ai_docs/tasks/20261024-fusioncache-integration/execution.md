<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261024-fusioncache-integration
AI_EXECUTION_FINISHED_AT: 2026-10-24T09:16:10.964996+00:00

# 实施执行报告

> 以下原实施及 Review 修复记录保留为历史证据；当前交付以文末「用户授权后续：冷启动预热样例」为准。review.md 保持 Reviewer 的历史独立结论。

## 执行结论

FC-00～FC-08 已完成：独立适配包、公共契约、资源控制、分布式能力、诊断、样例、文档以及本地测试和 NuGet 包消费均已验证。正式外部门禁与实现完成状态分开记录。

- IMPLEMENTATION_STATUS: PASS
- TEST_STATUS: PASS（本次受影响范围）
- PACKAGE_CONSUMER_STATUS: PASS（net8.0 / net10.0）
- OPEN_ACTIONABLE: 0
- PERFORMANCE_EVIDENCE_STATUS: PASS（小规模本地测量）
- RESOURCE_EVIDENCE_STATUS: PASS（回源额度、排队、取消、超时与拒绝）
- EXTERNAL_GATE_STATUS: NOT_VERIFIED（远程 CI / 正式生产容量）
- RELEASE_STATUS: NOT_EVALUATED（未发布，也不将脏工作区验证视为正式发布证据）

## 任务信息

- Task ID：`20261024-fusioncache-integration`。
- 执行方式：`execute-plan`，遵循根 AGENTS 与用户级 GOAL_RULES。
- 已将用户固定选择同步到 [plan.md](plan.md) 第 15 节。
- 主代理负责契约、适配器、测试、分布式验证和最终集成；`luna_worker` 负责样例、文档及 NuGet 消费脚本。
- 新包 TFM：net8.0；新测试、Probe、样例消费 TFM：net8.0 / net10.0。
- 固定版本：FusionCache 及官方 serializer、Backplane、locker、OTel 扩展 2.9.0；Redis L2 10.0.1。

## 计划执行情况

| 任务 | 状态 | 结果 |
| --- | --- | --- |
| FC-00 工程和依赖 | COMPLETED | 新工程还原、构建、旧新 Provider 同宿主及 net8/net10 NuGet 消费通过；新包直接依赖只有两个 |
| FC-01 公共契约 | COMPLETED | 新增 IHybridCache / IHybridCacheProvider / CacheKey 薄扩展 |
| FC-02 注册与隔离 | COMPLETED | 命名实例、显式默认、重复注册/身份校验、专属键和频道空间 |
| FC-03 单键与资源控制 | COMPLETED | 七个操作；复用内部准入器；命中与同键等待者不占额度；递归/嵌套冷回源明确拒绝 |
| FC-04 L2 / Backplane / locker | COMPLETED | 官方组件接线；L2/null、双进程传播、可选跨进程锁通过 |
| FC-05 容错与失效 | COMPLETED | Fail-Safe、软/硬超时、提前刷新作用域、标签移除/Clear 禁止后备、元数据保留期 |
| FC-06 诊断与测量 | COMPLETED | 原生日志/OTel；实际准入指标；两种 runtime 的五类负载测量 |
| FC-07 样例与文档 | COMPLETED | 内存、Redis、双实例、容错、可选锁/遥测、有界 L1；迁移与索引完整 |
| FC-08 验证收口 | COMPLETED | 新旧测试、CI 自检、文档校验、NuGet 消费通过；生产符号映射与证据完整 |

## 已完成事项

`Bing.Caching.FusionCache` 仅直接引用 Bing.Caching 和官方 FusionCache。Redis、serializer、锁、日志 provider 和遥测扩展在宿主/测试中按需引用。Bing.Caching 仍为 netstandard2.0，仅增加契约和友元声明；既有 ICache/ILocalCache/IRedisCache 的实现与注册未迁移。

默认策略为 TTL 5 分钟、工厂硬超时 2 分钟、8 个活跃工厂、128 个排队工厂、30 秒排队时限、自动恢复队列最多 1000。Fail-Safe、提前刷新、超时后后台完成默认关闭。实际工厂结束前持续持有准入额度。

数据键、标签、Clear 元数据与 Backplane 频道使用固定实例身份隔离。业务 TTL 按毫秒向上取整，每次复制启动配置；标签/Clear 使用单独的长保留期元数据配置，不能被普通业务 TTL 缩短。`ExpireAsync` 为软过期；标签使用 Remove；Clear 明确 `allowFailSafe: false`。

默认 L2 和通知操作等待完成并重新抛出错误；失败前已成功的本地/远端副作用不能回滚。组件生命周期由 DI 和官方组件负责，门面不重复 Dispose。样例和测试展示后台工厂自己创建、释放异步作用域。

## 部分/未完成事项

- 70 项既有 EasyCaching 外部/拓扑/故障环境测试按原门控跳过；未宣称通过。
- 远程 CI、生产环境容量、正式发布门禁未执行，不阻止本次本地实现收口。
- TrySet、扫描、旧持久 outbox、Admin/JWT/权限迁移不属于本任务。

## 修改文件

主要新增目录：

- `framework/src/Bing.Caching.FusionCache/`：net8.0 适配包、选项、注册、门面、命名 provider、内部键/TTL/准入/校验、README。
- `framework/src/Bing.Caching/Bing/Caching/`：`IHybridCache.cs`、`IHybridCacheProvider.cs`、`HybridCacheKeyExtensions.cs`。
- `framework/tests/Bing.Caching.FusionCache.Tests/`：单元与共存测试。
- `framework/tests/Bing.Caching.FusionCache.Tests.Integration/`：真实 Redis、双进程、故障/恢复、资源测量。
- `framework/tests/Bing.Caching.FusionCache.Probe/`：独立 OS 进程测试节点。
- `samples/Bing.Samples.FusionCache/`：可运行样例。
- `docs/migrations/fusioncache-adoption.md`：迁移说明。
- 当前任务目录：计划、执行/性能记录、TRX 摘要、Redis 和包消费复现脚本。

更新：`Bing.All.slnx`、Bing.Caching.csproj 友元、根 README/文档派生索引、缓存模块/架构/包索引/样例索引，以及 CI 单元测试列表和对应证据清单。

## API/数据/配置变化

- 新公共接口的签名遵循计划第 5 节；通过 `CacheEntry<T>.Hit` 区分 null 命中与缺失。
- `AddBingFusionCache(name, configure, configureFusion)` 不替换旧 Provider。Namespace 必填，默认实例必须显式指定。
- 回源准入复用现有 `CacheFactoryAdmission`，未复制一套信号量或同键锁。
- 原生配置通过 builder；身份字段和标签 Remove 语义不可覆盖。启动解析时验证所有命名实例。
- 冷启动使用独立物理空间，不回读旧 payload；灰度期间双空间失效，回滚保持数据库提交后的失效投递。
- 新 `Meter` / `ActivitySource`：`Bing.Caching.FusionCache`。四个工厂指标为 `bing.cache.factory.active`、`waiting`、`rejected`、`wait.duration`（完整名称均有相同前缀）。

## 测试结果

| 验证 | 框架 | 结果 | 证据 |
| --- | --- | --- | --- |
| FusionCache 单元测试 | net8.0 | 43 / 43 通过 | `output/fusioncache-validation/fusioncache-unit-net8.trx` |
| FusionCache 单元测试 | net10.0 | 43 / 43 通过 | `output/fusioncache-validation/fusioncache-unit-net10.trx` |
| FusionCache Redis + 资源 | net8.0 | 9 / 9 通过 | `output/fusioncache-validation/fusioncache-redis-net8.0.trx` |
| FusionCache Redis + 资源 | net10.0 | 9 / 9 通过 | `output/fusioncache-validation/fusioncache-redis-net10.0.trx` |
| 原 Bing.Caching.Tests | net6.0 | 204 / 204 通过 | `output/fusioncache-validation/legacy-cache-net6.trx` |
| 原 Bing.Caching.Tests | net8.0 | 204 / 204 通过 | `output/fusioncache-validation/legacy-cache-net8.trx` |
| 受影响 EasyCaching 回归 | net8.0 | 133 通过、70 门控跳过 | `output/fusioncache-validation/legacy-easycaching-net8.trx` |

结构化摘要：[validation-evidence.json](../../../artifacts/ai-tasks/20261024-fusioncache-integration/validation-evidence.json)。EasyCaching 的 TRX `notExecuted` counter 为 0，但实际 `UnitTestResult outcome=NotExecuted` 有 70 条，跳过数量以逐条结果为准。

Redis 验证使用临时 Redis 7.4 Alpine，固定映射到本次选择的回环地址端口，命名空间每项随机。测试直接断言双进程读取结果，包括更新、删除、标签、Clear 收敛、共享 L2/null、可选锁合并和 Redis 停启自动恢复。所有任务创建的 Redis 容器已清理；未操作原有 Redis/MySQL/PostgreSQL 容器。

复现命令：

```powershell
dotnet test framework/tests/Bing.Caching.FusionCache.Tests/Bing.Caching.FusionCache.Tests.csproj -f net8.0
dotnet test framework/tests/Bing.Caching.FusionCache.Tests/Bing.Caching.FusionCache.Tests.csproj -f net10.0
pwsh -NoProfile -File ai_docs/tasks/20261024-fusioncache-integration/verify-redis.ps1
dotnet test framework/tests/Bing.Caching.Tests/Bing.Caching.Tests.csproj -f net6.0
dotnet test framework/tests/Bing.Caching.Tests/Bing.Caching.Tests.csproj -f net8.0
dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj -f net8.0
```

## NuGet 包消费验证

以命令行覆盖版本 `7.0.0-fusioncache.20261024.3` 执行 Release 打包，不修改仓库版本文件、不发布包。Bing.Core/Bing.Caching 保持 netstandard2.0；Bing.Caching.FusionCache 为 net8.0。新包的 nuspec 直接依赖精确为 Bing.Caching 与 ZiggyCreatures.FusionCache 2.9.0；仅在新包中将继承的 SourceLink 构建引用设为 PrivateAssets，未更改旧包依赖。

独立消费者只有 PackageReference，从本地 feed + nuget.org 还原，使用独立包目录；net8/net10 均构建零警告、零错误并运行通过。覆盖 null/缺失、回源复用、标签/Clear、命名/默认选择及 DI 释放，NU1605 按错误处理。

摘要：[nuget-consumer-evidence.md](nuget-consumer-evidence.md)。原始结果：`output/fusioncache-package-validation/7.0.0-fusioncache.20261024.3/`。本次实际命令如下；再次执行时需使用未使用过的预发布版本，避免旧输出干扰。

```powershell
pwsh -NoProfile -File ai_docs/tasks/20261024-fusioncache-integration/verify-nuget-consumer.ps1 -PackageVersion 7.0.0-fusioncache.20261024.3
```

原始 JSON 按仓库约定存于 `artifacts/ai-tasks/20261024-fusioncache-integration/`，不入库；可审查结论、测试映射和复现脚本保存在任务目录。

## 生产符号 → 测试方法

Unit = Bing.Caching.FusionCache.Tests；Redis = Bing.Caching.FusionCache.Tests.Integration。以下均为最终真实方法名。

| 生产符号/关键行为 | 项目 / 测试方法 |
| --- | --- |
| IHybridCache.TryGetAsync / null 与缺失 / key | Unit / FusionCacheManagerTest.NullHit_IsDifferentFromMissing_AndPreservesLogicalKey |
| GetOrCreateAsync / null 与失败恢复 | Unit / NullFactory_IsCached_ButFailureIsNot、TypeMismatch_IsReported |
| HybridCacheKeyExtensions | Unit / CacheKeyExtensions_UseTheSameEntry |
| FusionCacheEntryOptionsMapper | Unit / Options_AreCopied_AndPositiveSubMillisecondTtlRoundsUp、ExplicitTtlExpires_WithoutChangingDefault |
| FusionCacheKeyNormalizer / 输入边界 | Unit / UnpairedSurrogate_IsRejectedBeforeEncoding、InvalidKeyFailsBeforeFactory、InputLimits_RejectOversizedKeysAndTagsWithoutWriting、Encoding_DoesNotCollideOrInterpretWildcards |
| AddBingFusionCache / provider / 默认与身份 | Unit / NamedCache_HasNoImplicitDefault_AndMissingNameFails、DuplicateNamesAndDefaultsFailAtRegistration、IdentityAndInvalidationContractsCannotBeOverridden、RegisteredOptionsAreSnapshotted_AndDefaultsAreConservative |
| 启动验证 / L2 组件缺失 | Unit / HostStartupResolvesNamedCaches_AndMissingL2Fails、MissingNamespaceAndInvalidBudgetsFail |
| 工厂同键合并 / 命中不占额度 | Unit / SameKeyCoalesces_AndHitsDoNotUseAdmission |
| FusionCacheFactoryInvoker / 活跃与排队 / 拒绝 | Unit / DifferentKeysRespectActiveAndQueueLimits_AndRecoverAfterRejection、QueueTimeoutAndFactoryFailureReleaseCapacity |
| 准入取消 / 真实工厂生命周期 | Unit / QueuedCancellationReleasesQueueSlot、IgnoringCancellationKeepsPermitUntilFactoryReallyEnds、CallerCancellationReachesActiveFactory |
| 递归调用链恢复 | Unit / RecursiveFactoriesFailPromptly_AndRestoreContext |
| Expire / Fail-Safe | Unit / ExpireAllowsFailSafe_ButTryGetNeverReturnsStale、FailSafeRetentionEventuallyExpires |
| 软超时与后台完成 | Unit / SoftTimeoutReturnsFallback_AndBackgroundCompletionKeepsPermit、SoftTimeoutWithoutFallbackWaitsForFactory |
| 标签/Clear/Remove 禁止旧值后备 | Unit / ExplicitInvalidationDisallowsFailSafe；Redis / TagAndClearMarkersKeepLongRetention_IndependentOfBusinessTtl |
| Eager Refresh / 独立 scope | Unit / EagerRefreshUsesIndependentScope_AndConsumesFactoryBudget |
| 资源所有权 / 对象克隆 | Unit / ExternalMemoryCache_IsDisposedOnceByItsDiOwner、MutableValuesShareReferences_UnlessAutoCloneIsEnabled |
| 新旧 Provider 共存 | Unit / LegacyEasyCachingAndFusionResolveTogether_InEitherRegistrationOrder |
| 前台通知错误 | Unit / ForegroundBackplaneIsAwaited_AndFailureIsReportedAfterLocalWrite |
| L2 + serializer + TTL | Redis / L2Hit_PreservesNullAndAvoidsFactory、L2SerializerMismatchIsReported_AndConfiguredTtlExpires |
| 数据/元数据/频道命名隔离 | Unit / NamedInstances_IsolateDataTagsAndClear；Redis / SharedRedis_IsolatesNamesNamespacesAndMetadata |
| 双进程 Backplane | Redis / TwoProcesses_ConvergeAfterUpdateRemoveTagAndClear |
| 官方可选分布式锁 | Redis / OptionalDistributedLocker_CoalescesAcrossProcesses |
| 迟到工厂竞争边界 | Redis / LateFactory_CanOverwriteExplicitSetOrRemoveInL2 |
| Redis 故障与自动恢复 | Redis / RedisFault_ReportsForegroundFailure_AndAutoRecoveryReplays |
| 准入指标及资源测量 | Unit / FactoryMetrics 支撑工厂边界断言；Redis / MeasureL1L2AndColdFactoryWorkloads |
| CI 清单与证据清单一致性 | Invoke-ReleaseCandidateValidation.ps1 -SelfTest / Unit inventory self-test |

## Build/Typecheck/Lint/Format

- 新适配包 restore/build 通过；未出现 NU1605 降级错误。
- 样例 Release net8.0/net10.0 构建通过；内存限额、Fail-Safe、OTel、Redis、多实例及可选 locker smoke 通过。
- `python eng/ci/sync-docs-index.py --check` 通过。
- `python eng/ci/check-docs.py`：13 项通过。
- `pwsh -NoProfile -File eng/ci/Invoke-ReleaseCandidateValidation.ps1 -SelfTest`：AppVeyor lane、Reset authorization、Source identity、Unit inventory、FormalHost manifest 自检通过。
- 本任务文件范围 `git diff --check` 通过；新增文件单独验证 UTF-8 和行尾空白。全工作区检查发现基线已有尾随空格，见下一节；未修改它们。
- 未运行整仓库全量构建/SQL Provider 矩阵：本次只新增缓存包和公共契约，未修改 Bing.Data.Sql / ORM / Provider SQL 输出；已覆盖实际影响范围。

## 性能与资源结果

详见 [performance.md](performance.md) 和两份原始 JSON。本地实际行为：64 个同键冷请求回源 1 次；64 个不同键冷请求回源 64 次，活跃峰值 8、排队峰值 56。命中场景回源 0 次。独立测试覆盖队列满、排队超时、取消、异常和实际任务晚于硬超时结束的资源恢复。

不存在同构历史 FusionCache 基线；不宣称相对旧 Provider 的提升比例。生产容量、故障分区/崩溃矩阵、长时间压力与正式性能门禁未执行，不自动开始新一轮修复。

## 计划偏差

1. 为真实双进程验证新增独立 Probe 工程，避免把两个 DI 容器当成两个 OS 进程。
2. 原生 DI 6.0 实现与新 Abstractions 的 keyed service 不兼容；适配器使用普通 singleton 命名集合，保留旧宿主消费能力，没有强行升级旧 Provider 依赖。
3. 标签/Clear 参数单独复制 TagsDefaultEntryOptions，保留其上游元数据时间窗口；业务选项映射只应用于数据操作。
4. CI 聚合器另有严格项目/TFM 白名单，更新脚本列表时同步更新 `eng/Bing.ProviderEvidence.Cli/Program.cs`；自检通过。
5. 迁移文档最终路径为 `docs/migrations/fusioncache-adoption.md`，与候选文件名不同但职责不变。
6. 打包检查发现公共 props 会传播 SourceLink 工具引用，已只在新包设为 PrivateAssets，最终包消费重验通过。
7. 真实 Redis TTL 测试采用秒级保留期；官方 L2 10.0.1 的整秒过期行为已记录于迁移指南，未用 L1 测试冒充 L2 精度。

## 基线问题

执行前工作区已存在其他任务的 Core/EventBus/Uow/SQL-Uow/Admin 测试与任务文档修改，均予保留，没有 reset/restore/clean。全仓库 `git diff --check` 的已有尾随空格位于三个 20261021 计划和 EfCoreSqliteUnitOfWorkTestSupport.cs；本任务限定范围通过。

新旧 Provider 共存测试拉入旧 EasyCaching 依赖时报告已存在的 System.Drawing.Common 5.0.0 / NU1904；这是旧包链问题，新 FusionCache 生产包未新增该依赖。既有 EasyCaching 测试另有 xUnit analyzer 建议。未扩大范围升级旧依赖或修改旧测试。

## 已知问题与边界分类

| 状态 | 边界 | 影响 |
| --- | --- | --- |
| ACCEPTED_LIMITATION | 迟到工厂可能覆盖显式 Set/Remove 后的 L2 | 实测固定 2.9.0；不提供旧租约 fencing，依赖该保证的业务继续使用旧 Provider/版本键 |
| ACCEPTED_LIMITATION | 原生自动恢复是有界进程内队列 | 进程退出可丢操作，不是持久 outbox |
| VERIFIED_BOUNDARY | 8 活跃 / 128 排队 / 排队时限 | 超额或等待超时明确失败；Fail-Safe 可根据业务配置返回旧值，仍有拒绝观测 |
| ACCEPTED_LIMITATION | 官方 Redis L2 物理 TTL 为整秒 | 亚秒 TTL 不保证驻留，配置和测试需按真实 L2 精度设计 |
| ACCEPTED_LIMITATION | SizeLimit 单位并非字节；元数据 Size=0 | 样例不是总内存硬上限，标签基数和保留窗口须单独预算 |
| ACCEPTED_LIMITATION | Host 不能强行终止忽略取消的工厂 | 应用负责后台工作排空；本适配器额度到真实工厂结束才释放 |
| NOT_APPLICABLE | SQL 元数据/SQLite/SQL Benchmark 门槛 | 未修改对应生产路径 |
| NOT_VERIFIED | 正式远程 CI / 生产容量 / 70 项旧外部场景 | 明确与本次实现完成状态分离 |

## 风险与回归关注点

Backplane 是最终一致性传播，失效前后存在可见窗口。所有节点需采用一致 serializer/schema，并确保元数据保留期覆盖业务最长存活窗口。Redis/Backplane/locker 不应直接共享会被多个组件释放的同一个外部 multiplexer。默认配置与可运行样例明确区分，开启后台工厂的业务必须自建作用域。

## Reviewer 注意事项

重点复查原生 builder 的最终校验时机、数据与元数据选项分离、实际工厂内的准入、启动配置快照、旧 DI/Provider 并存及真实进程测试。测试没有添加旧 Provider 的“显式修改胜过迟到工厂”保证；竞争测试专门固定并记录了不具备此保证的行为。

## Git 状态

仅修改工作区，未暂存。未自动 git commit；未自动 git push；未自动创建 PR；未发布 NuGet。任务 finalizer 以 `--no-notify` 收口，不发送外部通知。

已执行 `node .agents/scripts/task-finish.mjs 20261024-fusioncache-integration --no-notify`，结果 `ok: true`、`finalStatus: COMPLETED`、`notificationStatus: skipped`。

## Review 修复记录

### Round 1

- 模式：REVIEW_FIX；来源 codex；Fix Scope：all（用户要求补齐已列出的全部 10 项）。
- Review 输入：[review-round1.md](review-round1.md)，NEEDS_FIX；8 项 SHOULD_FIX、2 项 OPTIONAL，无 MUST_FIX。
- 本轮开始：2026-10-24T02:19:29.783Z。
- 变更影响：新 FusionCache 适配包、单元/Redis/Probe、样例和对应文档。未修改旧 Provider、SQL 路径或原计划延期的能力。
- 主代理实现和集成 FIX-001～005、008～010；luna_worker 实现 FIX-006/007 的窄范围测试与测量。主代理补充 Clear 预取消修复、官方具名状态指标断言、故障恢复性能接线并完成最终验证。
- 旧 Bing.Caching net6/net8 各 204 项、EasyCaching 133 通过/70 外部门控跳过，以及 CI inventory 自检继续复用上一轮证据；本轮未修改相应生产路径或 CI 清单。新旧 Provider 同宿主测试本轮重新通过；测试 Host 保持 6.0.1 实现，避免为了关闭测试强行升级旧 DI 实现。

#### FIX-001：元数据窗口约束

- 严重程度 MEDIUM；SHOULD_FIX；执行状态 COMPLETED；Finding CLOSED（Executor 结论）。
- 根因：此前仅保留默认长窗口，未验证调用方覆盖后的物理保留关系。
- 修改：新增 `Internal/FusionCacheRetentionValidator.cs`；Manager 在实例解析时验证，EntryOptionsMapper 对每次 TTL 覆盖验证。取数据 L1/L2 最大物理窗口与元数据各层最短物理窗口比较，包含 Fail-Safe 和 L1 jitter；拒绝元数据跳过读写。元数据逻辑 TTL 短但物理保留充分的配置仍允许。
- 验证：`UnsafePhysicalWindowFailsAtResolution` 四分支、`PerCallTtlCannotOutliveMarkers_AndDoesNotExecuteFactory`、`LongPhysicalMarkersPreventResurrection_AfterLogicalExpiry` 的 tag/Clear 分支、真实 Redis `L2AndFailSafeRetentionCannotOutliveMetadata`、`TagAndClearMarkersKeepLongRetention_IndependentOfBusinessTtl` 均通过。失效后等待期间不先读条目，避免惰性清理掩盖复活。

#### FIX-002：Backplane 初次订阅就绪

- 严重程度 MEDIUM；SHOULD_FIX；执行状态 COMPLETED；Finding CLOSED。
- 根因：原生产默认异步订阅，而测试单独设置同步等待；原生订阅层还可能吞掉最终异常。
- 修改：注册器默认并强制 `WaitForInitialBackplaneSubscribe=true`；新增 `FusionCacheStartupBackplane` 包装器记录同步订阅结果，Manager 对首次订阅失败明确抛错。去除 Probe 的特殊等待设置，生产/样例/测试一致。
- 验证：`StartupWaitsForSubscribe_AndUnsubscribesWithoutDisposingExternalBackplane`、`FailedInitialSubscribeFailsStartup_AndAsyncOverrideIsRejected`，以及默认注册的 `TwoProcesses_ConvergeAfterUpdateRemoveTagAndClear` 均通过。就绪不承诺通知已被所有远端消费。

#### FIX-003：关闭与资源所有权

- 严重程度 MEDIUM；SHOULD_FIX；执行状态 COMPLETED；Finding CLOSED。
- 根因：此前只有请求 scope/MemoryCache 释放证据，缺少正在执行工厂时的宿主停止和连接证据。
- 修改：样例新增 `SampleFactoryLifetime` 和 `shutdown` 模式，在实际工厂内跟踪工作，停止接收新回源，等待独立 scope 释放；排空器最后注册。容器 Dispose 前必须等待 StopAsync 成功，预算到期报告取消。没有新增公共 drain API，也不把应用工厂排空说成原生恢复队列持久化。
- 验证：`HostShutdown_DrainsBackgroundFactoryBeforeScopeAndContainerDisposal`、`DiOwnedLockerDisposesItsDedicatedConnection_ExternalInspectorRemainsUsable`、订阅包装器外部组件不重复 Dispose 及真实 Redis 解除订阅均通过；shutdown 样例在 net8/net10 可运行。

#### FIX-004：真实进程崩溃窗口

- 严重程度 MEDIUM；SHOULD_FIX；执行状态 COMPLETED；Finding VERIFIED_BOUNDARY。
- 修改：Probe 增加崩溃窗口模式，恢复延迟一分钟；测试支持显式杀死本任务子进程。
- 验证：`CrashedProcessLosesPendingRecovery_NewProcessReadsPreviousL2Value`。Redis 保存已确认值 1 后停止服务；原节点本地写入 2 后被强制退出；Redis 恢复、全新节点读到 1，随后写入 3 正常。证明进程内待恢复操作可能丢失，不实现持久 outbox。
- 调试记录：首次采用 Docker pause，观察到值 2 在恢复后出现；TCP 中已发送的命令即使客户端超时/退出仍可能执行。改用 SAVE + stop/start 隔离「连接已断、恢复工作仅在进程内」窗口，没有删除该事实或把超时解释为无远端副作用。

#### FIX-005：官方锁超时与故障策略

- 严重程度 MEDIUM；SHOULD_FIX；执行状态 COMPLETED；Finding VERIFIED_BOUNDARY。
- 修改：测试宿主允许专属 locker 配置；使用真实官方 Redis locker、独立 ACL 用户定向拒绝锁命令，L2/Backplane 保持正常。测试后恢复权限并删除该任务用户。
- 验证：`LockTimeoutFallsBackToFactory_EvenWithRethrow_AndReleasedLockCanBeAcquiredAgain`：80ms 获取超时后仍继续回源，两节点各回源一次，释放后恢复。`OfficialLockerPermissionFailure_ThrowsOrDegradesAsConfigured_ThenRecovers`：不重抛时冷回源执行一次，旧值场景工厂失败后 Fail-Safe；重抛时包装 RedisServerException、工厂次数为零，即便有旧值也不遮蔽锁获取异常。恢复权限后新回源成功。
- 文档明确这不是严格互斥或严格一次。

#### FIX-006：默认值、预取消与租户标签

- 严重程度 MEDIUM；SHOULD_FIX；执行状态 COMPLETED；Finding CLOSED。
- 修改：新增 `FusionCacheContractBoundaryTest` 三项直接用例。测试发现 Clear 预取消实际会继续执行，已在 `FusionCacheManager.ClearAsync` 入口补上检查；其它六个操作沿用已通过的原生预取消语义。
- 验证：`TryGet_DistinguishesMissingFromValueTypeDefault`、`PreCanceledToken_CancelsEveryOperationBeforeFactoryOrMutation`、`TenantPrefixes_IsolateKeysAndTagsWithinOneInstance` 均通过；同一实例中 A 标签失效保留 B 数据，不新增自动租户识别。

#### FIX-007：诊断和完整测量字段

- 严重程度 MEDIUM；SHOULD_FIX；执行状态 COMPLETED；Finding CLOSED。
- 修改：FactoryMetrics 监听 double 等待直方图；诊断测试使用官方 Meter/ActivitySource 常量，验证 `fusioncache.cache.hit`、`fusioncache.cache.miss`、`fusioncache.failsafe_activate`，并检查 Bing 指标/活动不携带原始 key/tag。原生 trace 确实带键，文档说明导出前删除/脱敏 `fusioncache.operation.key` / `fusioncache.operation.tag`。
- 测量增加 P99、进程 CPU、WorkingSet 起始/结束/10ms 采样峰值、原生业务数据 Hit/Miss/Fail-Safe 次数。测量专用同步事件回调避免异步计数滞后，过滤 `:d:` 以排除元数据。单样本恢复 P50/P95/P99 均为 null。
- 验证：`EventsMetricsAndActivities_ReportRealCacheBehaviorAndBoundSensitiveDimensions`、`MeasureL1L2AndColdFactoryWorkloads` 与故障恢复测量通过。每个 TFM 5 类负载均有完整数值，见 [performance.md](performance.md)。64 个同键请求回源 1 次，64 个不同键请求回源 64 次；后者活跃/排队峰值 8/56。故障恢复 Fail-Safe 实际计数为 1。

#### FIX-008：回滚前旧空间处置

- 严重程度 MEDIUM；SHOULD_FIX；执行状态 COMPLETED；Finding CLOSED。
- 修改：[迁移指南](../../../docs/migrations/fusioncache-adoption.md) 明确先恢复双空间失效，再失效旧业务域后冷启动，或先失效再从权威数据预热，或切换全新旧 Provider 命名空间；完成后再切流。持续双空间失效直到所有新读者/工厂退出。
- 验证：逐项对照计划回滚步骤，13 项文档检查及索引检查通过。没有把跨空间操作当成数据库原子事务。

#### FIX-009：频道直接隔离证据

- 严重程度 LOW；OPTIONAL；执行状态 COMPLETED；Finding CLOSED。
- 修改与验证：`SeparateChannelsReceiveOnlyOwnIdentityMessages_AndSubscriptionsDisappearOnDispose` 使用真实 Redis PUBSUB CHANNELS 及原生 MessageReceived 事件；两组身份各发 Set/Remove/tag/Clear 和结束消息，断言收到的键只属本身份，两个订阅方各收到至少五条消息；释放后实际频道仅剩 inspector。通过 net8/net10，不只比较最终缓存值。

#### FIX-010：故障来源断言

- 严重程度 LOW；OPTIONAL；执行状态 COMPLETED；Finding CLOSED。
- 修改与验证：L2 故障要求 `FusionCacheDistributedCacheException` 包含 RedisException；STJ 类型不兼容要求 `FusionCacheSerializationException` 包含 JsonException，均已通过。
- 原 `L2SerializerMismatchIsReported_AndConfiguredTtlExpires` 更名为 `L2SchemaTypeMismatchIsReported_AndConfiguredTtlExpires`，限定证据为相同 STJ 下 string/Product schema 类型不兼容；不再推广成所有 serializer 配置变更已验证。

### Round 1 最终验证与追溯

上述每个 FIX 的实际方法名构成本轮追加的「最终生产符号 → 测试方法」映射；没有变动的原映射继续适用，FIX-010 名称替换旧方法。

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| 新 Unit / Release / net8.0 | 57 通过，0 跳过 | `output/fusioncache-validation/review-fix-1/unit-net8.0.trx` |
| 新 Unit / Release / net10.0 | 57 通过，0 跳过 | `output/fusioncache-validation/review-fix-1/unit-net10.0.trx` |
| 最终具名指标断言 / 两 TFM | 各 1 通过 | `diagnostics-final-net8.0.trx` / `diagnostics-final-net10.0.trx`；只重跑直接受影响测试 |
| Redis / Release / net8.0 | 15 通过，0 跳过 | `output/fusioncache-validation/review-fix-1/redis-net8.0.trx` |
| Redis / Release / net10.0 | 15 通过，0 跳过 | `output/fusioncache-validation/review-fix-1/redis-net10.0.trx` |
| NuGet 消费 net8/net10 | PASS；零构建警告/错误，核心仍仅两项消费依赖 | `output/fusioncache-package-validation/7.0.0-fusioncache.20261024.4/result.md` |
| 样例 | net8/net10 shutdown 通过；net10 multi --locker 通过 | 控制台输出确认排空及更新收敛 |
| 文档 | 13 项检查通过、索引同步通过 | `check-docs.py` / `sync-docs-index.py --check` |
| Diff / 编码 | 本任务范围差异检查、UTF-8/行尾检查通过 | 不修改原工作区其它任务的既有空白问题 |

复现：`dotnet test framework/tests/Bing.Caching.FusionCache.Tests/Bing.Caching.FusionCache.Tests.csproj -c Release -f <net8.0|net10.0>`；Redis 使用 `verify-redis.ps1 -Configuration Release`；打包消费者脚本使用未使用过的预发布版本，避免缓存旧包。

本轮临时 Redis `bing-fusioncache-fix-2ca441d0` 仅绑定 `127.0.0.1:55634`，测试完成后已删除。仅测试创建的子进程和 ACL 用户被清理，没有触碰用户既有 Redis/MySQL/Postgres。保留原 review.md 和其它工作区修改，未提交、推送或发布。

### Round 1 汇总

- MUST_FIX：0；SHOULD_FIX：8/8 完成；OPTIONAL：2/2 完成；PARTIAL/BLOCKED/FAILED：0。
- IMPLEMENTATION_STATUS / TEST_STATUS / PACKAGE_CONSUMER_STATUS：PASS（本轮范围）。
- OPEN_ACTIONABLE：0（Executor 结论）；EXTERNAL_GATE_STATUS：NOT_VERIFIED（远程 CI / 生产容量）；RELEASE_STATUS：NOT_EVALUATED。
- luna_worker 对其未编写的 FIX-001/002 核心与 FIX-003 生命周期示例做了独立只读复核，未报告具体缺陷；该复核没有重新运行测试，也不冒充完整 Reviewer PASS。
- 下一步：完整独立 Review。原 review.md 的 NEEDS_FIX 历史状态不由执行者改写；没有因外部门禁未执行而开启新 Fix。
- No-Progress Check：CHANGED；当前范围实现和必要验证完成，停止扩展范围。

本轮 finalizer 已执行：ok=true，finalStatus=COMPLETED，mode=review-fix，reviewRound=1，notificationStatus=skipped。未发送外部通知。


### Round 2：FIX-005 同一锁释放与重获证据

- 模式：REVIEW_FIX；Fix Scope：recommended；本轮唯一处理项 FIX-005（MEDIUM / SHOULD_FIX）。
- Review 输入：[review.md](review.md)，第二轮 NEEDS_FIX。保留 Reviewer 报告原状，不由 Executor 改写为 PASS。
- 执行状态：COMPLETED；Finding：CLOSED（Executor 结论，待独立 Review）。
- 根因：原测试释放 `contended` 后验证 `after-release`，新键成功不能证明原锁释放；仅检查回源成功也无法区分真实拿锁与超时降级。
- 修改文件：`framework/tests/Bing.Caching.FusionCache.Tests.Integration/FusionCacheLockerTest.cs`、`framework/tests/Bing.Caching.FusionCache.Probe/RedisTestHost.cs`、本执行报告。
- 委派：luna_worker 完成窄范围测试及宿主装配修改；主代理检查、调整所有权/时限断言、修正编译问题、运行真实 Redis 验证并收口。

#### 修复行为

1. 测试专用 RecordingDistributedLocker 透明转发真实官方 Redis locker 的同步/异步获取与释放，记录实际 lockName、句柄、调用标识和获取开始时间，不模拟 Redis 结果。
2. 原工厂等待期间确认已取得非空句柄且尚未释放；原 80ms 竞争超时、两节点各回源一次的断言保留。工厂退出后确认同一句柄经官方释放路径完成。
3. 直接对记录的同一 lockName 调用官方 AcquireLockAsync，要求返回非空句柄；绕过缓存读写和工厂路径，排除缓存命中及超时回源降级。新句柄在 finally 中释放。
4. 从实际 locker 配置读取只读 AbandonTimeout（固定版本默认为 30 秒），要求从原获取开始到重获完成小于其一半（15 秒）；直接获取另有 5 秒 timeout/token。获取前后均检查年龄，排除等到原锁自然过期才成功。
5. RedisTestHost 只增加可选 decorateLocker 装配入口；默认仍返回原官方实例。原始 locker 仍仅由具体类型的 DI 注册管理释放，不增加同一 IDisposable 的第二份 DI 注册。包装器不取得所有权。
6. 等待工厂进入及退出均有界，并在 finally 释放工厂完成信号；保留 ACL 故障、重抛/降级、Fail-Safe 原断言。

#### 变更影响与验证范围

- ChangedProjects：FusionCache.Tests.Integration、FusionCache.Probe（仅测试工程）。
- ChangedPublicContracts / RuntimePaths / Providers：生产公共 API、生产执行路径和 Provider 均无修改；只调整测试的 locker 观察与装配。
- ChangedTFMs / BuildPackaging / BenchmarkHarness：无目标框架、依赖、打包或性能测量改动。
- AffectedDependents：使用可选分布式锁的集成测试节点；默认未包装路径纳入回归。
- RiskLevel：LOW。L0 编译/格式/编码，L1/L3 精准真实 Redis；不重跑无关 Unit 全量、旧 Provider、包消费及性能矩阵。

#### 测试证据

| 验证 | net8.0 | net10.0 |
| --- | --- | --- |
| LockTimeoutFallsBackToFactory_EvenWithRethrow_AndReleasedLockCanBeAcquiredAgain | PASS，约 120ms | PASS，约 319ms |
| OfficialLockerPermissionFailure_ThrowsOrDegradesAsConfigured_ThenRecovers | PASS | PASS |
| OptionalDistributedLocker_CoalescesAcrossProcesses | PASS | PASS |
| DiOwnedLockerDisposesItsDedicatedConnection_ExternalInspectorRemainsUsable | PASS | PASS |
| 合计 | 4 通过 / 0 失败 / 0 跳过 | 4 通过 / 0 失败 / 0 跳过 |

上表耗时为整个测试方法耗时，不冒充单次拿锁延迟。最终两目标 Release 构建及测试成功，未出现新增编译/analyzer 提示。

原始 TRX：`output/fusioncache-validation/review-fix-2/locker-net8.0.trx`、`locker-net10.0.trx`。本轮运行脚本：`output/fusioncache-validation/review-fix-2/run-locker-tests.ps1`，执行命令：

```powershell
pwsh -NoProfile -File output/fusioncache-validation/review-fix-2/run-locker-tests.ps1 -Configuration Release
```

脚本在临时 Redis 7.4 上对两个 TFM 使用下列过滤器：

```text
FullyQualifiedName~FusionCacheLockerTest|FullyQualifiedName~OptionalDistributedLocker_CoalescesAcrossProcesses|FullyQualifiedName~DiOwnedLockerDisposesItsDedicatedConnection_ExternalInspectorRemainsUsable
```

首次编译发现 2.9.0 的 AbandonTimeout 为只读，不能按初稿赋值；改为读取实际配置，并修正本次新增的 xUnit analyzer 提示后重新验证。首次未进入测试，不计作通过。最终容器 `bing-fusioncache-fix2-e7c71930bfc1` 只绑定 `127.0.0.1:51621`；首次容器 `bing-fusioncache-fix2-a5c755bec46f` 与最终容器均已删除。ACL 用户由原用例 finally 清理，未操作用户既有容器。

#### 最终生产符号 → 测试方法补充

| 生产符号 / 关键行为 | 测试项目 / 方法 |
| --- | --- |
| FusionCacheManager.GetOrCreateAsync → 官方 RedisDistributedLocker.AcquireLockAsync/ReleaseLockAsync，同一 lockName 释放后真实重获 | Integration / FusionCacheLockerTest.LockTimeoutFallsBackToFactory_EvenWithRethrow_AndReleasedLockCanBeAcquiredAgain |
| 官方 locker 异常策略、跨进程合并、DI 专属连接释放 | Integration / OfficialLockerPermissionFailure_ThrowsOrDegradesAsConfigured_ThenRecovers、OptionalDistributedLocker_CoalescesAcrossProcesses、DiOwnedLockerDisposesItsDedicatedConnection_ExternalInspectorRemainsUsable |

本节补足 Round 1 对“释放后恢复”过宽的证据声明。原锁超时/ACL 策略不变，仍不承诺严格一次或业务互斥。

#### Round 2 汇总

- Task：20261024-fusioncache-integration；Implementation：本轮 1/1 修复完成；MUST_FIX 0、SHOULD_FIX 1/1；PARTIAL/BLOCKED/FAILED 0。
- IMPLEMENTATION_STATUS / TEST_STATUS / RESOURCE_EVIDENCE_STATUS：PASS（本轮范围）；OPEN_ACTIONABLE：0（Executor 结论）。
- Release Gates：本轮未新增发布门禁；EXTERNAL_GATE_STATUS：NOT_VERIFIED；RELEASE_STATUS：NOT_EVALUATED。
- Changed Scope：两个测试文件及 execution.md；review.md/plan.md 保持原状。
- Validation Run：L0 Release 编译、任务范围 diff、UTF-8/行尾；L1/L3 两 TFM 各 4 项真实 Redis；L2/L4/L5 不重跑。
- Reused Evidence：Review 2 已接受的其余 9 项及未改变的 Unit、旧 Provider、包消费、性能范围证据，不宣称本轮重跑。
- Completed：FIX-005；Open Actionable：无；Blocked Approval：无。
- Blocked External：远程 CI、生产容量、旧 EasyCaching 门控场景；不据此追加修复。
- Not Applicable：SQL/SQLite、历史同构性能基线。
- Accepted Limitations：最终一致、进程内恢复、不保证严格互斥/一次。
- Verified Boundaries：同一锁真实持有、释放及在自然过期前重获；竞争超时/故障策略继续通过。
- Deferred：原计划排除的 TrySet、扫描、持久 outbox、Admin/JWT/权限迁移等。
- No-Progress Check：CHANGED；Next Action：STOP，修复交付后由独立 Review 判断最终通过。
- Goal Status：未启动自动循环；当前 REVIEW_FIX 完成。未暂存、commit、push 或发布。

本轮 finalizer：`task-finish.mjs --no-notify` 返回 `ok=true`、`finalStatus=COMPLETED`、`reviewRound=2`、`notificationStatus=skipped`。未发送外部通知。


## 用户授权后续：依赖安全、L1 分配与冷启动

### 结论与范围

2026-10-24，execute-plan 后续实施完成。用户明确要求修复历史依赖风险、优化 L1 分配、分析并列出冷启动。本轮没有发起新的自动 Review/Fix 循环，也没有改写 review.md 的 PASS_WITH_ISSUES 历史证据。原 OPTIONAL FIX-011 已按用户新授权修复（Executor 结论）。

- IMPLEMENTATION_STATUS / TEST_STATUS / PACKAGE_CONSUMER_STATUS：PASS（本轮范围）。
- PERFORMANCE_EVIDENCE_STATUS：PASS（固定探针分配差异）；生产延迟、吞吐和容量不由本地探针代替。
- OPEN_ACTIONABLE：0；Next Action：STOP；No-Progress Check：CHANGED，本轮三项交付完成。
- EXTERNAL_GATE_STATUS：NOT_VERIFIED；RELEASE_STATUS：NOT_EVALUATED；未启动 Goal。
- ACCEPTED_LIMITATION：有界回源排队、GetOrCreate 的逐次选项/闭包分配、已结束维护的历史包版本线、最终一致和进程内恢复。
- DEFERRED：原计划排除的扫描、原子 TrySet、持久 outbox 和 Admin/JWT/权限迁移不变。

### 修改结果

1. Bing.Core 显式约束 System.Text.Json 最低版本 6.0.10，修补 Microsoft.Extensions.DependencyModel 6.0.0 传递的 CVE-2024-43485 / GHSA-8g4q-xg66-9fp4。没有升级全局 MicrosoftPackageVersion。
2. 测试暴露旧 EasyCaching 的另一条历史风险：EasyCaching.Redis/Bus.Redis → StackExchange.Redis 2.5.43 → PerformanceCounter/ConfigurationManager/Permissions/Windows.Extensions → System.Drawing.Common 5.0.0。Bing.EasyCaching 以 5.0.3 最低版本修补 CVE-2021-24112；保持 Provider 版本和 netstandard2.0 兼容。该公告针对 Linux/macOS 图形解析，不把包存在等同于缓存路径可利用。
3. FusionCacheManager.TryGetAsync 使用实例级私有读取选项快照，强制 AllowStaleOnReadOnly=false。固定版本 2.9.0 的 TryGet 调用链不修改选项，也不把它传给业务工厂或 Backplane。写入/回源仍逐次复制，运行时改动原生默认选项不影响门面快照。升级上游版本时需重新检查这一只读前提。
4. 新增行为回归 TryGetKeepsFrozenReadPolicy_AutoClonesFreshValuesAndRejectsExpiredStaleValue：冻结后改原生默认选项，连续/并发读取仍返回独立克隆，软过期仍 miss。
5. 新增 cold-start.md 并更新迁移指南，分类说明 L1 冷/L2 热、全空间冷、首次键、集中 TTL、失效、淘汰、故障和版本迁移，以及 Eager Refresh 的边界。提供小批热门键预热示例，不自动扫描或全量预热。
6. 本地纯 L1 探针 net8/net10 均为 TryGet 424 → 208 B/op，减少 216 B/op（50.94%）；GetOrCreate 保持 608 B/op。没有为了命中路径增加额外预读，也没有修改公共 Task 返回签名。延迟单轮数据只作观察，不宣称稳定加速比例。

来源：[JSON 公告](https://github.com/advisories/GHSA-8g4q-xg66-9fp4)、[微软图形组件公告](https://github.com/dotnet/announcements/issues/176)、[JSON 6.0.10](https://www.nuget.org/packages/System.Text.Json/6.0.10)、[Drawing 5.0.3](https://www.nuget.org/packages/System.Drawing.Common/5.0.3)。历史分支已结束维护；这是兼容补丁，不是长期支持升级或全仓库安全审计。

### Change Impact Analysis

- ChangedFiles/Projects：Bing.Core.csproj、Bing.EasyCaching.csproj、FusionCacheManager.cs、FusionCachePolicyTest.cs、当前任务 plan/execution/performance/cold-start 及迁移指南。
- ChangedPublicContracts / Providers / TFMs：无公共签名、Provider 选择或目标框架改动。
- ChangedRuntimePaths：TryGet L1 命中、L2 读取/回填的选项生命周期；其他操作策略不变。
- ChangedBuildPackaging：Core/EasyCaching 增加已存在传递包的补丁最低版本；FusionCache 仍仅有两个直接运行时依赖。
- ChangedBenchmarkHarness：无；保持原探针、输入、预热/样本数和 Runtime，原 before 不覆盖。
- AffectedDependents：旧缓存、EasyCaching、新 FusionCache、样例及打包消费者。RiskLevel：MEDIUM（共享依赖与只读选项复用）。
- 验证 L0/L1/L2/L3：局部构建、旧/新项目回归、读取策略、Redis、依赖审计及包消费；L4 只重测改动的 L1 分配。没有运行全解决方案或重复冷启动性能矩阵。

### 验证结果

结果目录：`output/fusioncache-optimization/`。候选生产/测试文件 SHA256、审计摘要、TRX 计数和包解析结果见 `candidate.json`。

| 项目 / 验证 | 目标 | 结果 |
| --- | --- | --- |
| Bing.Core.Tests | net6 / net8 | 各 653 通过，0 失败 |
| Bing.Caching.Tests | net6 / net8 | 各 204 通过，0 失败 |
| FusionCache.Tests（含新增快照测试） | net8 / net10 | 各 58 通过，0 失败 |
| EasyCaching.Tests（补丁后） | net6 / net8 | 各 133 通过、70 原有门控跳过，0 失败 |
| LegacyEasyCachingAndFusionResolveTogether_InEitherRegistrationOrder（最终 EasyCaching 图形补丁后） | net8 / net10 | 各 1 通过；未因只改旧 Provider 依赖而重复整个新包单元测试 |
| FusionCache Redis 集成 | net8 / net10 | 各 14 通过，0 跳过；只排除未改变的 MeasureL1L2AndColdFactoryWorkloads |
| 样例 memory | net8 | PASS，命中复用和标签失效 |
| 三包纯 NuGet 消费 | net8 / net10 | PASS，版本 7.0.0-fusioncache.20261024.5；独立包缓存、本地包 SHA256、依赖、生命周期与行为校验 |
| 四包组合消费（增加 Bing.EasyCaching） | net8 / net10 | PASS；旧 ICache 与新 IHybridCache 的同键数据隔离 |
| 审计 Core / EasyCaching / FusionCache / 样例 / 组合消费者 | 含传递依赖 | 五份 audit-*.json 均未报告已知漏洞 |
| L1 固定探针 | net8 / net10 | 各运行一次，数据见 performance.md |

复现单元：`dotnet test <project.csproj> -c Release --verbosity quiet --logger "trx;LogFilePrefix=<name>" --results-directory output/fusioncache-optimization`（多目标项目一次执行两个 TFM）。Redis：`pwsh -NoProfile -File output/fusioncache-optimization/run-redis.ps1 -Configuration Release -ResultsRoot output/fusioncache-optimization`。其过滤器排除原冷启动测量方法。旧冷启动 64 请求 → 同键 1 工厂、不同键 8 活跃/56 等待证据复用自 Review Fix 1，未冒充本次重跑。

三包打包脚本：`verify-nuget-consumer.ps1 -PackageVersion 7.0.0-fusioncache.20261024.5`，完整记录在 `output/fusioncache-package-validation/7.0.0-fusioncache.20261024.5/`。额外 EasyCaching 包使用相同命令行临时版本打入同一 feed，组合消费项目与日志在本轮结果目录。仓库发布版本没有修改。`security-nuspec.json` 确认 Core/EasyCaching 包声明了 6.0.10/5.0.3 补丁最低版本；Core net6 测试 assets 实际解析 JSON 6.0.10；net8/net10 消费者由 SDK 裁剪该共享框架包，不能要求其 assets 必然有独立 JSON 包记录。纯 L1 运行时仍加载受维护的宿主 JSON 程序集 8.0.30 / 10.0.11。

本轮 Redis 容器 `bing-fusioncache-1d9e4d563a28` 仅绑定本机回环地址，finally 删除；最终 docker ps -a 确认不存在。没有改动既有 Redis 容器或生产缓存。

### 生产符号 → 测试与证据

| 最终生产符号 / 变化 | 直接测试 / 验证 |
| --- | --- |
| FusionCacheManager 构造函数、TryGetAsync 只读快照 | PolicyTest.TryGetKeepsFrozenReadPolicy_AutoClonesFreshValuesAndRejectsExpiredStaleValue |
| TryGetAsync null/缺失、TTL、类型错误与只读策略 | ManagerTest.NullHit_IsDifferentFromMissing_AndPreservesLogicalKey、ExplicitTtlExpires_WithoutChangingDefault、TypeMismatch_IsReported；PolicyTest.MutableValuesShareReferences_UnlessAutoCloneIsEnabled |
| TryGetAsync L2/失效语义 | Integration.L2Hit_PreservesNullAndAvoidsFactory、SharedRedis_IsolatesNamesNamespacesAndMetadata、TagAndClearMarkersKeepLongRetention_IndependentOfBusinessTtl、L2SchemaTypeMismatchIsReported_AndConfiguredTtlExpires |
| Bing.Core/System.Text.Json 依赖与包传播 | Core.Tests 653 × 2；security-nuspec.json；audit-core.json；纯包消费者及 net6 assets |
| Bing.EasyCaching/System.Drawing.Common 依赖与共存 | EasyCaching.Tests 133 × 2；LifetimeTest.LegacyEasyCachingAndFusionResolveTogether_InEitherRegistrationOrder；audit-easy.json；四包组合消费者 |
| TryGetAsync 热路径分配 | 固定 ReviewProbe 中 adapter.tryget；before/after 原始日志 |
| FactoryInvoker/Admission（本次未改） | 复用 FusionCacheResourceTest.MeasureL1L2AndColdFactoryWorkloads，及现有 FactoryTest 限流/取消/异常回收单元回归 |

### 偏差、基线与收口

逐次复制策略仅在不暴露给写入/工厂的只读路径改为私有快照；这是用户授权 L1 优化的有证据局部调整。原 OPTIONAL JSON 补丁扩展到同宿主旧 Provider 审计发现的 Drawing 历史漏洞，均采用最小兼容补丁。未调整队列预算或新增通用预热服务。

Core 测试编译存在其他工作区文件的 CS8632 警告。全仓库 git diff --check 报告其他任务计划及 EfCoreSqliteUnitOfWorkTestSupport.cs 既有尾随空白；本轮文件单独检查通过，不顺手修改。EasyCaching 的 70 项外部门控测试未执行，不标为通过。远程 CI、生产负载、多平台图形解析未验证，不据此自动开启下一轮修复。

文档/测试窄任务由 luna_worker 完成，主代理实现依赖和热路径、审阅集成并执行验证。所有已有工作区改动保留。未自动暂存、commit、push、创建 PR、发布或发送外部通知。

最终测试检查将新增只读回归的八路读取放入 Task.Run，避免同步 L1 命中让 Task.WhenAll 仅验证串行创建的已完成任务。该方法在 net8/net10 各精准重跑 1 项通过，记录为 read-policy-final*.trx；其余不受影响证据复用。


## 用户授权后续：冷启动预热样例

### 交付与行为

2026-10-24，用户要求补充上一轮讨论的冷启动解决方案示例。本轮完成样例与验证，未扩展为生产预热框架。

- 新增 `SampleCacheWarmup.RunAsync`：接收业务提供的有限键清单，每批最多 batchSize 个缓存调用，等待当前批次完成后再调度下一批；不为整个清单一次性创建任务。业务回源继续使用 IHybridCache 的同键合并及准入器。
- 聚合 Requested/Started/Succeeded/Failed/Cancelled/NotStarted/TimedOut/CallerCancelled；成功包含已有缓存命中，宿主另记录 FactoryCalls。单键异常计为失败后继续；协作取消停止新调用，等待当前批次收尾。没有无界逐键结果集合或自动重试。
- 新增 `SampleWarmupHostedService`：StartAsync 预热 12 个示例产品，每批 4 个，默认总预算 5 秒；真实工厂内部创建独立异步 DI scope 并经 SampleFactoryLifetime 跟踪。排空器保持最后注册。
- `warmup` 模式采用尽力预热：单键失败或预算耗尽允许部分完成后继续启动；外部令牌取消则中止启动。它不是所有业务数据就绪的 readiness 承诺。默认顺序启动，不引入并发启动 hosted service。
- 为避免旧值后备、提前刷新或后台完成混淆统计，只有 warmup 模式关闭这些策略；原有其他样例配置保留。
- 增加命令 `warmup`、`warmup --warmup-failure`、`warmup --warmup-timeout`、`warmup --warmup-cancel`。正常预热后再次访问全部产品，若意外回源则抛错。
- README 说明真实接入、参数、失败/取消语义、多节点额度累加、无优先级保证和协作取消边界；cold-start.md 链接运行样例，plan.md 增加本次授权范围。

### Change Impact Analysis 与验证范围

- ChangedFiles：样例 Program.cs、SampleCacheWarmup.cs、SampleWarmupHostedService.cs、README.md；测试 SampleCacheWarmupTest.cs、测试项目中的 Compile Link；任务 plan/execution/cold-start 文档。
- ChangedProjects：Bing.Samples.FusionCache、Bing.Caching.FusionCache.Tests。
- ChangedPublicContracts / Providers / TFMs / Packaging：生产包 API、Provider、目标框架、依赖与打包均无变化；新增类型仅属样例。
- ChangedRuntimePaths：样例 CLI 分支、Host 启动、预热调度和取消。AffectedDependents：样例使用者及链接样例源码的测试。
- ChangedBenchmarkHarness：无。RiskLevel：LOW；测试集中验证并发/取消及实际宿主启动，不重跑生产库、旧 Provider 全量、Redis、NuGet 消费或性能矩阵。上一轮相关生产证据继续复用。
- 委派 luna_worker 编写职责明确的预热行为测试，主代理实现示例、文档、审阅测试并执行最终验证。

### 测试证据与映射

结果目录：`output/fusioncache-warmup/`，文件指纹及测试计数在 `candidate.json`。Release 两目标样例构建均成功，0 警告、0 错误。

| 生产/样例符号 → 测试方法或运行场景 | net8.0 | net10.0 |
| --- | --- | --- |
| SampleCacheWarmup.RunAsync → RunAsync_LimitsConcurrencyAndWaitsBetweenBatches | PASS | PASS |
| RunAsync → RunAsync_ContinuesAfterSingleKeyFailureAndSkipsFactoryOnHit | PASS | PASS |
| RunAsync → RunAsync_CallerCancellationStopsLaterBatchesAndReleasesFactoryPermit | PASS | PASS |
| RunAsync → RunAsync_BudgetCancellationStopsLaterBatches | PASS | PASS |
| RunAsync → RunAsync_PreCancelledTokenDoesNotStartAnyKey | PASS | PASS |
| SampleWarmupHostedService.StartAsync + CLI → warmup | 12 成功，二次读取无回源 | 12 成功，二次读取无回源 |
| StartAsync + 独立工厂作用域 → warmup --warmup-failure | 11 成功、1 失败，继续启动 | 11 成功、1 失败，继续启动 |
| 预算与部分启动 → warmup --warmup-timeout | 预算取消，部分启动 | 预算取消，部分启动 |
| 外部取消 → warmup --warmup-cancel | 取消宿主启动，无成功声明 | 取消宿主启动，无成功声明 |
| 原有 CreateHost/CLI → memory、shutdown | 两种模式均通过 | 两种模式均通过 |

单元测试命令：`dotnet test framework/tests/Bing.Caching.FusionCache.Tests/Bing.Caching.FusionCache.Tests.csproj -c Release --filter FullyQualifiedName~SampleCacheWarmupTest --verbosity quiet --logger "trx;LogFilePrefix=warmup-unit" --results-directory output/fusioncache-warmup`，每目标 5 项，0 失败、0 跳过。

实际样例验证脚本：`pwsh -NoProfile -File output/fusioncache-warmup/verify-samples.ps1`；重新构建后验证两个 TFM × 四种新模式的退出码与关键输出，共 8 项通过。另对两目标 memory/shutdown 各执行一次运行回归。预热超时和外部取消时实际 Started/Cancelled 数随调度变化，测试只约束语义和统计守恒，不声明固定数量。

### 边界与收口

IMPLEMENTATION_STATUS / TEST_STATUS：PASS（本轮样例范围）；OPEN_ACTIONABLE：0；Next Action：STOP。没有新生产性能结论，PERFORMANCE_EVIDENCE_STATUS：REUSED；EXTERNAL_GATE_STATUS：NOT_VERIFIED；RELEASE_STATUS：NOT_EVALUATED。

总预算为协作取消，不能强杀忽略 CancellationToken 的用户代码；本例真实 reader 会响应令牌。预热与实时流量共用回源额度，没有优先级或集群级总预算；业务应提供热门清单并协调多节点错峰。示例的 4 并发/5 秒并非生产参数承诺。只增加显式样例，不自动扫描、全量预热或修改生产默认配置。

检查本轮文件 UTF-8、项目 XML 与尾随空白；保留其他工作区修改及独立 review.md。未暂存、commit、push、创建 PR、发布或发送通知。
