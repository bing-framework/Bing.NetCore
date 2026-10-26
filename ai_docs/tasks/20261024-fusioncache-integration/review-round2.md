<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: 20261024-fusioncache-integration
AI_REVIEWED_AT: 2026-10-24T04:38:51Z

# FusionCache 第二轮独立 Review

## 当前结论

第一轮 10 项问题中，**9 项闭环，FIX-005 部分完成，剩余 1 项 SHOULD_FIX**。本轮没有发现新的生产实现缺陷；剩余问题是锁测试没有直接证明原锁释放后能重新获取，不代表已经发现运行时锁泄漏。

第一轮原始报告保存在 [review-round1.md](review-round1.md)。本报告给出当前 Reviewer 结论，不改写 execution.md 中执行者当时的完成声明。

| 维度 | 状态 |
| --- | --- |
| IMPLEMENTATION_STATUS | PASS：检查范围未发现未解决的生产实现缺陷 |
| TEST_STATUS | PARTIAL：FIX-005 的释放验收断言不足 |
| PACKAGE_CONSUMER_STATUS | PASS：复用固定产物 net8/net10 消费证据 |
| PERFORMANCE_EVIDENCE_STATUS | PASS：本地轻量测量字段齐备，不代表生产容量通过 |
| RESOURCE_EVIDENCE_STATUS | PARTIAL：同一锁释放及重获缺直接证据 |
| EXTERNAL_GATE_STATUS | NOT_VERIFIED：远程 CI、生产容量未验证 |
| RELEASE_STATUS | NOT_EVALUATED |
| OPEN_ACTIONABLE | 1，FIX-005，MEDIUM / SHOULD_FIX |
| REVIEW_STATUS | NEEDS_FIX |

## 计划验收矩阵

| 阶段 | 结论 | 核对结果 |
| --- | --- | --- |
| FC-00 工程依赖 | PASS | net8 独立包、固定依赖、两项核心直接依赖、net8/net10 包消费、旧新 Provider 共存 |
| FC-01 契约 | PASS | 七操作、CacheEntry null/miss、CacheOptions TTL、CacheKey 薄扩展真实接入 |
| FC-02 注册生命周期 | PASS | 命名/默认冲突拒绝、身份隔离、首次订阅就绪与失败检查、组件所有权 |
| FC-03 单键资源控制 | PASS | 实际工厂内准入，默认 8/128/30 秒；同键合并、取消、异常恢复、递归及基础边界测试 |
| FC-04 分布式 | PARTIAL | L2、Backplane、双进程传播和锁故障策略已验证；FIX-005 仍缺同一锁重获证据 |
| FC-05 容错失效 | PASS | TTL 5 分钟、硬超时 2 分钟；默认关闭 Fail-Safe/提前刷新/后台完成；软过期、标签移除、Clear 禁止后备、保留窗口约束 |
| FC-06 诊断 | PASS | 官方日志/遥测、准入及等待直方图、恢复队列上限、五类测量字段 |
| FC-07 样例文档 | PASS | 内存、Redis、多实例、容错、独立 scope 关闭示例及灰度/回滚步骤 |
| FC-08 收口 | PARTIAL | 测试、包消费、符号映射和执行报告已有；补齐 FIX-005 后再确认完整本地验收 |

## 第一轮问题逐项复核

| ID | 状态 | 实现与证据判断 |
| --- | --- | --- |
| FIX-001 | CLOSED | FusionCacheRetentionValidator 比较 L1/L2 数据物理存活上界与元数据保留下界，计入 Fail-Safe、L1 jitter；拒绝跳过元数据读写，校验本次 TTL。短窗口拒绝及足够窗口下标签/Clear 不复活的 L1/L2 用例成立 |
| FIX-002 | CLOSED | 默认等待首次订阅，拒绝异步启动覆盖；包装器记录 Subscribe 结果，管理器检查失败。延迟、失败、配置拒绝有测试；Probe 不再单独强化等待条件 |
| FIX-003 | CLOSED | SampleFactoryLifetime 跟踪实际工厂并在应用关闭时排空；独立 scope 保持到工厂完成。停止等待、scope 释放、外部 Backplane 所有权、真实订阅移除、locker 专属连接关闭有证据 |
| FIX-004 | VERIFIED_BOUNDARY | Redis 故障期间本地写 2 后强制终止原进程；Redis 快照恢复后新进程读旧值 1，再写 3 成功。证明崩溃丢更新边界，不承诺恢复持久化 |
| FIX-005 | OPEN_ACTIONABLE | 真实锁竞争超时、专属 ACL 故障、重抛/降级/Fail-Safe 已验证；释放后换键测试不能证明原锁重获 |
| FIX-006 | CLOSED | 默认值 0/miss、七操作预取消无副作用、同实例租户键/标签隔离有直接测试；Clear 入口补取消检查 |
| FIX-007 | CLOSED | 原生具名 Hit/Miss/Fail-Safe、等待直方图、Activity 及敏感维度有断言；性能补 p99、CPU、WorkingSet 和命中/降级数，恢复单样本不伪造分位数 |
| FIX-008 | CLOSED | 先恢复双空间失效，再清理/预热旧域或换命名空间，完成后切流；持续失效直到新读者/工厂退出 |
| FIX-009 | CLOSED | PUBSUB CHANNELS 和 MessageReceived 直接验证两组身份的 Set/Remove/tag/Clear 通知隔离；释放后仅剩 inspector 频道 |
| FIX-010 | CLOSED | L2 故障断言包装异常及 RedisException；相同 STJ 的 string/Product 不兼容断言序列化包装异常及 JsonException；报告限定实际覆盖范围 |

