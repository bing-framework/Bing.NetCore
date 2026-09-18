# API 速查索引

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../ReleaseNotes.md)

> **定位**：这是一份**手工维护的"按模块 API 快速索引"**，用于快速定位「某个能力对应哪个命名空间 / 类型」。它**不是**逐方法级的 API 参考。
>
> **与 `/docs/api/` 的分工**：逐方法级的 API 参考由 **docfx 自动生成**，落在 `docs/api/`（`docfx.json` 的 `metadata.dest`），随 `.github/workflows/docfx.yml` 在 push 到 `main` / `master` / `dev` 时构建部署。
> 那一块**是生成物，不要手工编辑**；本文则是人工索引，用来补自动生成结果所缺少的语义分组——「我要做 X，该用哪个命名空间」。
>
> 权威细节请对照：
> - 契约 ↔ 实现的完整边界：[架构文档 §4](../architecture/架构文档.md)
> - 每个工程的一句话职责：[架构文档 §11 模块清单](../architecture/架构文档.md)
> - 注册入口（`AddXxx` / `UseXxx`）：[架构文档 §12](../architecture/架构文档.md)｜精简版 [使用文档 §3.3](使用文档.md)
>
> 类型签名以源码为准；若本文与源码冲突，请信任源码并回来修正本文。

---

## 1. 启动、模块与 DI

| 模块 | 命名空间 | 关键类型 | 说明 |
| --- | --- | --- | --- |
| Bing.Core | `Bing.Core.Modularity` | `IBingModule` / `BingModule` / `ModuleLevel` / `[DependsOnModule]` | 模块系统：`Level` / `Order` / `Enabled`，两阶段 `AddServices` + `UseModule` |
| Bing.Core | `Bing.Core.Builders` | `BingBuilder` / `IBingBuilder` | `AddModule<T>()` / `AddModules(...)`；模块按 `Level → Order → FullName` 排序 |
| Bing.Core | `Bing.DependencyInjection` | `DependencyModule`、`ISingletonDependency` / `IScopedDependency` / `ITransientDependency`、`[Dependency]`、`[IgnoreDependency]` | 约定式 DI 自动注册；`[Dependency]` 优先于标记接口 |
| Bing.Core | `Bing.Exceptions` / `Bing` | `Warning` / `ConcurrencyException` / `BusinessException` / `BingException` | 异常体系（`IHasErrorCode`、`IHasHttpStatusCode`） |
| Bing.Core | `Bing.Linq` | `IAsyncQueryableExecuter` / `IAsyncQueryableProvider` | `IQueryable` 的异步执行抽象（`[MultipleDependency]`） |
| Bing.Core | `Microsoft.Extensions.DependencyInjection` | `AddBing` / `UseBing`（非 Web） | 框架总入口与初始化 |

## 2. 领域与数据契约

| 模块 | 命名空间 | 关键类型 | 说明 |
| --- | --- | --- | --- |
| Bing.Uow | `Bing.Uow` | `IUnitOfWork`（`Commit` / `CommitAsync`）、`IUnitOfWorkManager` | 提交边界 |
| Bing.Data | `Bing.Data` / `Bing.Data.Queries` | `Query<TEntity>` / `IQuery`、`Pager` / `PagerList<T>`、`ICondition` | 查询对象、分页与条件 |
| Bing.ObjectMapping | `Bing.ObjectMapping` | `IObjectMapper` | 对象映射抽象（实现见 `Bing.AutoMapper`） |
| Bing.AutoMapper | `Bing.AutoMapper` | `AutoMapperObjectMapper`、`IObjectMapperProfile`、`AddAutoMapper` | AutoMapper 实现 |
| Bing.Ddd.Domain | `Bing.Domain.Repositories` / `Bing.Data` | `IRepository<TEntity,TKey>` / `IQueryRepository` / `ICompactRepository`、`IStore<TEntity,TKey>` / `IQueryStore` | 仓储与存储契约（注意在 `Bing.Ddd.Domain`，非 `Bing.Data`） |
| Bing.Ddd.Domain | `Bing.Domain.Entities` | `Entity` / `AggregateRoot` / `ValueObject`、审计接口 | 实体与聚合根基类 |
| Bing.Ddd.Application.Contracts | `Bing.Application.Services` | `IAppService` / `ICrudAppService` / `IQueryAppService`、`DtoBase` / `RequestBase`、`[UnitOfWork]` | 应用契约 |
| Bing.Ddd.Application | `Bing.Application.Services` | `AppServiceBase` / `QueryAppServiceBase` / `DeleteAppServiceBase` / `CrudAppServiceBase` | 应用服务基类 |

## 3. 数据访问实现

