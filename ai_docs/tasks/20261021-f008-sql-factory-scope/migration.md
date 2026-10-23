# F008 SQL 工厂生命周期迁移说明

## 默认生命周期

`AddSqlCore` 注册的 `ISqlQueryFactory`、`ISqlExecutorFactory`、`ISqlMultipleQueryExecutorFactory` 和 `ISqlTransactionScopeFactory` 现在默认是 Scoped。EF Core 的 `IEfCoreSqlQueryFactory` 同样是 Scoped。`IDataFilter` 保持 Scoped；元数据、Provider 注册表、Builder factory 和映射缓存仍为 Singleton。

通常的 HTTP 请求、Scoped 仓储、Scoped 查询服务和已有 Hangfire 作业 scope 可继续构造注入工厂。工厂在解析时绑定其 DI scope；在 scope A 解析后传入 scope B 调用，它仍持有 A 的服务。不要把工厂或由它创建的 Query、Executor 缓存在 Singleton 或传递到另一个业务 scope。

## Singleton 与后台操作

Singleton 消费者不得直接构造注入默认 Scoped 工厂。在每个业务操作中通过 `IServiceScopeFactory` 建立 scope，并在该 scope 内解析工厂及其产物：

```csharp
public sealed class BackgroundWorker
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BackgroundWorker(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        using var query = factory.Create();
        await query.Sql("Select 1").ScalarAsync<int>(cancellationToken: cancellationToken);
    }
}
```

作用域要覆盖查询执行、异步枚举、reader 使用和资源释放全过程。现有 Scoped Admin 服务和 Hangfire 作业不需要批量修改构造签名；需要确认的是 Singleton 后台组件是否已为每次操作创建 scope。

## 对象与资源所有权

- 直接从 scope 的 `IServiceProvider` 解析 transient Query、Executor 或 Multiple Executor 时，对象由 DI scope 管理并在 scope 释放时清理。
- 通过工厂的 `Create` 创建的对象不由 DI scope 自动追踪释放。调用方应使用 `using` 或 `await using`，并在释放 scope 前先释放 Query、Executor、Multiple Executor 和 transaction。
- 事务通过对应 Scoped `ISqlTransactionScopeFactory` 开始。事务 scope 管理其连接和事务资源；子 Query/Executor 必须通过事务 scope 创建，并在事务完成或释放前结束执行和流式枚举。
- Shared EF 查询继续使用 EF Core 提供的连接，Independent 查询继续使用独立数据源连接；连接所有权规则没有变化。

## 数据上下文与过滤器

Query、Executor 或 Transaction 在 `Create`/`Begin` 时解析并固定数据库上下文。既有优先级仍是显式 `SqlOptions` context、`IDatabaseContextAccessor.Current`、元数据默认值；显式 `dbKey` 和 MappingProfile 规则不变。之后改变 ambient 数据源不会重定向既有对象，需要新路由时应创建新的对象。

`IDataFilter` 在 Builder 渲染时读取当前 scope 状态。嵌套 Disable/Enable 在原 scope 内恢复，独立 scope 互不影响。原始 SQL、Multiple batch/reader 接口保持既有语义，不会自动追加软删除谓词。

## 自定义注册

`TryAddScoped` 保留调用方预先注册的工厂类型、实例或委托；默认注册后的额外注册仍按 Microsoft DI 的后注册解析规则工作。调用方自定义的 Singleton 工厂不会被替换，因此仍由注册者负责其依赖和跨请求状态契约。改动不会自动修复其他 Singleton 对 Scoped 服务的直接注入。

## 兼容范围

公共方法、Provider SPI、Provider 支持范围、NuGet 包版本和 API baseline 均未改变。改动只调整默认工厂生命周期和关联测试/fixture scope。映射缓存键、SQL 格式化、Clone 算法及 EF StoreBase 资源所有权没有改变。