关闭排空属于应用示例责任，不是新增框架公共排空 API，也不承诺排空上游全部后台 I/O/恢复队列。首次订阅就绪不等于持续连接健康。

## 待修复问题

### FIX-005：原锁释放后重新获取的测试可能假通过

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 分类：验收测试缺口；沿用第一轮 ID，不扩张首版范围。
- 计划依据：FC-04 可选官方分布式锁测试；第一轮 FIX-005 要求记录实际工厂次数、返回/异常、释放和恢复。
- 问题：`LockTimeoutFallsBackToFactory_EvenWithRethrow_AndReleasedLockCanBeAcquiredAgain` 释放 `contended` 后，对另一个键 `after-release` 断言成功。锁按键隔离，新键成功不证明旧锁已释放。仅改成原键也不够：本测试已证明拿锁超时后仍会降级回源，业务返回值正确不等于真正拿到了锁。
- 证据：`framework/tests/Bing.Caching.FusionCache.Tests.Integration/FusionCacheLockerTest.cs:49` 等待原工厂结束，`:50` 移除 `contended`，`:51` 对 `after-release` 断言返回 3，没有检查原 lockName 的真实获取结果；`:44` 同键竞争仍返回 2 是超时降级的直接证据。execution.md 对“释放后恢复”的声明因此过宽。
- 根因：用另一键的业务成功结果替代原锁释放/重获证据，没有区分获取成功与降级回源。
- 影响：原锁即使未及时释放，此测试仍可能通过。未证实生产代码存在锁泄漏；已有锁故障策略证据仍有效。
- 修复目标：原工厂完成后，直接证明相同 lockName 在有界时间内重新获取成功，排除自然过期或超时降级。
- 修复要求：保留真实官方 Redis locker，可用透明记录包装器转发官方调用并记录 lockName 和非空获取句柄，或直接获取同一 lockName。若经缓存门面则排除缓存命中。断言拿到有效句柄并释放；保留超时、ACL、Fail-Safe 用例，不以纯 Mock 替代真实 Redis。
- 验收标准：原键锁在工厂期间确认持有；工厂结束后，在锁自然过期之前，真实 Redis 同一 lockName 的新获取返回有效句柄；测试能区分真实获取与超时降级。更新 execution.md 和符号到测试映射，使声明与证据一致。
- 验证方式：net8/net10 运行受影响锁测试及必要宿主验证。无生产 API、依赖图或性能路径变化，不要求重跑旧缓存全量、打包消费或性能矩阵。

## 审查与测试证据

基准 HEAD：`3919c1509907d661cfef3679dad79368c8b9cb39`。审查包括当前未提交差异和 FusionCache 未跟踪目录，不只看 Git diff。其他任务既有修改保持不动。

已对照完整 plan、execution、原 review、源码调用链、测试、样例、迁移文档、项目依赖和 CI 清单。主代理负责核心行为及结论；luna_worker 独立只读核对测试、TRX、性能 JSON 和包消费证据。固定版本行为结合本地上游 2.9.0 源码核对，Redis 同步订阅/异步退订另核对固定 tag 官方源码。

