# Bing.Samples.SqlUnitOfWork

这是一个可运行的多数据库工作单元示例，使用两个独立 SQLite 文件演示 HTTP 请求、`BackgroundService` 消费者和 Hangfire 作业中的真实业务 scope。

## 启动

默认使用 Dapper：

```powershell
dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj
```

启用 EF Shared bridge：

```powershell
dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -- --SqlUow:Storage=EfBridge
```

显式启用业务操作 executor：

```powershell
dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -- --SqlUow:OperationsEnabled=true
dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -- --SqlUow:Storage=EfBridge --SqlUow:OperationsEnabled=true
```

`SqlUow:OperationsEnabled` 默认为 `false`。启用后新增 `POST /api/operation-scenarios/{scenario}`；Worker 与 Hangfire Server 也会将每项工作交给 `IBingOperationExecutor`，在新依赖注入 scope 中先开始 root，再重新解析 `OperationScenarioService`。队列和 Hangfire 参数只传操作、跟踪及数据库路由标量。关闭开关时新路由返回 404，原手工 Uow 路由继续工作。启用开关时 `Startup` 调用 `AddBingOperations()`，开启已注册的 opt-in EF 保存处理器；同一容器中的旧手工 root 若包含已解析且映射的 tracked EF 变更，也会在完成前保存。默认 `AddUnitOfWorkScopes()` 不自动保存，仍需显式 `SaveChanges`。

本 Sample 演示显式 executor 调用，不会自动代理所有业务方法。需要 AspectCore `[UnitOfWork]` owner/participant 边界时，另在应用中启用 `EnableAop()` 与 `EnableBingAutomaticUnitOfWork()`；不要把 `EnableBingUnitOfWork()` 的旧注册当成新自动边界。executor 的 root 拥有资源提交、回滚与释放；participant 失败会使 root rollback-only。`requiresNew` 只创建同一个 DI scope 中的新 root，不会替换原有 `DbContext`；需要独立 Context 时使用 executor 创建的新 DI scope。executor 返回前完成 root 并释放作用域，因此只让已物化结果离开回调，不返回 `DbContext`、Query、reader、stream 或延迟枚举。

operation 的 caller token 会与配置的 `UnitOfWorkScopeOptions.Timeout` 组成协作取消边界；Sample 将 HTTP request、host shutdown 或 Hangfire token 交给 executor。将收到的 token 继续传给业务异步 I/O。期限不会强制终止忽略取消的用户代码。

宿主启动时创建专属临时目录，包含 `orders.db` 和 `reporting.db`。数据库结构初始化完成后才开始业务处理；宿主停止并释放服务后删除该目录。`EfBridge` 模式显式将 `orders` 映射到同一 DI scope 中的 `SampleUnitOfWorkDbContext`，`reporting` 仍使用独立 SQLite 数据源。

## HTTP 场景

默认地址为 `http://localhost:5000`。可直接运行 [`SqlUnitOfWork.http`](SqlUnitOfWork.http) 中的请求。