| 模块 | 命名空间 | 关键类型 | 说明 |
| --- | --- | --- | --- |
| Bing.EntityFrameworkCore | `Bing.Datas.EntityFramework.Core` | `UnitOfWorkBase : DbContext`、`RepositoryBase` / `StoreBase` / `CompactRepositoryBase` | EF Core 实现（运行时命名空间与包名不同） |
| Bing.EntityFrameworkCore.* | `Bing.Datas.EntityFramework.{SqlServer,MySql,PgSql,Oracle,Sqlite}` | `AddSqlServerUnitOfWork` / `AddMySqlUnitOfWork` / `AddPgSqlUnitOfWork` / `AddOracleUnitOfWork` / `AddSqliteUnitOfWork` | 各库 Provider 注册 |
| Bing.FreeSQL | `Bing.Uow` | `UnitOfWorkBase : FreeSql.DbContext, IUnitOfWork, IDatabase` | FreeSQL 工作单元（乐观锁异常包装为 `ConcurrencyException`） |
| Bing.FreeSQL.MySql | `Bing.FreeSQL` | `AddMySqlUnitOfWork<TUnitOfWork,TImpl>` | FreeSQL MySql Provider |
| Bing.Data.Sql | `Bing.Data.Sql` / `Bing.Data.Sql.Builders` | `ISqlQuery` / `ISqlBuilder` / `IDialect` / `DialectBase` / `ISqlProvider` | ORM 无关的流利 SQL 引擎 |
| Bing.Dapper.Core | `Bing.Data.Sql` / `Bing.Dapper` | `SqlProviderRuntime`、`ISqlQueryFactory`、`AddSqlCore` / `AddSqlDataSource` | Provider 路由与数据源注册 |
| Bing.Dapper.* | `Bing.Dapper.{SqlServer,MySql,PostgreSql,Oracle,Sqlite}` | `XxxSqlProvider` / `XxxDialect` / `XxxBuilder`、`AddXxxProvider()` | 各库 Dapper Provider |

## 4. Web 与横切

| 模块 | 命名空间 | 关键类型 | 说明 |
| --- | --- | --- | --- |
| Bing.AspNetCore | `Bing.AspNetCore.Mvc` / `Bing.AspNetCore.ExceptionHandling` | `ApiResult : JsonResult`、`StatusCode{Ok=1,Fail=2,Unauthorized=401}`、`BingControllerBase` / `ApiControllerBase`、`BingExceptionHandlingMiddleware` | 统一响应与异常处理 |
| Bing.AspNetCore.Mvc | `Bing.AspNetCore.Mvc.Filters` / `.ExceptionHandling` | `ResultHandlerAttribute`、`BingExceptionFilter`、`QueryControllerBase` / `CrudControllerBase`、`MvcOptions.AddBing` | 结果包装与控制器基类 |
| Bing.AspNetCore.Authentication.JwtBearer | `Bing.AspNetCore.Authorization.JwtBearer.Extensions` | `AddJwt(IConfiguration)` | 配置节 `JwtOptions`，策略名 `jwt` |
| Bing.Events | `Bing.Events` / `.Messages` / `.Handlers` | `IEventBus` / `ISimpleEventBus` / `IMessageEventBus` / `IMessageEvent` / `IEventHandler<T>` / `IEventHandlerManager` | 事件总线（两层发布） |
| Bing.Events | `Bing.Events.Cap` / `Bing.Events.Default` | `EventBus`、`MessageEventBus`、`AddCapEventBus` / `AddDefaultEventBus` | CAP 与进程内实现 |
| Bing.Caching | `Bing.Caching` | `ICache` | 缓存抽象；实现：`CSRedisCacheManager` / `FreeRedisCacheManager` |
| Bing.Security | `Bing.Security.Users` | `ICurrentUser` | 当前用户 |
| Bing.MultiTenancy | `Bing.MultiTenancy` | `ICurrentTenant` | 当前租户 |
| Bing.ExceptionHandling | `Bing.AspNetCore.ExceptionHandling` / `Bing.Http` | `IExceptionToErrorInfoConverter`、`RemoteServiceErrorInfo` | 异常 → 错误信封 |
| Bing.Aop.AspectCore | `Bing.DependencyInjection` | `EnableAop` | 启用 AOP 动态代理 |
| Bing.Logging | `Bing.Logging` | `ILog<T>` / `ILogFactory` / `ILogContextAccessor`、`AddBingLogging` | 日志抽象 |
| Bing.Locks | `Bing.Locks` | `IDistributedLock` / `ILock`、`AddRedisDistributedLock` / `AddLocalLock` | 分布式锁 / 进程内锁 |

---

## 5. 如何本地预览与重新生成 API 文档

docfx 已经配置好，通常你**不需要**重新接入，只需要在本地预览：

```bash
# 安装（一次性）
dotnet tool install -g docfx

# 在仓库根目录执行；产物输出到 docs/_site/
docfx ./docs/docfx.json --serve
```

然后打开终端中提示的地址（默认 `http://localhost:8080`）即可浏览完整站点，其中「API 文档」节点就是逐方法级的 API 参考。

### 配置要点（改动前请先读）

| 项 | 值 | 说明 |
| --- | --- | --- |
| 配置文件 | `docs/docfx.json` | 必须在**仓库根目录**执行（`metadata.src` 用的是相对路径 `../framework/src`） |
| 元数据范围 | `../framework/src/**/*.csproj` | **只覆盖 `framework/src`**——`components/src`（含 `Bing.Biz`、`Bing.Biz.Payments`）与 `modules/` 均**不在** API 站点范围内，这不是遗漏，需要时请自行追加 `src` 条目 |
| 输出目录 | `docs/_site/` | 已在根 `.gitignore` 中忽略，**不要提交** |
| 站点导航 | `docs/toc.yml` | 新增文档请同步登记，否则站点上看不到 |
| 自动部署 | `.github/workflows/docfx.yml` | push 到 `main` / `master` / `dev` 时自动构建发布 |

> 环境事实：仓库当前默认目标框架 `netstandard2.0` / `net6.0`，构建 SDK 由 `global.json` 固定为 **8.0.424**。若本地预览失败，先用 `dotnet --version` 确认 SDK 与 `global.json` 一致。
