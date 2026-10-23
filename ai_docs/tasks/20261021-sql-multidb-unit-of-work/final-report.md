# SQL 多数据库业务工作单元交付报告

日期：2026-10-21。实施由 GPT-6-Luna/max 完成，GPT-6.1-Sol/high 独立验收 **PASS**。经过 2 轮修复、3 轮审查，9 项 Finding 全部 CLOSED，OPEN_ACTIONABLE=0；计划 T01–T08 验收 8/8。无需继续自动修复。

## 已交付能力

- 工厂保持 Scoped，ISqlQuery/ISqlExecutor 保持 Transient。应用层与仓储层可以各自注入或创建 Query；同一个业务工作单元内，匹配的冻结数据源共用连接和本地事务，各 Query 保持独立查询状态。
- 新增 IUnitOfWorkScopeManager 和业务 scope，统一负责资源提交、回滚、释放；支持嵌套参与、requiresNew、失败后 rollback-only、同步与异步执行。Query 首次执行决定其工作单元归属，不能在后续业务操作间迁移。
- 多数据源分别管理资源；查询、写入、分页、reader/stream、Multiple 和默认批量写入均覆盖参与式执行。批量操作不会在业务 root 内另行提前提交事务。
- EF Core 通过 AddEfCoreSqlUnitOfWork<TContext>(dataSourceKey) 显式映射，与 Dapper 共用相应 EF 连接和事务。SaveChanges 负责写入，root Complete 负责物理提交；Complete 不隐式保存尚未 SaveChanges 的跟踪实体。
- 后台 worker 每次业务操作创建 DI scope，Hangfire job 使用作业 activator 的 scope；scope 内的业务类可构造函数注入工厂或 Query。Singleton worker 通过 IServiceScopeFactory 解析业务类。

## 验证范围

| 证据 | 框架与配置 | 结果 |
| --- | --- | --- |
| 修复第 2 轮 Core 完整套件 | net6/net8，Debug | 各 179 passed |
| 修复第 2 轮 SQLite 本地完整集成 | net6/net8，Debug | 各 229 passed、1 个既有门控 skip、0 failed |
| 修复第 2 轮 EF Core 完整套件 | net6，Release | 165 passed |
| Sol 新构建的精准回归 | SQLite net8 / EF net6，Release | 35 / 6 passed，无 skip |
| Sol 独立边界诊断 | 原异常、租约冲突、非事务 root 的批量事务拒绝 | 6/6，直接退出码 0 |

审查明确接受 T07 配置差异为 DEVIATED_OK：没有将 Debug 全量结果冒充 Release。未受影响的 Provider、FreeSQL、AspectCore、Admin 消费者测试及生产/消费者构建，按变更影响分析复用前轮证据；本轮没有重新执行全仓矩阵。详细命令、配置、TRX 和追溯见 [执行报告](execution.md)、[独立审查](review.md) 与 [符号到测试映射](traceability.md)。

有界性能与资源证据复用 [Review 2 直接确认结果](artifacts/review-round-2/probe-confirmation-001/watchdog-result.json)：net8 Release，16/16 cells，耗时 1,200.7183 ms，60 秒 watchdog 未超时，host/child 直接退出码均为 0；1,554 次连接打开/关闭/释放、314 次物理事务开始/提交/释放，结束 open=0、peak=2。该记录验证有限场景下的执行与资源清理，不代表性能提升或正式性能预算通过。

## 已确认边界与剩余门禁

- 多库采用独立本地事务、顺序提交，不提供跨数据库原子性；后续库提交失败不能撤销先前已提交库。
- 同一连接资源顺序使用；并行业务使用各自 DI scope 与业务 root。频繁创建和释放 Query 不会提前关闭 root 拥有的共享连接，但不得跨业务 scope 缓存 Query 或工厂。
- FreeSQL 的默认 SQL 查询路径可参与；其原生 CRUD、CAP 自有事务动作、未映射 EF Shared、Independent 和显式 SQL 事务仍保留各自所有权。
- 外部 MySQL/PostgreSQL/SQLServer/Oracle/Doris/CAP/Redis 服务未验证，保持 BLOCKED_EXTERNAL；正式性能预算为 BLOCKED_APPROVAL。因此实施/测试/独立审查 PASS，整体 RELEASE_STATUS 仍为 BLOCKED。这些状态没有新增可执行代码缺陷，不启动下一轮 Fix。

## 交付文件

- [计划与设计依据](plan.md)
- [执行报告](execution.md)
- [独立审查报告](review.md)
- [注册、跨层使用、后台作业和迁移说明](migration.md)
- [生产符号与测试映射](traceability.md)
- [当前候选与证据索引](artifacts/candidate-manifest.md)

保留 F008、历史审查和原始失败证据，以及原有未提交修改。没有暂存、提交、推送、创建 PR 或调整版本及已批准 API baseline。独立验收之后的收口仅同步任务文档状态、纠正测试配置标注并补全迁移示例的 orderId 参数；没有再改生产和测试代码。