| 请求 | 场景 | 预期结果 |
| --- | --- | --- |
| `POST /api/scenarios/cross-layer-commit` | 应用层和 orders 仓储分别写入并读取未提交数据 | 两行提交；两个注入 Query 实例独立 |
| `POST /api/scenarios/cross-layer-rollback` | 同上后回滚 | 响应可见两行；独立连接最终读到零行 |
| `POST /api/scenarios/multi-database-commit` | orders 与具名 reporting 工厂分别写入 | 两个 SQLite 文件各提交一行 |
| `POST /api/scenarios/multi-database-rollback` | 两个数据源分别写入后回滚 | 两个文件的最终行集均不变 |
| `POST /api/scenarios/caught-sql-exception` | 捕获重复主键异常后尝试完成 root | 返回冲突；rollback-only 阻止提交先前写入 |
| `POST /api/scenarios/factory-query-lifetime` | 单一 root 中创建、查询并释放 100 个工厂 Query | 后续注入 Query 仍可使用 root 并完成写入 |
| `POST /api/scenarios/ef-dapper-commit` | EF `SaveChanges` 与 Dapper 写入共同提交 | 仅 `EfBridge` 模式支持，两个来源各提交一行 |
| `POST /api/scenarios/ef-dapper-rollback` | EF `SaveChanges` 与 Dapper 写入共同回滚 | 仅 `EfBridge` 模式支持，最终不留记录 |
| `POST /api/scenarios/ef-complete-without-save` | 跟踪 EF 实体但不调用 `SaveChanges`，另执行 Dapper 写入 | 默认模式下 EF 实体不落库；启用操作模式后由 opt-in 保存处理器自动保存 |
| `POST /api/scenarios/scope-isolation-probe?targetOperationId={operationId}` | 只读非事务 scope；目标参数可省略 | 响应包含 root ID；提供目标操作 ID 时读取对应 orders 行，不会修改数据库 |
| `POST /api/operation-scenarios/operation-success` | executor 建立新 scope/root，Dapper 写入；`EfBridge` 还加入 EF tracked-only 记录 | 成功响应在保存和提交完成后返回 |
| `POST /api/operation-scenarios/operation-failure` | 在写入后抛出业务异常 | 返回冲突，已暂存行回滚 |
| `POST /api/operation-scenarios/operation-cancellation` | 写入后等待请求取消 | 请求取消时回滚尚未提交的行 |
| `POST /api/operation-scenarios/operation-ef-tracked-only` | 仅 `EfBridge`；只跟踪 EF 变更，不调用 `SaveChanges` 或 Dapper | opt-in 保存处理器提交一行 EF 记录 |

未提供 `targetOperationId` 时，探针只读取当前操作 ID 下的行；提供先前操作 ID 可复查其 orders 数据。

`POST /api/worker/scenarios/{scenario}` 会投递只含场景名、操作标识及标量上下文的内存队列消息；关闭操作模式时每项工作由旧手工 service 执行，启用时由 executor 创建独立 DI scope。`POST /api/jobs/scenarios/{scenario}` 会通过 MemoryStorage 入队给真实 Hangfire Server；启用操作模式时 Hangfire cancellation token 会传入 executor，关闭时沿用旧两参数 job。两个入口都返回 `operationId`，可用 `GET /api/operations/{operationId}` 查看 queued、running、completed、failed 或 cancelled 状态及错误信息。

operation 回调应返回 DTO、标量或其他已经物化的结果；executor 完成提交并释放其 DI scope 后才向入口返回。查询、reader、流或延迟枚举必须在回调内消费并释放。

## 测试

```powershell
dotnet build samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -c Release
dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj -c Release -f net6.0
dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj -c Release -f net8.0
```

测试使用随机本地 HTTP 端口和真实 SQLite 文件；数据库行集通过独立连接校验。后台场景按顺序执行，Hangfire 测试串行运行。宿主停止后验证 worker 退出、监听端口关闭和临时目录删除。

## 边界

每个 SQLite 文件拥有自己的本地事务，提交顺序不构成跨库原子提交。Hangfire 使用内存存储，进程退出后不保留排队作业；该示例验证执行生命周期，不提供持久化队列。

## CAP 多源模式

默认 SQLite 与上面的离线启动、HTTP、Worker、Hangfire 和测试保持不变。真实 CAP 模式连接任务专属 MySQL 与 RabbitMQ：

```powershell
& ai_docs/tasks/20261021-multisource-cap-diagnostics/scripts/start-infra.ps1
dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -- --SqlUow:Storage=CapMySql
```

需要把 `orders` 的首次资源创建交给 EF 时，使用 `--SqlUow:Storage=CapMySqlEfBridge`。连接地址可由 `BING_SQLUOW_MYSQL_ORDERS`、`BING_SQLUOW_MYSQL_REPORTING`、`BING_SQLUOW_CAP_RABBITMQ` 与 `BING_SQLUOW_MYSQL_ADMIN` 覆盖。默认值只指向 `127.0.0.1` 上本任务 Compose 的端口和数据库。结束后执行 `ai_docs/tasks/20261021-multisource-cap-diagnostics/scripts/stop-infra.ps1`；脚本保留 MySQL 数据卷。

`IMessageEventBus` 的旧入口继续保留原契约：`MessageEvent.Send=false` 会延迟到工作单元完成回调执行，`Send=true` 表示原有 CAP 自管事务发布。旧入口不会借用业务事务。新增 `IUnitOfWorkMessagePublisher.PublishAsync(dataSourceKey, message)` 要求活动的事务型 root，且要求 `Send=false`；该调用立即将消息写入指定数据源当前未提交事务中的 CAP Outbox。它返回时只说明 Outbox SQL 已加入当前事务，不代表消息已投递或消费。`Send=true` 会被拒绝，避免把新事务语义与旧入口混用。

