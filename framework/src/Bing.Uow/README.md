# Bing.Uow
[![NuGet](https://img.shields.io/nuget/v/Bing.Uow.svg)](https://www.nuget.org/packages/Bing.Uow/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Uow.svg)](https://www.nuget.org/packages/Bing.Uow/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 工作单元契约：`IUnitOfWork` / `IUnitOfWorkManager` 与抽象基类。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0
>
> **上游 Bing 包**：`Bing.Core`

---

## 这是什么

定义一次业务操作的提交边界。旧 `IUnitOfWork` / `IUnitOfWorkManager` 保持可用；新增的 `IUnitOfWorkScopeManager` 管理可登记多数据源资源的 companion scope，数据访问包负责向 scope 提供本地连接和事务。

## 安装

```bash
dotnet add package Bing.Uow
```

## 快速上手：旧提交接口

```csharp
using System.Threading;
using System.Threading.Tasks;
using Bing.Uow;

public sealed class LegacyOrderAppService
{
    private readonly IUnitOfWorkManager _uowManager;

    public LegacyOrderAppService(IUnitOfWorkManager uowManager) => _uowManager = uowManager;

    public Task CommitRegisteredWorkAsync(CancellationToken cancellationToken) =>
        _uowManager.CommitAsync(cancellationToken);
}
```

`IUnitOfWorkManager` 只提交已经注册的 `IUnitOfWork`；它不创建工作单元，也没有 `Begin()`。如果应用需要显式的完成/回滚范围，使用下方的 `IUnitOfWorkScopeManager`。

## 显式工作单元 scope

下面的最小示例通过资源工厂展示 scope 契约。常规 SQL、Dapper 和 EF 代码会在首次访问时由对应数据访问包将资源加入当前 root。

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Bing.Uow;

public sealed class OrderScopeAppService
{
    private readonly IUnitOfWorkScopeManager _scopes;
    private readonly Func<IUnitOfWorkResource> _resourceFactory;

    public OrderScopeAppService(IUnitOfWorkScopeManager scopes,
        Func<IUnitOfWorkResource> resourceFactory)
    {
        _scopes = scopes;
        _resourceFactory = resourceFactory;
    }

    public async Task CreateAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.Begin();
        scope.GetOrAddResource("orders", _resourceFactory);
        await scope.CompleteAsync(cancellationToken);
    }

    public async Task FailAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.Begin();
        scope.GetOrAddResource("orders", _resourceFactory);
        await Task.Yield();
        throw new InvalidOperationException("The example operation failed.");
    }
}
```

在组合根调用 `services.AddUnitOfWorkScopes()`，并向业务服务注入 scoped 的 `IUnitOfWorkScopeManager`。默认的嵌套 scope 参与当前 root；`Begin(requiresNew: true)` 会暂挂当前 root 并创建独立 root。它不会创建新的 Microsoft DI scope，也不会重新解析已注入的 `DbContext`；需要隔离 scoped ORM 时，应用还要创建新的 DI scope 并从该 scope 解析服务。未调用 `CompleteAsync` 的 scope 在释放时回滚尚未提交的资源。以上旧/新示例由 `framework/tests/Bing.Uow.Tests/UnitOfWorkDocumentationExamplesTest.cs` 编译并验证。

## Opt-in 业务操作

`AddUnitOfWorkScopes()` 保持显式 Begin/Complete 和手工保存语义。启用 `AddBingOperations()` 后，容器才会为 root 运行已注册的 `IUnitOfWorkSaveChangesHandler`，并注册 `IUnitOfWorkOperationManager` 与 `IBingOperationExecutor`。仅调用 `AddBingOperations()` 不会自动拦截业务方法；Sample 的 [`Startup.ConfigureServices`](../../../samples/Bing.Samples.SqlUnitOfWork/Startup.cs#L92) 和 [`OperationScenarioService.ExecuteAsync`](../../../samples/Bing.Samples.SqlUnitOfWork/Application/OperationScenarioService.cs#L41) 展示了显式 executor 路径。

使用 AspectCore 自动 owner/participant 边界时，引用 `Bing.Ddd.Application.AspectCore` 并显式启用：

```csharp
using Bing.DependencyInjection;

services.EnableAop();
services.EnableBingAutomaticUnitOfWork(options =>
    options.DefaultOptions.Timeout = TimeSpan.FromSeconds(30));
```

自动模式以 `[UnitOfWork]` 标记方法作为边界：当前没有活动 root 时该调用拥有 root；存在活动 root 时该调用是 participant。participant 不物理提交资源；participant 失败会将 root 标为 rollback-only，即使调用方捕获了异常，root 也不能随后提交。旧 `EnableBingUnitOfWork()` 保留原适配路径；`EnableBingAutomaticUnitOfWork()` 是显式的新 opt-in，并包含 `AddBingOperations()` 注册。

`IBingOperationExecutor.ExecuteAsync<TService, TResult>` 会创建新的 Microsoft DI scope，先开始 operation root，再从该 scope 解析业务服务；完成或回滚并释放 root 后，再释放 DI scope。它适合需要新 `DbContext`、Query 和 manager 的独立操作。`IUnitOfWorkScopeManager.Begin(requiresNew: true)` 只创建同一 DI scope 内的新 root，不会替换已经注入的服务；同一 scope 中属于外层的 pending 或已 claim EF 状态不能迁入该 root。`IUnitOfWorkScopeManager.Current` 继续返回 root 身份；当前 participant 的取消令牌由 `IUnitOfWorkExecutionScope.CancellationToken` 提供。

Sample 的 HTTP 入口使用相同的泛型调用，并在操作 root 释放后返回已物化结果：

```csharp
var result = await executor.ExecuteAsync<OperationScenarioService, ScenarioExecutionResult>(
    (service, token) => service.ExecuteAsync(scenario, operationId, token),
    cancellationToken: cancellationToken);