本轮 Change Impact：仅修改审查报告和历史存档；不改生产/测试、公共契约、plan.md 或 execution.md。选择 L1 精准测试，未扩大到全解决方案或重建性能基线。

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| 本轮 Release/net8 精准测试 | 14 通过、0 失败、0 跳过 | `output/fusioncache-validation/review-round2/review-round2-targeted-net8.trx`；Startup、Retention、Shutdown、ContractBoundary、Diagnostics |
| 复用 Unit net8/net10 | 各 57 通过 | `output/fusioncache-validation/review-fix-1/unit-net8.0.trx`、`unit-net10.0.trx` |
| 复用最终具名指标 net8/net10 | 各 1 通过 | 同目录 `diagnostics-final-net8.0.trx`、`diagnostics-final-net10.0.trx` |
| 复用 Redis net8/net10 | 各 15 通过 | 同目录 `redis-net8.0.trx`、`redis-net10.0.trx`；通过不能补足 FIX-005 缺失断言 |
| 复用性能 net8/net10 | 各 5 类，字段完整 | 同目录 `performance-net8.json`、`performance-net10.json`；对照 performance.md |
| 复用 NuGet net8/net10 | 构建及运行 PASS | `output/fusioncache-package-validation/7.0.0-fusioncache.20261024.4/result.md`、verification.log、package-report.json；包哈希与 feed 相符，核心两项直接依赖 |
| 旧缓存未变化范围 | net6/net8 各 204 通过；EasyCaching 133 通过、70 门控跳过 | 复用 execution.md 记录，不将跳过记为通过 |
| 格式与报告 | PASS | 任务范围 git diff --check；报告 UTF-8、前三行机器协议及历史存档检查 |

复用标识：REUSED_UNCHANGED_SCOPE，依据相关行为、测试路径和证据核对，不宣称本轮重新执行。当前中文注释变化不改变行为证据；包消费结论绑定 `.4` 产物，不冒充重新打包了当前整个工作区。

本轮精准命令：

```powershell
dotnet test framework/tests/Bing.Caching.FusionCache.Tests/Bing.Caching.FusionCache.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~FusionCacheStartupTest|FullyQualifiedName~FusionCacheRetentionTest|FullyQualifiedName~FusionCacheShutdownTest|FullyQualifiedName~FusionCacheContractBoundaryTest|FullyQualifiedName~FusionCacheDiagnosticsTest" --verbosity quiet --logger "trx;LogFileName=review-round2-targeted-net8.trx" --results-directory output/fusioncache-validation/review-round2
```

构建有既有旧 EasyCaching 测试依赖链的 System.Drawing.Common 5.0.0 / NU1904 提示，此前已有记录，不是新适配包引入。本轮未创建 Redis 容器或外部服务资源。

## 证据适用边界

- 崩溃用例直接证明本地未完成分布式更新可因终止丢失，以及新进程读写恢复；没有直接观测恢复队列计数。“待恢复”来自配置和失败路径推断，不冒充队列内部状态已测量。
- 轻量测量：64 同键请求回源 1 次；64 不同键请求回源 64 次，活跃/排队峰值 8/56；恢复样本 Fail-Safe 为 1。CPU 是线程累计时间，可超过墙钟；WorkingSet 峰值是 10ms 采样，不是严格最大值。
- 原生 trace 可能带 key/tag，文档要求导出前删除/脱敏；自身指标断言不代表自动清洗第三方所有遥测。
- Backplane 最终一致、恢复队列进程内且有界、迟到工厂无 fencing，均为已记录边界，不重新计作功能欠缺。

## Round 2 汇总

- Task：20261024-fusioncache-integration；Round：第二轮 Review，第一轮 Fix 后。
- Implementation：第一轮生产行为约束 FIX-001/002 均闭环；综合问题 9/10 闭环，1 项测试验收部分完成，不换算主观功能完成比例。
- Release Gates：本轮无完整发布门禁分母；包消费通过，外部 CI/生产容量未验证，发布未评估。
- Changed Scope：review.md、review-round1.md；另有忽略目录内测试输出。
- Validation Run：L0 diff/编码/协议；L1 精准 14 项通过；L2/L3 复用；L4/L5 不重跑。
- Completed：FIX-001、002、003、004、006、007、008、009、010。
- Open Actionable：FIX-005，同一锁释放后真实重获的断言。
- Blocked Approval：无。
- Blocked External：远程 CI、生产容量、旧 EasyCaching 门控外部场景；不据此触发代码修复。
- Not Applicable：SQL/SQLite 门槛、新 API 历史同构性能基线。
- Accepted Limitations：最终一致、进程内恢复、非严格一次/业务互斥、忽略取消的工厂不可强制终止。
- Verified Boundaries：准入/排队峰值、崩溃丢更新、锁超时和 ACL 故障策略；锁释放重获仍待验证。
- Deferred：完整 ICache 适配、原子 TrySet、前缀扫描/批量、持久 outbox、Admin/JWT/权限迁移等原计划排除项。
- No-Progress Check：CHANGED，从 10 项收敛至 1 项明确测试缺口。
- Next Action：本轮 Review STOP；后续独立 Fixer 仅补 FIX-005，再精准复核。
- Goal Status：本轮未启动 Goal 或自动 Review→Fix；审查交付完成，整体验收仍为 NEEDS_FIX。
