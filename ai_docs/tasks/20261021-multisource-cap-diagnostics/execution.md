<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261021-multisource-cap-diagnostics
AI_EXECUTION_FINISHED_AT: 2026-10-22T00:32:07Z

AI_EXECUTION_STARTED_AT: 2026-10-21T13:44:55.973Z

# 实施执行报告

## 结论

- T01–T06 全部完成（6/6）。T07 是用户明确交由父线程安排的 Sol/high 独立验收，不属于本执行代理的实施项。
- 完成多数据源 Uow 资源契约、CAP 8.0.1 事务借用运行时、Sample 三种运行入口、独立跨进程消费者、只读诊断 UI、文档与验证证据。
- OPEN_ACTIONABLE：0。真实 MySQL/RabbitMQ 及浏览器验证已执行；SQLite 证据探针有 2 个按测试自身环境门控跳过，详见验证矩阵。
- 本任务专属 Compose 项目 `bing-multisource-cap-diagnostics` 的 MySQL/RabbitMQ 容器保持运行，供复核保留数据；其他容器未触碰。测试用发布与消费者进程已停止。
- 默认 SQLite Sample 当前运行于 `http://127.0.0.1:5088`；诊断页面和 API 均实测 HTTP 200，SQLite 模式下 CAP 来源明确显示 unknown。
- 未暂存、提交、推送或创建 PR；未修改 `plan.md`、旧 review 或合并审计报告。任务开始前已有的 F008/Uow/Sample 工作树变更均保留。

## T01–T06 状态

| Task | 状态 | 交付与证据 |
| --- | --- | --- |
| T01 CAP 协议和所有权 | 完成 | 核验 CAP MySQL 8.0.1 源码和 `CapOrderedEventTransactionFactory`；记录借用连接/事务约束、真实失败回归与唯一资源 owner，见 `resource-ownership.md`。 |
| T02 Uow 资源契约 | 完成 | 新增 root/resource/connection provider/accessor；Dapper、EF 与 publisher 共用当前 root 的真实连接和事务；补充首次访问顺序、嵌套、隔离、取消、释放及 SQLite 测试。 |
| T03 多源 CAP runtime | 完成 | 每个数据源独立 CAP 服务容器、存储和缓冲。CAP 只借用当前物理事务写 Outbox；root 独占物理提交、回滚、释放；资源提交后才 Flush。保留既有 CAP 自管事务保护。 |
| T04 Sample 与消费者 | 完成 | SQLite 默认离线路径保留；新增 MySQL CAP 双源、HTTP/Worker/Hangfire 共用应用服务、独立 consumer host、事务 Inbox/effect 幂等、Compose 与生命周期脚本。 |
| T05 诊断页面 | 完成 | 新增 `/diagnostics/events` 与只读 API，按 operation/Event/Trace/source/status 查询真实业务、CAP Outbox 与 consumer 事实；无证据显示 unknown；无前端框架。 |
| T06 验证及文档 | 完成 | Release 测试矩阵、MySQL/RabbitMQ 跨进程 E2E、真实浏览器检查、README、API delta、resource ownership、traceability 和原始 artifacts 均已落盘。 |

## 设计与 API 语义

- CAP NuGet 8.0.1 所指向的 MySQL provider 源码表明，Storage 从传入的 `DbTransaction.Connection` 写 Outbox；CAP 原生事务 `Commit` 会提交借用事务并 Flush，`Dispose` 会释放借用事务。因此适配层不调用 CAP native transaction 的提交或释放，Uow root 是物理连接与事务的唯一 owner。
- CAP 的每源 runtime 保持数据源隔离。发布只把消息写进当前事务的 CAP Outbox；root 对应资源提交后刷新该 runtime 缓冲，回滚不刷新。多源仍顺序提交，第二源失败时第一源可以已提交，API 不声称跨库原子性。
- `IUnitOfWorkMessagePublisher` 要求活动事务 root 和 `MessageEvent.Send == false`，调用立即写当前未提交事务的 Outbox；返回不表示已提交、投递或消费。`Send == true` 被拒绝。旧 `IMessageEventBus` 的 `Send == false` 继续是 Uow 完成回调中的延迟 action，旧 `Send == true` 继续使用 CAP 自管事务。
- 新 API 为增量公开契约；未更改既有 shipped API baseline、默认单源行为、NuGet 版本或性能预算。`Bing.Events` 仅引用生态已有的 CAP 8.0.1。符号映射见 `api-delta.md` 与 `traceability.md`。
- publisher-only 注册保留 CAP dispatcher 与 `CapProcessingServer`（含 Outbox retry processor），只移除消费者 alias。真实 RabbitMQ 故障恢复曾发现移除 server 会让 Outbox 停在 Failed；回归由 `CapHostRoleRegistrationTest` 覆盖并已修复。
- Sample 内存消息处理器按真实 scope 注册和解析；没有使用既有 ServiceLocator 根解析跨 scope 复用 handler。