每个数据源各有 CAP 存储、运行时容器和 Outbox。Uow root 拥有物理连接及事务；CAP 仅在发布调用中借用事务，root 提交后才刷新 CAP 缓冲。多源 root 按资源登记顺序提交，第二库失败时可能留下首库已提交结果；样例不会把这种结果报告成全局回滚。

### 真实场景

| `scenario` | 验证内容 |
| --- | --- |
| `memory-event-immediate` / `memory-event-failure` | 当前请求 scope 的内存 handler 即时执行；异常传播到请求处理 |
| `cap-multi-source-commit` / `cap-multi-source-rollback` | 两库业务行和各自 CAP Outbox 同事务提交或回滚 |
| `cap-message-first-commit` | 消息先建立 orders 事务资源，随后 Dapper 写入复用该事务 |
| `cap-ef-first-commit` | EF `SaveChanges` 先建立 orders 资源，CAP 与 EF 共用该事务 |
| `cap-duplicate-event` | 既有 CAP 发布/consumer 场景中，同一业务 EventId 发布两次并核对幂等副作用 |
| `cap-consumer-retry` | 既有 CAP consumer 场景中，首次失败后由 CAP 重试并记录失败与成功尝试 |
| `cap-partial-commit` | 关闭 reporting 资源连接，核对 orders 已提交、reporting 未提交 |

HTTP 入口为 `POST /api/scenarios/{scenario}`；Worker 与 Hangfire 使用 `/api/worker/scenarios/{scenario}` 和 `/api/jobs/scenarios/{scenario}`，仍执行相同业务服务。发布与消费者进程分离运行：

```powershell
dotnet run --project samples/Bing.Samples.SqlUnitOfWork.Consumer/Bing.Samples.SqlUnitOfWork.Consumer.csproj -c Release
dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj -c Release -- --SqlUow:Storage=CapMySql
```

消费幂等键为来源数据源、EventId 和 ConsumerName。CAP 原生 `received` 表、尝试记录、Inbox 与 Effects 分开观察；Inbox 和 Effects 在同一业务事务中。回调错误只代表消费处理失败，不会把已提交的业务交易回滚。该 CAP 场景组与 `SqlUow:OperationsEnabled` 的 operation executor 入口分开；旧 consumer 场景或仅启动 publisher host 不证明新 operation 的 Outbox 消息已被独立 consumer 接收并产生 effect。新 operation 到 consumer 的双进程证据以[阶段 B 执行记录](../../ai_docs/tasks/20261022-abp-phase-b-operation-uow/execution.md)中对应的 MySQL/Rabbit 结果为准。

### 诊断与验证

打开 `/diagnostics/events` 可按 operationId、EventId、TraceId、source 和状态过滤。页面从两份 MySQL 的业务行、CAP 原生 `published`/`received` 表和 Consumer 表读取现状。运行中的请求状态只存在发布宿主内存中；进程重启后，历史状态会从已持久化事件行恢复。未配置 CAP 模式、库不可达或所需表尚不存在时，页面会显示未知、不可用或未观测，不推断成功。

MySQL 集成用例默认在 TRX 中标记为跳过。只对本任务 Compose 启用：

```powershell
$env:RUN_CAP_MYSQL_TESTS = '1'
dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj -c Release -f net6.0 --filter FullyQualifiedName~CapMySqlIntegrationTests
dotnet test samples/Bing.Samples.SqlUnitOfWork.Tests/Bing.Samples.SqlUnitOfWork.Tests.csproj -c Release -f net8.0 --filter FullyQualifiedName~CapMySqlIntegrationTests
```

测试会独立连接核对提交前可见性、提交后业务行与 Outbox、回滚结果以及旧 `Send=true` 入口造成的孤立 Outbox 回归。集成测试的通过范围以其实际测试结果为准；publisher host 的行集结果不替代 operation host 到独立 consumer 的端到端证据。消费者重试/幂等、进程重启与部分提交也需由相应 E2E 结果支持，不能用启动状态或日志代替数据库行集。