```

对应的可编译入口是 [`OperationScenariosController.Run`](../../../samples/Bing.Samples.SqlUnitOfWork/Controllers/OperationScenariosController.cs#L28)；该 controller 由 Sample 测试实际启动的 HTTP host 调用。

`UnitOfWorkScopeOptions.Timeout` 与调用方令牌组成协作取消边界。启用自动模式时，业务方法的 `CancellationToken` 接收该操作令牌；显式 executor 也把合成令牌传入委托。将令牌继续传给异步 I/O。它不会强行终止忽略取消令牌的用户代码。

EF Core 的 `AddEfCoreSqlUnitOfWork<TContext>(dataSourceKey)` 显式映射并注册 EF 保存适配。opt-in root 只检查当前 DI scope 中已经解析的 `UnitOfWorkBase` 实例，并在物理资源提交前保存有待处理状态的已映射 Context；没有变更时不额外 flush。显式 `SaveChanges` 是 flush 到 root 管理的本地事务，不代表该资源已经提交；未映射 Context 的 pending 写入会被拒绝，不会被隐式独立提交。`SaveChanges(false)` 未被业务接受的状态也不能由最终 handler 自动完成。旧容器只调用 `AddUnitOfWorkScopes()` 时仍须显式 `SaveChanges`。

executor 的 `TResult` 在 root 完成后才返回。返回 DTO 或其他已物化值；不要返回依赖已释放 scope 的 `DbContext`、Query、reader、stream 或延迟枚举结果。

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IUnitOfWork` | `Bing.Uow` | `Commit()` / `CommitAsync()` 提交边界 |
| `IUnitOfWorkManager` | `Bing.Uow` | 管理并提交已注册工作单元 |
| `IUnitOfWorkScopeManager` / `IUnitOfWorkScope` | `Bing.Uow` | 管理当前异步上下文、嵌套与多资源完成边界 |
| `IUnitOfWorkOperationManager` / `IUnitOfWorkExecutionScope` | `Bing.Uow` | 显式 owner/participant 生命周期与操作取消令牌 |
| `IBingOperationExecutor` | `Bing.Uow` | 在新 DI scope 中先 Begin、再解析服务并完成操作 |
| `IUnitOfWorkResource` / `IUnitOfWorkConnectionResource` | `Bing.Uow` | 由 root 统一提交、回滚和释放的数据源资源 |
| `UnitOfWorkScopeOptions` | `Bing.Uow` | 选择本地事务或只读汇总所用的非事务 scope |

## 多数据源业务 scope

在组合根调用 `AddUnitOfWorkScopes()`，并把 scoped 的 `IUnitOfWorkScopeManager` 注入业务服务。独立创建的 transient Query 和 Executor 会在首次执行时加入当前 root；同一冻结数据源配置共享连接与本地事务。

可编译的多数据源业务示例见 [`SqlUnitOfWorkApplicationService.ExecuteAsync`](../../../samples/Bing.Samples.SqlUnitOfWork/Application/SqlUnitOfWorkApplicationService.cs#L70)：`MultiDatabaseCommit` 分支写入 `orders` 与 `reporting`，`CapMultiSourceCommit` 分支还把各自 Outbox 写入对应事务。数据源和 `AddUnitOfWorkScopes()` 在 [`Startup.ConfigureServices`](../../../samples/Bing.Samples.SqlUnitOfWork/Startup.cs#L86) 注册；默认 `Dapper` 模式使用两个本地 SQLite 文件，CAP 模式须按 [Sample README](../../../samples/Bing.Samples.SqlUnitOfWork/README.md#cap-多源模式) 配置本地 MySQL 与 RabbitMQ。

各数据源拥有自己的本地事务；root 按资源登记顺序逐个提交，不提供跨数据库原子性。后续数据源提交失败时，先前数据源可能已经提交；只读汇总的 `IsTransactional = false` scope 不会回滚已执行写入。完整迁移规则与边界见 [SQL 多数据源 Uow 迁移指南](../../../docs/sql/migration-guide.md#业务多数据源-uow)。

## 与其它包的关系

**本包依赖**：`Bing.Core`

## 注意事项

EF Core、FreeSQL 和 Dapper 继续管理各自的 ORM 生命周期；本包提供通用 scope 状态机和资源协作契约。默认 Dapper 与映射的 EF Shared 资源通过数据访问包接入同一个 root。FreeSQL 原生 ORM 的 operation 适配属于独立 B04 计划；本阶段只说明已验证的 Dapper 和显式 EF 映射范围，不据此宣称 FreeSQL 全 ORM 支持。CAP 的 Outbox/独立 consumer 能力也按各自已验证入口区分，不能由 Sample 的发布端测试推定 operation consumer 闭环。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Uow

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
