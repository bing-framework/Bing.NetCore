# API Delta

比较基准是 B00 捕获的 dirty working tree，而不是 HEAD。当前候选为 `phase-b-review-fix-round-2-2026-10-23-v1`，文件清单位于 `artifacts/review-fix-round-2/candidate-review-fix-round-2-v1.json`。Phase B 首轮实现只增加计划冻结的 companion API；Review Fix Round 1 和 Round 2 均没有新增公开签名，六组旧 Uow 接口仍没有新增必实现成员，也没有替换既有调用入口。

## 新增公开 API

| 程序集/命名空间 | 新增签名或成员 | 说明 |
| --- | --- | --- |
| Bing.Uow / `Bing.Uow` | `IUnitOfWorkExecutionScope : IUnitOfWorkScope`，提供 `CancellationToken` | 操作 root deadline 与 caller token 的执行 scope。 |
| Bing.Uow / `Bing.Uow` | `IUnitOfWorkOperationManager : IUnitOfWorkScopeManager`，`BeginOperation(UnitOfWorkScopeOptions options = null, CancellationToken cancellationToken = default)` | 显式 opt-in owner/participant 入口。 |
| Bing.Uow / `Bing.Uow` | `IUnitOfWorkSaveChangesHandler.SaveChanges/SaveChangesAsync` | owner root 完成前调用的保存扩展点。 |
| Bing.Uow / `Bing.Uow` | `IBingOperationExecutor.ExecuteAsync<TService>` 和 `ExecuteAsync<TService,TResult>` | 新 DI scope 内先 Begin，再解析服务并等待一次委托。 |
| Bing.Uow / `Bing.Uow` | `BingOperationServiceCollectionExtensions.AddBingOperations(IServiceCollection)` | 注册 opt-in 操作执行与保存处理链。 |
| Bing.Uow / `Bing.Uow` | `UnitOfWorkScopeOptions.Timeout : TimeSpan?` | 新 deadline 可选；默认 null，不隐式限时。 |
| Bing.Ddd.Application.AspectCore / `Bing.DependencyInjection` | `BingAutomaticUnitOfWorkOptions.DefaultOptions` 与 `EnableBingAutomaticUnitOfWork(IServiceCollection, Action<BingAutomaticUnitOfWorkOptions> configure = null)` | 显式启用自动代理边界；旧 `EnableBingUnitOfWork` 的模式保留。 |
| Bing.EntityFrameworkCore / `Bing.EntityFrameworkCore` | `EfCoreSqlQueryServiceCollectionExtensions.AddEfCoreOperationSaving(IServiceCollection)` | 显式注册已解析 EF Context 保存适配，不自行解析 Context。 |

`Bing.Dapper.Core` 的 `ISqlUnitOfWorkResourceAccessor` 记录在 B00 起点快照，未由本轮新增；其 `PublicAPI.Unshipped.txt` 与 B00 快照逐字节相同。`eng/baselines.props` 未修改。SQL/Dapper API baseline 没有为本轮行为修改更新。

## 保留的兼容契约

- `IUnitOfWork`、`IUnitOfWorkManager`、`IUnitOfWorkScope`、`IUnitOfWorkScopeManager`、`IUnitOfWorkResource`、`IUnitOfWorkConnectionResource` 的既有签名保持；默认旧模式仍手工 Begin/Save/Commit。
- 只有 `AddBingOperations` 或 `EnableBingAutomaticUnitOfWork` opt-in 的容器才自动调用保存处理器。旧 `AddUnitOfWorkScopes` 保持 tracked-only EF 不因 root Complete 自动写入。
- Sample 的 `SqlUow:OperationsEnabled` 默认 false；新路由独立，既有 `/api/scenarios/{scenario}` 保持。
- SDK、目标框架、包/程序集版本、`PublicAPI.Shipped.txt`、`PublicAPI.Unshipped.txt`（相对 B00）、以及 API baseline/性能预算均未由本轮更新。

## Review Fix Round 2

Round 2 仅调整既有实现与测试：同步 EF Store 取消边界、requiresNew Context 归属与自动保存准入、automatic AspectCore 组合链验证，以及验证报告/候选清单。没有添加、删除或改签名，没有修改 `PublicAPI.Shipped.txt`、`PublicAPI.Unshipped.txt` 或 `eng/baselines.props`；SDK、TFM、包/程序集版本及性能预算保持不变。

当前代码证据：EF 精准 L1 net6 27/0/0、EF 完整 L2 net6 219/0/0、SQLite L2 net6/net8 各 233/0/1；automatic proxy 组合链 L1 net6 18/0/0、AOP L2 net6/net8 各 178/0/0，EventBus L2 各 409/0/1。方法、退出码和原始 TRX 见 `traceability.md` 与 `artifacts/review-fix-round-2/l2/`。本轮没有修改产品文档；Release build 与默认完整 L4 未刷新，均为 `NOT_REFRESHED_AFTER_FIX`，Round 1 结果不作为当前候选门禁。

## 兼容证据

- `Bing.Uow.Tests.OperationApiContractTest.OperationContracts_ShouldMatchFrozenSignaturesAndRetainLegacyScopeApi`、`AddBingOperations_ShouldRegisterOperationManagerAndExecutor` 与 `UnitOfWorkScopeRegistrationTest.Registration_BingOperations_ShouldRejectLegacyOnlyCustomManager` 检查冻结签名、旧入口和 opt-in 注册。Round 2 Uow L2 net6/net8 各 70/70，完整 TRX `artifacts/review-fix-round-2/l2/uow-net6/`、`uow-net8/`。
- `Bing.Uow.Tests.UnitOfWorkDocumentationExamplesTest.OperationExecutorExample_ShouldResolveAfterBeginAndReturnMaterializedResult` 编译并执行当前 executor 文档主例；net6/net8 精准集各 2/2，原始 TRX `artifacts/review-fix-round-1/l1/uow-fix007-api-docs-net6-v1/`、`uow-fix007-api-docs-net8-v1/`。
- `Bing.Aop.AspectCore.Tests.LegacyConsumerAssemblyTest` 与真实代理 `UnitOfWorkAdapterTest`：Round 2 AOP L2 net6/net8 各 178/178，`artifacts/review-fix-round-2/l2/aop-net6-v2/`、`aop-net8-v2/`；`LegacyMarkerConsumer.csproj` 仍在解决方案中。
- `PublicAPI.Unshipped.txt` 与 `eng/baselines.props` 均未因 Review Fix Round 1/2 变更。Round 1 Release solution build 退出码 0 的输出保留于 `artifacts/review-fix-round-1/final-candidate/Release-build/`；Round 2 当前候选 build 未刷新，状态为 `NOT_REFRESHED_AFTER_FIX`。

## Review Fix Round 1

Review Fix Round 1 修复保存状态机、旧标记 participant 分派、EF/Dapper ambient cancellation、requiresNew Context 归属与 executor Trace 隔离，并增加新模式契约覆盖和支持文档；Round 2 补同步 Store I/O 取消、EF 外层资源归属和 automatic 代理组合链回归。两轮均只改既有 API 的实现、测试和报告，没有增加或移除 public API。Round 1 默认 L4 因历史 GC 弱引用失败而为 FAIL；Round 2 Release/L4 为 `NOT_REFRESHED_AFTER_FIX`。L1/L2/L3 结果和 Reviewer 关闭状态分开记录，独立 Reviewer 状态见 `review.md`。
- `PublicAPI.Unshipped.txt` 对 B00 的 SHA256/长度相同，详见 `artifacts/b00-worktree-startpoint/snapshot-manifest.json` 中原始文件记录和当前文件。