## Release 验证

测试以 Release 配置运行，所有 `dotnet test` 进程退出码均为 0。实际命令按矩阵每个 TFM 各执行一次：`dotnet test <project.csproj> --configuration Release --framework <TFM>`。每项命令的 stdout log 和 TRX 均位于相应证据目录。Sample 运行前设置 `RUN_CAP_MYSQL_TESTS=1`，只连接本任务 Compose 数据库，不访问生产数据库。

| 测试项目 / 范围 | TFM | 结果 | 证据 |
| --- | --- | --- | --- |
| `framework/tests/Bing.Uow.Tests/Bing.Uow.Tests.csproj` | net6.0 / net8.0 | 31 + 31 passed | `artifacts/t06-final/framework-uow-net60/`、`framework-uow-net80/` |
| `framework/tests/Bing.Aop.AspectCore.Tests/Bing.Aop.AspectCore.Tests.csproj` | net6.0 / net8.0 | 104 + 104 passed | `artifacts/t06-final/framework-aop-net60/`、`framework-aop-net80/` |
| `framework/tests/Bing.Dapper.Core.Tests/Bing.Dapper.Core.Tests.csproj` | net6.0 / net8.0 | 179 + 179 passed | `artifacts/t06-final/framework-dapper-core-net60/`、`framework-dapper-core-net80/` |
| `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/Bing.Dapper.Sqlite.Tests.Integration.csproj` | net6.0 / net8.0 | 231 + 231 passed；各 1 个环境探针 skipped | `artifacts/t06-final/framework-sqlite-integration-net60/`、`framework-sqlite-integration-net80/` |
| `framework/tests/Bing.EntityFrameworkCore.Tests/Bing.EntityFrameworkCore.Tests.csproj` | net6.0 | 166 passed | `artifacts/t06-final/framework-entity-framework-net60/` |
| `framework/tests/Bing.EventBus.Tests/Bing.EventBus.Tests.csproj` | net6.0 / net8.0 | 378 + 378 passed | `artifacts/t06-final/framework-event-bus-net60/`、`framework-event-bus-net80/` |
| `framework/tests/Bing.Dapper.MySql.Tests/Bing.Dapper.MySql.Tests.csproj` | net6.0 / net8.0 | 180 + 180 passed | `artifacts/t06-final/framework-dapper-mysql-net60/`、`framework-dapper-mysql-net80/` |
| `samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj`，含真实 MySQL CAP 门控测试 | net6.0 / net8.0 | 13 + 13 passed；`RUN_CAP_MYSQL_TESTS=1` | `artifacts/t06-final/sample-net60-final2/`、`sample-net80-final2/` |

合计 15 次 `dotnet test` 运行：2,398 passed、0 failed、2 skipped。两个 skipped 项为 SQLite `ProviderContract_WhenSqliteScenariosRun_ShouldRecordRealIntegrationEvidence` 的受控环境证据探针；其余 SQLite 本地集成测试实际执行。单测没有把环境 skip 计为 PASS。

另执行 3 个独立 Release build，均退出码 0：

| 命令 | 结果 / 证据 |
| --- | --- |
| `dotnet build samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj --configuration Release --framework net6.0` | 0 error；`artifacts/t06-final/sample-app-release-net6.log` |
| `dotnet build samples/Bing.Samples.SqlUnitOfWork.Consumer/Bing.Samples.SqlUnitOfWork.Consumer.csproj --configuration Release --framework net6.0` | 0 error；`artifacts/t06-final/sample-consumer-release-net6.log` |
| `dotnet build framework/src/Bing.Dapper.Core/Bing.Dapper.Core.csproj --configuration Release --no-restore` | 0 warning、0 error；`artifacts/t06-final/dapper-core-build-release.log` |

Sample 测试命令分别为 `dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj --configuration Release --framework net6.0` 与 `net8.0`，运行前均设置 `RUN_CAP_MYSQL_TESTS=1`。所有命令退出码均为 0。TRX 的 `UnitTestResult` outcome 汇总为 2,398 Passed、0 Failed、2 NotExecuted；两个 NotExecuted 均为上述 SQLite 证据探针。

Sample app 和 Consumer 构建日志含既有 `NETSDK1138`（net6.0 生命周期提示）；没有构建失败。SQLite 环境探针的两个 skip 是唯一未执行的验证项，没有外部 MySQL/RabbitMQ 门禁未通过。

## MySQL、RabbitMQ 与浏览器证据

隔离 Compose 使用 `mysql:8.0.16` 和 `rabbitmq:3.13-management`，只映射本机端口 13307、5673、15673，orders/reporting 两个独立数据库和 Rabbit vhost 均为任务专用。启动/停止入口为 `scripts/start-infra.ps1`、`scripts/stop-infra.ps1`；配置在 `infra/compose.yaml`。CAP 测试门控与本地真实 MySQL 连接配置记录在各测试日志/运行环境，未访问生产服务。

