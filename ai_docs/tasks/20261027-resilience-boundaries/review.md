<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: 20261027-resilience-boundaries
AI_REVIEWED_AT: 2026-10-27T09:13:39Z

# Round 1 独立验收

## 范围

本次只验收 Round 1 的 FIX-BND-R01～R05，依据完整计划、Round 1 执行记录、原始五项 Finding、当前源码与测试、工作区状态及现有测试/性能/包消费证据。旧 Finding 保存在 [review-before-fix.md](evidence/fix-round1/review-before-fix.md)。这不是对 BND-01～06、生产部署或完整管理页面的重新认证。

结论：**R01～R05 全部 CLOSED，OPEN_ACTIONABLE=0。** 本机实现与已记录验证通过；`PASS_WITH_ISSUES` 保留本次性能采样和终态保留策略的适用边界。无须进入下一轮修复。

## Findings

| Finding | 状态 | 独立核对 |
|---|---|---|
| FIX-BND-R01 | CLOSED | [WeightedRoundRobinServiceEndpointSelector](../../../framework/src/Bing.Resilience.Discovery/WeightedRoundRobinServiceEndpointSelector.cs) 实现按服务隔离的平滑加权选择，并限制服务/端点容量、回收空闲状态；权重或成员变化会重置轮转。HTTP 路由在选址前过滤契约、组别、凭据 audience 和撤销端点，之后固定每次调用的候选快照；Ordered 可显式保留。每框架 12 项覆盖 3:1、等权、并发、状态回收、候选快照和 HttpClient 首发比例。首发测试使用真实 HttpClient 管线及记录型 handler，并未声称是网络分布测试。 |
| FIX-BND-R02 | CLOSED | [Schema 校验](../../../framework/src/Bing.Resilience.Operations.Relational/RelationalOperationStore.Schema.cs) 要求 MySQL 认领索引名为 `{prefix}_due` 且列序为 `kind,next_at,id`，与 [ClaimDueAsync](../../../framework/src/Bing.Resilience.Operations.Relational/RelationalOperationStore.cs) 的 `FORCE INDEX` 一致。每框架 4 项 MySQL 测试覆盖正确索引并成功认领、改名、缺失和列序错误；最终扩展回归无跳过。 |
| FIX-BND-R03 | CLOSED | [RecordLifecycle](../../../framework/src/Bing.Resilience/ResilienceDiagnostics.cs) 隔离 Meter/Activity 监听异常；调用点覆盖 Generation 切换/排空、发现刷新/stale、分布式准入及 Replay/Saga 已提交状态。标签仅用固定事件和结果类别。每框架 5 项职责测试核对成功/失败/取消、状态与监听器异常。Saga 尝试路径记录使用父操作 `running.Id`，见 [SagaWorker](../../../framework/src/Bing.Resilience.Compensation/SagaWorker.cs)。 |
| FIX-BND-R04 | CLOSED | [保留策略](../../../framework/src/Bing.Resilience.Operations/OperationRetention.cs) 默认由 Replay/Saga 的 `Retention = null` 关闭；关系型 Store 在事务内仅处理明确终态、保护期结束且无有效租约的记录，并限制操作和历史批量。幂等身份与状态保留，载荷清理用 `retained:v1:` 标记防止重新执行；Saga 父子载荷同事务清理，未知/在途状态受保护。每框架最终回归含 11 项保留测试，覆盖 SQLite/MySQL、并发认领、回滚、批次及去重身份。`Saga ManualIntervention` 父记录由终态白名单排除；[test-map.md](test-map.md) 诚实说明该父状态没有独立断言。回滚到不识别 `retained:v1:` 的旧版本受限，已在执行记录和测试映射说明，属已披露兼容边界。 |
| FIX-BND-R05 | CLOSED | [正式性能报告](evidence/fix-round1/performance-results.md) 与 [独立口径复核](evidence/fix-round1/review-performance.md) 支持本机固定负载结论：120 轮分为 HTTP 48、Redis 24、Journal 48；HTTP 使用真实 loopback，Redis 健康/延迟使用真实客户端和双向代理字节，Journal 覆盖积压、重复投递、恢复确认和终态清理。报告明确 Redis 只在预热前注入一次 RST、正式窗口新增连接/RST 为 0；恢复是到期租约由新宿主确认，不是本轮杀进程；Journal 延迟不含取任务前等待，`maxQueuedRequests` 是初始积压估值。没有将这些数值表述为驱动内部队列或生产 SLA。 |

## 验证与证据

- [回归汇总](evidence/fix-round1/regression-summary.json) 的六项目、双 TFM 共 869/869 通过，0 失败、0 跳过。两份最终扩展 TRX 各 207/207；各自可核对出 WeightedDiscovery 12、MySqlSchemaIndex 4、LifecycleDiagnostics 5、OperationRetention 11 项。早期定向确认 TRX 是窄子集（其中保留清理 8 项），不能与最终完整回归计数相混。
- [独立包消费结果](evidence/fix-round1/package-consumption/results.md) 的最终 run `2026-10-27T09-02-20-045Z` 记录 34 包、53 命令、6 次运行和 6 次发布通过，漏洞查询为 0；消费者来自 nupkg 且没有 ProjectReference。
- [静态检查清单](evidence/fix-round1/static-check.json) 记录 40 个相关文件 UTF-8/空白检查通过。无效 Redis 探针写入的六个随机键有精确比对与清理记录；任务 MySQL/Redis 清理见 [cleanup.json](evidence/fix-round1/cleanup.json)。早期失败证据保留且没有计入正式结果。
- Resilience 源码和测试目录在当前工作区显示为未跟踪，因而不能由普通 `git diff` 完整呈现；已直接检查实际文件、职责测试及最终 TRX。工作区的其他任务修改不纳入本次结论。
- 本次未运行 `dotnet`、测试或性能探针；复用当前代码范围对应的最新已保存证据，符合任务要求。

## 状态

- IMPLEMENTATION_STATUS: PASS（仅 R01～R05 修复范围）
- TEST_STATUS: PASS（复用最终 869/869 双 TFM 证据）
- PERFORMANCE_EVIDENCE_STATUS: PASS_WITH_LIMITATIONS（仅本机固定负载）
- PACKAGE_CONSUMPTION_STATUS: PASS（34 包最终 run）
- OPEN_ACTIONABLE: 0
- ACCEPTED_LIMITATION: `retained:v1:` 数据不能由不识别该标记的旧版本安全回滚恢复；性能指标不代表生产阈值。
- BLOCKED_APPROVAL: 生产 SLA/性能/容量阈值尚未批准。
- BLOCKED_EXTERNAL: 目标部署认证及完整管理页面变更操作未在本轮重新验收。
- RELEASE_STATUS: BLOCKED（以上门禁不构成本地实现 Finding）。
- NEXT_ACTION: STOP；不自动启动 Fix。
- GOAL_STATUS: STOPPED_BLOCKED（实现 Finding 已关闭，Release 门禁仍在外部/审批范围）。
