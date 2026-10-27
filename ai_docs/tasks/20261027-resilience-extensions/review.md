<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: 20261027-resilience-extensions
AI_REVIEWED_AT: 2026-10-27T05:40:12.0000000Z

# 独立复审

本轮按 `plan.md`、`execution.md`、当前源码、测试和消费证据复审 EX-01～EX-11。复审重点是实际调用链、未知结果、租约/代际、端点身份、响应所有权和公共 API 边界；没有修改生产代码、测试、计划或执行报告，仅新增本报告及 `evidence/review/` 下的复审证据。

## 当前状态

| 维度 | 状态 | 依据 |
|---|---|---|
| IMPLEMENTATION_STATUS | PASS | FIX-001 已由 Lua 辅助键租约修复并完成真实 Redis 回归 |
| TEST_STATUS | PASS | 最新双框架完整回归 723/723；Redis 精准回归 net6/net8 各 6/6 |
| PERFORMANCE_EVIDENCE_STATUS | PARTIAL | 双框架资源探针和 Provider 测量通过；本任务未设生产性能阈值，Redis 租约回收按资源证据验证 |
| RESOURCE_EVIDENCE_STATUS | PASS | 本地 timer、代、动态分区和 Redis 辅助键均有界回收证据 |
| EXTERNAL_GATE_STATUS | PASS | 任务范围内的 Redis/MySQL/多进程和包消费证据已提供；不宣称生产 SLA |
| RELEASE_STATUS | PASS | 无未关闭 MUST_FIX/SHOULD_FIX；生产 SLA 仍按计划不作宣称 |
| GOAL_STATUS | COMPLETED | `OPEN_ACTIONABLE=0`，按规则停止自动修复循环 |

## Change Impact Analysis

```text
ChangedFiles: 任务新增容错核心、Provider、HTTP、Discovery、Distributed/Redis、Operations/Replay/Saga 及样例文档；本复审只新增 review.md
ChangedProjects: 12 个容错包、两个 HTTP 客户端适配及扩展测试项目
ChangedPublicContracts: 中性 Provider/管线、发现、分布式熔断、持久操作、Saga、SyntheticResponse、HTTP 重放接口
ChangedRuntimePaths: Provider 执行、代际切换、动态端点、Redis 准入/上报、持久 Worker、HTTP 发送和定义下载
ChangedProviders: BuiltIn、Fences、Polly
ChangedTFMs: net6.0、net8.0
ChangedBuildPackaging: 可选包和无 ProjectReference 的三 Provider 消费验证
ChangedBenchmarkHarness: 资源探针和 Provider/HTTP 测量证据
ChangedDocsOnly: 样例、迁移、test-map、execution 和本报告
AffectedDependents: 两个动态 API 客户端、Bing.Http.Resilience、ASP.NET Core 幂等接入、Redis/MySQL/SQLite 宿主
RiskLevel: HIGH
```

## 阶段验收

| 阶段 | 结论 | 复审依据 |
|---|---|---|
| EX-01 契约与依赖 | PASS | Abstractions/Core 不引用 Polly/Fences；三 Provider 包边界和独立消费 assets/nupkg/deps 检查通过 |
| EX-02 BuiltIn | PASS | BuiltIn 八策略真实执行路径；三 Provider 共用契约双 TFM 各 88/88 |
| EX-03 热切换 | PASS | 稳定句柄、Generation/CAS、租约排空、跨代配额和资源探针通过；未发现混代或提前释放路径 |
| EX-04 动态发现 | PASS | 配置/HTTP 来源、撤销墓碑期限、快照租约、动态分区限制与退休回收均有回归；最近撤销边界修复已复核 |
| EX-05 分布式熔断 | PASS | Redis Lua epoch/探测/去重/失联行为及未报告准入辅助键 TTL 均通过真实 Redis 回归 |
| EX-06 写重放与幂等 | PASS | 请求快照、逐次请求/签名、服务端同事务幂等、Unknown 确认和损坏结果配置异常均有证据 |
| EX-07 持久重放 | PASS | SQLite/MySQL、租约/CAS、真 kill/restart、损坏文档转人工状态和 attempt ledger 通过 |
| EX-08 Saga 补偿 | PASS | Forward/Confirm/Compensate、逆序/DAG、处理器版本、人工 CAS、事务接入和真 kill/restart 通过 |
| EX-09 SyntheticResponse | PASS | 缓存 200/持久 202 校验、来源观察、取消优先、失败响应释放和远端失败统计边界通过 |
| EX-10 描述竞速 | PASS | 完整读取/解析/校验获胜、分支隔离、Retry-After、缓存失败驱逐和等待者取消隔离通过 |
| EX-11 集成交付 | PASS | 723/723、样例、双框架包消费、资源探针和独立复审均通过 |