真实验证覆盖：

- Dapper-first、EF-first 与 message-first 通过当前 root 的事务写业务行与各自 CAP Outbox；独立 SQL 连接在 root commit 前看不到记录，提交后两个库分别读到业务和 CAP 行；整体 rollback 后两库均无业务/Outbox 行。
- 正常双源提交操作 `56e7edd7ba094ec68e02be55f482ff95`；message-first `4f7970438c974f078424b2373ed37de2`；多源 rollback `4bb3b9f9c82244cb8961ff0ede759c5d`。独立查询证据在 `artifacts/t06-e2e/independent-*` 与 `http-cap-multi-source-*`。
- 第二源故障的部分提交 `9eceed2c0cb2421c9225523570f922c2`：orders 的业务和 Outbox 已提交，reporting 未提交且无相应行；进程重启后仍由持久订单事件恢复诊断。证据 `http-cap-partial-commit-restarted-diagnostics.json`、`independent-post-*`。
- 重复投递 `01050280785c4feaa6559cb8ecb5ae6e` 记录两次接收/Inbox 尝试而副作用只发生一次；消费第一次失败后由独立 consumer 重试成功，事件 `ec86e373d9d0476182871f209baec8be`。证据 `http-cap-duplicate-event.json`、`http-cap-consumer-retry.json` 及 consumer 进程日志。
- RabbitMQ 停止期间提交 `7272df8719a54950b4651f53c3c30ac0`，两库业务与 Outbox 均提交，Outbox 暂为 Failed；保存数据后重启 broker 及发布进程，CAP retry server 将两源 Outbox 恢复至 Succeeded，跨进程 Consumer 各处理一次。证据 `rabbit-down-*`、`rabbit-recovery-outbox-*`、`rabbit-recovered-*`、`rabbit-restart.log`。
- Worker 入口 operation `ef0490b94b464314b26a6d7ac108a25d`；Hangfire job `90dfdbaa37fa46008cda58713f7dac11`，均真实调用共同应用服务。对应 `worker-cap-multi-source*` 与 `hangfire-cap-multi-source*`。
- 在真实浏览器打开诊断页并实际筛选数据源 `reporting`、部分提交 operation ID/status；核对行级 source 状态、Outbox、consumer 计数、更新时间与真实数据库事实，控制台错误查询为空。截图 `artifacts/t06-e2e/cap-diagnostics-browser-final.png`，数据响应 `http-cap-partial-commit-restarted-diagnostics.json`。
- 留给用户试用的本地离线 Sample 由 `dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj --configuration Release --no-build --urls http://127.0.0.1:5088` 启动；`GET /diagnostics/events` 与 `GET /api/diagnostics/events` 均为 HTTP 200，API 明确返回未配置 CAP 时的 unknown 数据源状态。

历史记录若不能由数据库证明，会显示 `unknown`，不把日志当事务事实，也不把单侧证据推断成双库已提交。

## 交付文件与变更边界

