# 最终生产符号到测试映射

原始实施候选身份见 [candidate-identity.txt](artifacts/candidate-identity.txt)；Review Fix Round 1 的通过结果和本轮源码哈希清单见 [source-files-sha256.txt](artifacts/review-fix-round-1/source-files-sha256.txt)。测试项目为 `samples/Bing.Samples.SqlUnitOfWork.Tests`；Round 1 的 4 个理论方法在 `net6.0` 与 `net8.0` Release 各执行 8 个用例，全部通过且无跳过。原始 6 用例结果仅作为历史记录。

| 最终生产符号 / 行为 | 直接验证方法 | 核心断言 |
| --- | --- | --- |
| `Program.CreateHostBuilder` / `Startup.ConfigureServices` | `HttpHost_ShouldVerifyTransactionsFactoryLifetimeAndExactRows` | Dapper 与 EfBridge 两种宿主均开启 ValidateScopes / ValidateOnBuild 并成功启动 |
| 默认 `orders` 数据源、应用层与 `OrdersRepository` 的注入 Query | `HttpHost_ShouldVerifyTransactionsFactoryLifetimeAndExactRows`；`AssertCrossLayerBuilderState`；`AssertCrossLayerBuilderRows` | 真实跨层 HTTP 中交错修改应用层与仓储的不同 builder；`AssertQueryStates` 对固定完整 SQL 常量作 ordinal 字符串相等、比较预期参数字典；`AssertCrossLayerBuilderRows` 比较两层单行完整值；提交前双方仍各读到同事务两行，提交/回滚后 `AssertRowsAsync` 与独立连接比较完整行集 |
| `ReportingRepository.InsertAsync` 具名 Query/Executor 工厂 | `HttpHost_ShouldVerifyTransactionsFactoryLifetimeAndExactRows` | orders.db 与 reporting.db 各自提交或回滚；逐行比较两个文件的准确结果 |
| `SqlUnitOfWorkApplicationService` rollback-only 与 `CaughtSqlException` | `HttpHost_ShouldVerifyTransactionsFactoryLifetimeAndExactRows`、`BackgroundWorker_ShouldReportFailuresAndUseFreshScopes`、`HangfireServer_ShouldRunRealJobsReportFailuresAndUseFreshScopes` | SQL 异常已捕获但 root 仍拒绝提交；HTTP/Worker/Job 失败可见；独立连接确认预写入已回滚 |
| 工厂 Query 释放生命周期 | `HttpHost_ShouldVerifyTransactionsFactoryLifetimeAndExactRows` | 同一个 root 内创建、执行、释放 100 个工厂 Query 后，注入 Query 仍能访问 root 并提交 |
| `SampleUnitOfWorkDbContext` EF Shared 映射与 `SaveChanges` 边界 | `HttpHost_ShouldVerifyTransactionsFactoryLifetimeAndExactRows` | EF SaveChanges + Dapper 可共同提交/回滚；跳过 SaveChanges 时 Complete 不会写入 EF 跟踪实体 |
| Dapper 模式的 EF bridge 配置保护 | `HttpHost_ShouldVerifyTransactionsFactoryLifetimeAndExactRows` | Dapper 模式调用 EF 专属场景会返回明确错误且两个数据库行集不变 |
| HTTP 只读 scope 与 scoped 资源隔离 | `HttpHost_ShouldKeepConcurrentReadOnlyScopesIndependent`；`AssertDistinctScopeResources`；`AssertScopeResourcesDisposed`；`AssertReadOnlyQueryRows` | 可控屏障让两个真实 HTTP 请求同时活动；直接断言 `metrics.MaxActualParallelism >= 2`，scope/manager/Query/Context/连接引用各异；首个请求结束时检查第二个 scope 未释放且连接仍 Open，随后第二个在首个结束后建 Query 再执行；每阶段 `AssertRowsAsync` 比较两库完整行集 |
| `ScenarioWorkQueue` / `ScenarioBackgroundWorker` | `BackgroundWorker_ShouldReportFailuresAndUseFreshScopes`；`AssertDistinctScopeResources`；`AssertScopeResourcesDisposed`；`AssertReadOnlyQueryRows` | 实际 HTTP 入队，Channel 单消费者依次处理；每个操作的 root 对应独立 manager/Query/Context/连接；断言 scope 与资源销毁、每次只读前后逐行结果、各操作后的 `AssertRowsAsync` 完整行集；完成/回滚/失败状态及 worker 退出可见 |
| `JobsController` / `SqlUnitOfWorkJob` / Hangfire DI scope | `HangfireServer_ShouldRunRealJobsReportFailuresAndUseFreshScopes`；`AssertDistinctScopeResources`；`AssertScopeResourcesDisposed`；`AssertReadOnlyQueryRows` | 实际 MemoryStorage 入队由单 worker Hangfire Server 执行；两次顺序操作各自关联不同 root 和 scoped manager/Query/Context/连接；断言资源销毁、每次只读前后逐行结果、各操作后的 `AssertRowsAsync` 完整行集；提交/回滚/失败可查询 |
| `ScenarioOperationStore` / `OperationsController` | 两个后台测试方法 | queued、completed、failed 及错误类型/信息由 GET 状态端点返回 |
| `DatabaseInitializerHostedService` / `SampleDatabaseFiles` | 三个测试方法及其 `RunningSampleHost.DisposeAsync` | 初始化两文件；StopAsync 后 worker 退出且端口关闭；Host Dispose 后目录不存在、文件可删除 |