## 验证证据

- 最新最终回归为 `evidence/final-tests/2026-10-27T05-37-50-629Z/summary.json`：12 个项目目标，723/723 通过，0 失败，0 未执行；此前 `latest.json` 的 719/719 证据由本轮新鲜结果覆盖。
- 本轮直接受影响目标测试命令为 `dotnet test framework/tests/Bing.Resilience.Extensions.Tests/Bing.Resilience.Extensions.Tests.csproj --filter "FullyQualifiedName~DiscoveryTests|FullyQualifiedName~DurableReplayTests.Corrupt|FullyQualifiedName~SagaTests.Corrupt|FullyQualifiedName~SyntheticResponseTests.ObserverFailure"`，net6.0 和 net8.0 各 8/8 通过；TRX 位于 `evidence/review-targeted/`。
- Redis 真实测试、三进程、暂停/恢复、MySQL/SQLite、Durable/Saga 真 kill/restart、三 Provider 双 TFM 包消费和资源探针均已检查执行报告及对应 evidence；这些证据未被本复审变更影响。
- FIX-001 直接回归：`evidence/review/redis-fix/net6/redis-fix-net6.trx` 与 `evidence/review/redis-fix/net8/redis-fix-net8.trx`，`RedisCircuitTests` 各 6/6 通过，包含 `AbandonedAdmissionAuxiliaryKeysExpireWithoutAReport`。
- `git diff --check` 对任务证据和 Redis 脚本无输出。工作区的 Redis 脚本为新增未跟踪生产资源，故复审直接读取源码而不以 Git diff 作为唯一范围。

## Findings

### FIX-001：Redis 未报告准入的临时记录没有自动过期（已关闭）

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 状态：CLOSED
- 问题：原实现只在 `report` 路径设置辅助键 TTL，准入后进程终止且无后续访问时可能滞留。
- 修复证据：`framework/src/Bing.Resilience.Distributed.Redis/CircuitState.lua:51-58` 新增 `touchAuxiliary()`；`admit` 写入后在 `:87-91` 立即为 `admitted`、`expiry` 等辅助结构设置 `keep` TTL；`report` 在 `:111-114` 继续刷新同一租约。稳定状态键 `k` 不设置 TTL，避免隔离状态被 Redis 自动恢复。
- 影响结论：未报告 attempt 的租约不再依赖下一次相同 key 访问；主状态仍由显式控制和 epoch 保护，未引入静默恢复。
- 验收证据：真实 Redis 的 `AbandonedAdmissionAuxiliaryKeysExpireWithoutAReport` 在 net6/net8 各通过，完整 `RedisCircuitTests` 各 6/6；最新双框架完整回归 723/723。现有 stale epoch、半开探测和手动隔离回归继续通过。
- 关闭依据：辅助键自动回收、后续状态安全和跨框架验证均已形成证据闭环，无需继续修复。

## 其他边界

- 已确认 `ResilienceAttemptObservations` 对观察器完成/释放异常隔离；调用链没有把 Observer 异常直通重试或覆盖业务结果。
- 已确认目录撤销接受历史身份并按 `MaxStale` 清理，已完成幂等结果损坏统一转换为 `ResilienceConfigurationException`；这些已修复问题不重新打开。
- Provider 公共 API 未泄漏 Polly/Fences 类型；Core、BuiltIn、Discovery、Operations 等免费组合的依赖图没有重新引入具体引擎。
- 非协作用户回调可能延长清理时间属于计划明确的协作式取消边界；当前证据没有把它误报为新增缺陷。

## TODO 分类

- Completed：EX-01～EX-11 的生产路径和对应职责/集成证据；三 Provider 双 TFM；723/723 回归；FIX-001 Redis 未报告准入记录自动回收；独立包消费；资源探针；样例和迁移材料。
- Open Actionable：无。
- Blocked Approval：无。
- Blocked External：无；生产 SLA/生产部署不属于本地验收承诺。
- Not Applicable：不执行生产数据库迁移、全局依赖升级、Git 提交/推送或 NuGet 发布。
- Accepted Limitations：首批不实现全部注册中心适配器、管理后台和具体支付/库存补偿处理器；这些由业务宿主提供。
- Verified Boundaries：Redis 故障转移不宣称网络分区下绝对线性一致；协作式取消不强杀忽略令牌的回调；无批准生产性能阈值时只报告测量值。
- Deferred：无。

## 收口

当前 `OPEN_ACTIONABLE=0`，`Next Action` 为 `STOP`。FIX-001 已关闭，独立复审状态为 PASS；按 GOAL_RULES 不再启动新的自动修复或重复无关实验。