- 框架资源、EF/Dapper 适配、CAP 多源 runtime 及职责测试：`framework/src/Bing.Uow/`、`framework/src/Bing.Dapper.Core/`、`framework/src/Bing.EntityFrameworkCore/`、`framework/src/Bing.Events/` 与对应 `framework/tests/`。
- Sample 服务与独立消费者、Host/API/UI/Compose/脚本：`samples/Bing.Samples.SqlUnitOfWork/`、`samples/Bing.Samples.SqlUnitOfWork.Consumer/`、`samples/Bing.Samples.SqlUnitOfWork.Tests/`、`ai_docs/tasks/20261021-multisource-cap-diagnostics/scripts/`、`infra/`。
- API 增量、资源所有权及符号到测试追溯：`api-delta.md`、`resource-ownership.md`、`traceability.md`；操作说明见 Sample `README.md`。
- 不创建性能 baseline：本任务没有批准的吞吐或分配门槛，未以虚构数字代替测量；现存 `SqlUnitOfWorkProbe` 来自原有 SQL Uow 工作，不改变既定预算。
- `git diff --check` 退出码 0。Git 输出包含一条对既有、任务外 `ai_docs/tasks/20261021-merged-code-audit/final-report.md` 的 CRLF/LF 警告；该文件及同目录 backlog 均未由本任务修改或复原。
- 未进行暂存或提交；最终工作树同时保留本任务与起点的 F008/Uow/Sample 修改。最终生产符号到测试方法的逐项映射见 `traceability.md`。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`。
- Fix Scope：`recommended`，包含 FIX-001 MUST_FIX 和 FIX-002–008 SHOULD_FIX。
- Review 文件：`ai_docs/tasks/20261021-multisource-cap-diagnostics/review.md`，本轮未编辑。
- 原始输入备份：`artifacts/fix-round-1/review-input.md`；与 review.md SHA256 相同：`B9FEF670D923481D5D865D483D379066BD0766D6177D1433ED54497B38683391`。
- 本轮沿用已存在的 F008/Uow/Sample 和 Round 1 实现；没有覆盖旧审查证据。新增修复验证索引为 `artifacts/fix-round-1/validation.md`。

#### FIX-001

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：COMPLETED。
- 修改/验证文件：`framework/src/Bing.Events/Cap/CapUnitOfWorkRuntimeManager.cs`、`CapUnitOfWorkMessagePublisher.cs`、`samples/Bing.Samples.SqlUnitOfWork.Tests/CapMySqlIntegrationTests.cs`。
- 根因：运行时只校验注册声明连接，未验证 callback 最终生效的 CAP MySQL Provider / `MySqlOptions.ConnectionString`。
- 修复：startup 校验最终 native storage provider、选项类型、可比较物理库身份；publish 边界继续对齐声明身份和借用 Uow 连接。最终 options 指向 reporting 而声明/借用 orders 时，startup 在消息 SQL 前拒绝。
- 验证：`CapMySqlIntegrationTests.Runtime_WhenFinalNativeMySqlOptionsPointToAnotherDatabase_ShouldRejectBeforeOutboxSql` 在实际任务 MySQL 上通过；orders/reporting 独立 Outbox 查询均无新增行。Round 1 Sample net6/net8 Release TRX 均通过。

#### FIX-002

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改/验证文件：`framework/src/Bing.Events/Cap/CapUnitOfWorkRuntimeManager.cs`、`samples/Bing.Samples.SqlUnitOfWork.Tests/CapMySqlIntegrationTests.cs`。
- 根因：CAP 默认 singleton `ICapPublisher` 捕获 runtime root provider，单纯创建操作 scope 不会生成每操作 publisher。
- 修复：native `ICapPublisher` descriptor 按 scoped 生命周期注册；操作 scope 由 root resource 持有，同 root/嵌套 participant 复用，独立 root 使用另一个 scope，root 完成时释放。
- 验证：`RuntimePublisher_ShouldReuseWithinOperationScopeAndDisposeWithRoot` 断言 native publisher 同 scope 为同一实例、独立 root 不同、captured ServiceProvider 是操作 scope，释放后不能再 resolve；`CapHostRoleRegistrationTest` 继续覆盖原生 dispatcher/retry server。

#### FIX-003

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改/验证文件：`framework/src/Bing.Events/Cap/CapUnitOfWorkMessagePublisher.cs`、`framework/tests/Bing.EventBus.Tests/Cap/CapUnitOfWorkPublisherContractTest.cs`、`samples/Bing.Samples.SqlUnitOfWork.Tests/CapMySqlIntegrationTests.cs`。
- 根因：Dapper resource 已 ready 时 `EnsureReadyAsync` 不会检查取消，CAP 8.0.1 borrowed transaction Store 路径也不消费取消 token。
- 修复：publish 在解析/写入副作用前检查预取消 token，设置 root rollback-only 并抛 `OperationCanceledException`。
- 验证：direct publisher 单测及真实 MySQL `CapMySql` / `CapMySqlEfBridge`、Dapper / EF、ready / not-ready 4 组合通过；各组合无新增业务或 Outbox 行，root 不可成功提交。

#### FIX-004

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改/验证文件：`samples/Bing.Samples.SqlUnitOfWork/Application/SqlUnitOfWorkApplicationService.cs`、`samples/Bing.Samples.SqlUnitOfWork.Tests/CapMySqlIntegrationTests.cs`。
- 根因：message-first helper 原先在真实 publisher 前执行关联 business-event SQL。
- 修复：`CapMessageFirstCommit` 先调用 `IUnitOfWorkMessagePublisher`，后续业务行和关联事件继续复用当前 Uow 物理事务。
- 验证：`MessageFirstScenario_ShouldPublishBeforeAnyBusinessEventAndRetainExactRows` 对 `CapMySql` 与 `CapMySqlEfBridge` 均直接断言 publisher start/return 先于 business-event/order-row SQL，并核对两源 exact business、event、Outbox 行及真实 CAP IDs。Targeted 2/2 和最终 Sample TFM 矩阵通过。

#### FIX-005

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改/验证文件：`samples/Bing.Samples.SqlUnitOfWork/Application/SqlUnitOfWorkApplicationService.cs`、`samples/Bing.Samples.SqlUnitOfWork.Tests/SqlUnitOfWorkHostTests.cs`、`framework/tests/Bing.Uow.Tests/UnitOfWorkScopeTest.cs`。
- 根因：Sample 只有即时 memory bus 场景，没有当前 root `OnCompleted` 的提交后 local bus 场景。
- 修复：Sample 使用当前作用域解析 scoped bus，新增提交后处理和 rollback 场景；未改旧消息入口语义。
- 验证：`OnCompleted_ShouldRunOnceAfterCommitAndNeverAfterRollbackOrCommitFailure` 覆盖提交前不执行、提交后一次、重复 Complete 不重放、rollback/commit failure 不执行；`MemoryEventAfterComplete_ShouldUseCurrentScopeAndSkipRollback` 覆盖 HTTP/Worker/Hangfire 与 handler scope 隔离。

#### FIX-006

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改/验证文件：`samples/Bing.Samples.SqlUnitOfWork/Infrastructure/Diagnostics/CapEventDiagnosticsService.cs`、diagnostics DTO、`wwwroot/diagnostics/events.html`、Sample MySQL tests。
- 根因：CAP 8.0.1 `cap.published` 原生主键是 `Id`；只读 `MessageId` 会丢 ID，且单条 ID 投影不足以展示重复 Outbox、JobId 和 consumer identity。
- 修复：读真实 `Id` 并输出完整 ID 集；投影 JobId、ConsumerName/group、每次 attempt。round-trip 时间格式保留 `DATETIME(6)` 精度。
- 验证：`DiagnosticsProjection_ShouldPreserveEveryNativeMessageAndConsumerAttempt` 核对真实 CAP Id、完整多 ID、错误/时间/消费者身份；真实 Hangfire JobId 和两源 CAP IDs 在浏览器及独立 SQL 可交叉确认。该职责测试先发现 MySQL 时间被截成整秒，修复后 targeted 和最终 Sample 矩阵通过。

#### FIX-007

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改/验证文件：`samples/Bing.Samples.SqlUnitOfWork/Infrastructure/Operations/ScenarioOperationStore.cs`、`CapEventDiagnosticsService.cs`、`ScenarioOperationStoreTests.cs`、诊断 UI。
- 根因：操作内存状态不能在进程重启后定位 rollback/process steps；异常聚合中的逐源 callback outcome 与 attempt 历史没有完整投影。
- 修复：以 JSONL 保存 operation/event/trace/source/steps/JobId/outcome/callback failure；诊断标明 `process-journal`，数据库提交状态仍由真实业务/Outbox 行决定，证据不足显示 `unknown`；所有真实 consumer attempts 按时间线显示。
- 验证：`ProcessEvidence_ShouldSurviveRestartAndProjectCallbackFailuresAndRollbackFilters` 在重启后分别按 operation/event/trace/source 查询，断言两源 `CommittedCallbackFailed` 与数据库 unknown；真实 retry API/浏览器展示 Failed 和 Succeeded 两次 attempt，副作用为 1。消息失败不触发重提或补偿。

#### FIX-008

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改/验证文件：`framework/tests/Bing.EventBus.Tests/Cap/CapUnitOfWorkPublisherContractTest.cs`、`CapBorrowedTransactionContractTest.cs`、Sample `CapMySqlIntegrationTests.cs`、`traceability.md`。
- 根因：既有 CAP publisher 默认实现没有与每个新增职责符号对应的直接永久回归。
- 修复：补齐/保留 unknown source、非法 root、预取消、native scope、ambient、runtime 启停、borrowed buffer/physical owner 等职责测试，并为所有 FIX 映射到测试项目和具体方法。
- 验证：`BorrowedBuffer_ShouldLeavePhysicalOwnershipToRootAndFlushOnlyAfterCommit` 覆盖 commit/rollback/dispose 唯一 owner、rollback 不 flush、flush failure 不重提；runtime integration test 覆盖 scope/ambient/lifecycle。

### Round 1 汇总

- MUST_FIX：1/1 COMPLETED；SHOULD_FIX：7/7 COMPLETED；PARTIAL：0；BLOCKED：0；FAILED：0。
- 最终新 Release 矩阵：6 次 `dotnet test`，882 passed、0 failed、0 skipped；另有 3 项 targeted 专项通过（其中均属于上述最终 Sample TFM 行为覆盖）；2 项独立 Release build 均退出 0。
- 实际 MySQL/RabbitMQ Compose 健康；浏览器检查 CAP retry、Hangfire JobId 和 restored 5088 SQLite unknown 状态。完整命令、退出码、TRX、已保留失败尝试及外部边界见 `artifacts/fix-round-1/validation.md`。
- 本报告前文的 `OPEN_ACTIONABLE: 0` 是首次 T06 执行结论，不覆盖当前 `review.md` 的 8 个 finding 状态。本轮 executor 已完成 recommended 范围；`review.md` 保持 `NEEDS_FIX` 与原始 finding 标记不变，下一步由父线程安排原 Sol/high 独立复验。
- 无剩余已知可执行 FIX；Dashboard URL 未配置，外部 Dashboard 跳转未验证；原矩阵的 2 个 SQLite 环境证据探针为 `NotExecuted`，不计 PASS。没有暂存、提交、推送、PR 或通知。

### Round 2

- Review 状态：独立 Sol/high 的 `review.md` 为 `NEEDS_FIX`；本轮收到 3 个 `OPEN_ACTIONABLE`（FIX-002、FIX-008、FIX-009），其余 6 项为 Reviewer 已关闭项，本轮未重修。
- Fix Scope：`recommended`。只处理 FIX-002、FIX-008、FIX-009 及直接依赖；未改 `review.md`、Round 2 旧证据、包版本、批准 API baseline 或性能预算。
- 原始 review 字节备份：`artifacts/fix-round-2/review-input.md`；执行前与 `review.md` SHA256 一致（`BCDFD0D3009D0B61CDF6E6A0BCF8E6D5EC13DB58BADF13357F84D769BF4296A3`）。Round 2 验证索引：`artifacts/fix-round-2/validation.md`；CIA：`artifacts/fix-round-2/cia.md`。
- 本轮没有覆盖已验收 F008/Uow/Sample 修改，没有暂存、提交、推送、PR 或通知。`review.md` 保持 Reviewer 原文及 `NEEDS_FIX` 状态；以下是执行方修复与证据，等待父线程交原 Sol/high 复验。

#### FIX-002

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行方状态：已修复，待独立复验。
- 根因：`CapSourceRuntime.CreateOperationScope` 创建 async scope 后，`CapOperationScopeResource` 构造时解析原生 publisher 若抛错，创建者未将 scope 转交 root，scope 中已构造依赖无人释放。
- 修复：scope 成功转交给 resource 前由创建方负责清理；publisher 构造失败时释放 async scope，保留构造原异常；清理同时失败时以 `AggregateException` 保留原异常和清理异常。物理事务 ownership 不变。
- 永久故障注入：`OperationScope_WhenPublisherConstructionFails_ShouldDisposeDependenciesOnceAndRetainFailures` 经公开 CAP 配置令 publisher/依赖构造失败，断言依赖在 root/host 清理后仍恰好释放一次、root rollback-only，且不可提交 CAP Outbox。
- lifecycle 回归还覆盖正常 root、participant、独立 root 的 scoped publisher 复用、ServiceProvider 归属与逆序清理。直接测试见 `CapMySqlIntegrationTests.RuntimePublisher_ShouldReuseWithinOperationScopeAndDisposeWithRoot` 及 `RuntimeStartFailure_ShouldStopAttemptedServicesInReverseAndDisposeAfterCleanupErrors`。最终 gated lifecycle net8 TRX 为 8 passed / 0 failed / 0 skipped。

#### FIX-008

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行方状态：已补齐所列直接缺口，待独立复验。
- 生产路径：适配器的真实 `IUnitOfWorkMessagePublisher.PublishAsync`、`CapOperationScopeResource` 事务 ambient 安装/恢复与 root/participant 生命周期。
- 新增直接断言：`UnitOfWorkPublisher_ShouldRestoreNativeAmbientAfterSuccessAcrossNestedAndIndependentScopes` 真正调用适配后的 `PublishAsync`，覆盖跨 await 发布、参与者发布和独立 root 发布/rollback/dispose，并检查每次前后 `ICapPublisher.Transaction.Value`、当前 root 和 source 的恢复；`UnitOfWorkPublisher_WhenNativePublishThrows_ShouldRestoreAmbientAndCurrentSourceRoot` 覆盖 CAP native publish 异常后的 ambient 恢复、rollback-only 及资源释放；`UnitOfWorkPublisher_WhenPreCancelled_ShouldMarkReadyAndNotReadyDapperOrEfRollbackOnly` 覆盖 ready/not-ready 的 Dapper/EF、预取消副作用前拒绝及 ambient 不变。
- 原有 `BorrowedBuffer_ShouldLeavePhysicalOwnershipToRootAndFlushOnlyAfterCommit`、`RuntimeStartFailure_ShouldStopAttemptedServicesInReverseAndDisposeAfterCleanupErrors` 等已通过职责测试保留；未用通用 Uow 测试替代 CAP ambient 断言。方法与证据精确映射见 Round 2 `traceability.md` 表。

#### FIX-009

- 严重程度：HIGH；处理要求：MUST_FIX；执行方状态：已修复并完成当前独立进程验证，待独立复验。
- 根因：最终生效存储身份校验依赖 `ISqlDatabaseIdentityResolver`，原独立 Consumer 宿主未注册该依赖，Consumer 在启动 CAP runtime 时崩溃。
- 修复：`AddCapUnitOfWorkMessagePublisher` 为默认 standalone runtime/Consumer 注册默认 `ISqlDatabaseIdentityResolver` 与默认身份 contributor，使用 `TryAdd` 保持宿主自定义 resolver 优先；最终 CAP Provider、原生 options 与借用物理库一致性校验仍有效。`SqlUnitOfWorkConsumerHost.CreateHostBuilder` 被 Program 和直接宿主启动测试共同使用，避免仅测试另一份近似 DI 配置。
- 永久回归：`AddCapUnitOfWorkMessagePublisher_ShouldRegisterIdentityResolverForMinimalConsumerHost`、`AddCapUnitOfWorkMessagePublisher_ShouldPreserveHostIdentityResolver`、`MinimalConsumerHost_ShouldStartBothConfiguredCapRuntimes`。Round 2 targeted 注册 TRX 1/1；完整 EventBus net6/net8 各 385/385。
- 真实进程：当前 Release net6 Consumer 首次进程 PID 27304 两源 runtime 均完成启动；发布端进程 PID 80036 发布首个双源 operation `a47a35c1e8544a1292594157d435d11a`。停止发布端/Consumer 后，以当前二进制重启 Consumer PID 14120（两队列 active）和发布端 PID 97456，再发布 `bc433b22775d42baac5154b8a6c5bcec`。独立读取 orders/reporting 两个 MySQL schema，两个 operation 在每个 source 的 `cap.published`、`cap.received`、Inbox、effect 和 attempt 均为各 1，且 attempt `Succeeded`、重启后无重复副作用；原生 CAP Message ID、source、topic、group、trace、root 与 inbox/effect 互相对应。原始独立查询见 `artifacts/fix-round-2/consumer-v2-both-operations-{orders,reporting}-sql.txt` 和 `consumer-v2-both-operations-counts.txt`。

### Round 2 验证汇总

- 受影响项目 Release 矩阵：4 个 `dotnet test` 进程全部退出码 0：Sample net6 为 30/30，Sample net8 为 30/30，EventBus net6 为 385/385，EventBus net8 为 385/385，合计 830 passed / 0 failed / 0 notExecuted。MySQL 集成门控在两个 Sample 命令中显式设置 `RUN_CAP_MYSQL_TESTS=1`。原始命令、环境与退出码记录在对应 `artifacts/fix-round-2/*-round2.console.log`，TRX 在 `artifacts/fix-round-2/trx/round2-*.trx`。
- 另外执行 Sample lifecycle targeted net8（`RUN_CAP_MYSQL_TESTS=1`，最终 attempt4 为 8/8）和 EventBus registration targeted net8（1/1）。前两轮 targeted 失败尝试保留在 `artifacts/fix-round-2/trx/`，不计 PASS，也不与最终矩阵重复累加。
- 实际浏览器用 CUA 在 `http://127.0.0.1:5098/diagnostics/events` 填入第二次重启后的 operation ID 并点击“查询”。页面显示筛选值、两源 `observed`、各 source `committed / 1 / 1`、2 个事件、2/2 成功消费，以及各源的 native ID、topic、group/ConsumerName、attempt 与副作用数。浏览器截图在本轮 CUA 工具结果中呈现；可检索页面证据摘要见 `artifacts/fix-round-2/browser-filter-observation.md`。
- 任务 Compose 的 MySQL 与 RabbitMQ 均为 healthy；当前独立 Consumer/发布端均已停止。仅任务拥有的 5088 SQLite Sample 保持运行，端口进程 PID 51024；数据库/镜像服务未清理，数据留存。
- Round 2 执行方修复项完成数：3/3。独立审查结论仍由 `review.md` 控制，未由执行方自行改写为 PASS；等待原 Sol/high 复验。没有已知执行方 OPEN_ACTIONABLE；SQLite 默认离线入口已恢复。未重跑无关 Rabbit outage 全矩阵。

### Round 3 限定 Review Fix

- 用户授权：2026-10-22，父线程明确回复“是的，需要追加一次限定修复”，授权超出前三轮上限的一次限定修复及随后原 Sol/high 专项验收。仅处理 Round 3 `review.md` 唯一 `OPEN_ACTIONABLE FIX-002`；其余 8 个 CLOSED 项未重开。
- 注册：`.agents/scripts/task-state.mjs review-fix 20261021-multisource-cap-diagnostics --source codex --fix-scope recommended`；task runtime 本轮 `reviewRound=3`。FIX-002 为 MEDIUM / SHOULD_FIX，是本轮唯一纳入项。
- 输入完整性：原始 `review.md` 字节备份到 `artifacts/fix-round-3/review-input.md`。开始和结束 SHA256 均为 `C4027EE77E58EA07D1DF3AE67FCF17D02D931C5C0FA6FAED140FC76FBCD73991`；`review.md` 与 review-round-3 历史证据未修改。
- 范围与文件：`framework/src/Bing.Events/Cap/CapUnitOfWorkRuntimeManager.cs`、`CapUnitOfWorkMessagePublisher.cs`、Sample `CapMySqlIntegrationTests.cs`、本执行报告、`traceability.md`、`delivery-status.md`，以及 `artifacts/fix-round-3/`。

#### FIX-002

- 执行状态：已完成本次限定修复，待原 Sol/high 专项复验；不等同 Reviewer 关闭或 PASS。
- 根因：`CapSourceRuntime.CreateOperationScope` 在资源登记 root 前解析原生 publisher；构造失败时同步 `.GetAwaiter().GetResult()` 等待异步 scope 清理。已 ready 的真实 SQL 资源会让公开 `PublishAsync` 在返回 Task 前保持调用线程；真正挂起的依赖 `DisposeAsync` 将 continuation Post 到该线程的单线程 `SynchronizationContext`，形成确定等待环。
- 修复：CreateOperationScope 只创建 scope resource，root 先登记并持有唯一资源所有权；`GetOperationPublisherAsync` 使用资源中缓存的单一 async publisher 初始化任务；`PublishAsync` await 获取。初始化错误路径 await 一次性 scope disposal，无同步阻塞；保留原始构造异常与 cleanup error，且设置 rollback-only。缓存 disposal Task 防止 root/host 再次释放。物理连接/事务仍由 Uow root 唯一拥有；CAP 既有 dispatcher、recovery、retry 与旧入口未动。
- 永久回归：`CapMySqlIntegrationTests.OperationScope_WhenAsyncPublisherCleanupCapturesSingleThreadContext_ShouldReturnAwaitableFailure` 使用 ready MySQL Dapper 事务、公开 `IUnitOfWorkMessagePublisher.PublishAsync`、同步与异步 scoped dependency、实际 `await Task.Yield()` 的 async disposer 和 owner 单线程 context。外层 45 秒有界子 testhost 防止旧实现挂死主进程；测试同线程泵送 continuation，不强行异线程解锁。断言调用者先拿到未完成 Publish Task 再 await 失败、原始/cleanup 异常均可辨、root rollback-only、sync/async 创建和释放各一次、root/host 后续无二次释放；独立连接读 orders/reporting `cap.published` 均为0。实际 ID 和 SQL 行数见对应 TRX 标准输出。
- 既有生命周期与真实发布职责测试继续执行：`OperationScope_WhenPublisherConstructionFails_ShouldDisposeDependenciesOnceAndRetainFailures`、`RuntimePublisher_ShouldReuseWithinOperationScopeAndDisposeWithRoot`、`UnitOfWorkPublisher_ShouldKeepBusinessAndOutboxInvisibleUntilRootCommit`、`UnitOfWorkPublisher_ShouldRestoreNativeAmbientAfterSuccessAcrossNestedAndIndependentScopes`、`UnitOfWorkPublisher_WhenNativePublishThrows_ShouldRestoreAmbientAndCurrentSourceRoot`。
- 最终 Release 职责验证：net8 6/6、net6 6/6，失败0、未执行0；两框架 MySQL 门控都显式 `RUN_CAP_MYSQL_TESTS=1`。每条最终 `dotnet test` 命令退出0，调用其目标框架 Release build 退出0/错误0；TRX 与 console 原件、命令、迭代失败尝试见 `artifacts/fix-round-3/validation.md`。
- 环境恢复：任务 Compose MySQL/RabbitMQ healthy；执行构建前按 CIM 归属核验短暂停止的仅是本任务 Sample。最终 SQLite Sample 恢复为 PID 55756，`http://127.0.0.1:5088/diagnostics/events` 返回200，见 `artifacts/fix-round-3/restored-sample-final.json`。
- 不重跑无关 Consumer/双源重启/outage/UI/browser/全 Release/benchmark 矩阵；复用原 review 历史证据。`git diff --check` 退出0。未更改 API baseline、包版本或性能预算；未暂存、提交、推送、PR 或发送通知。

### Round 3 汇总

- 本次额外授权纳入项：1；执行完成：1；未完成：0；执行方称为待复验，不把 `review.md` 的 OPEN 状态覆盖为验收结果。
- 最终职责 TRX：Sample net8 6 passed/0 failed/0 notExecuted；Sample net6 6 passed/0 failed/0 notExecuted。门控真实运行，不以 skip 计 PASS。构建有 SDK net6 EOL warning 和已有 Sample nullable-context warnings，0 errors。
- 新证据索引：`artifacts/fix-round-3/cia.md`、`validation.md`、`trx/fix-round-3-lifecycle-net8-output-check-attempt2.trx`、`trx/fix-round-3-lifecycle-net6-output-check.trx`、`restored-sample-final.json`。历史失败原件保留且未计通过。
- 下一步：父线程安排原 Sol/high 进行唯一 FIX-002 专项复验；review 仍为 NEEDS_FIX，执行方未自判 PASS。
