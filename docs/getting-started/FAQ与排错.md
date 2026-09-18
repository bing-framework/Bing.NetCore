# FAQ 与排错

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../ReleaseNotes.md)

> 集中收录使用/二次开发中最容易踩的坑。每条都基于源码核查，并给出「正确做法」与「依据」。
> 分主题索引：启动与模块 · 依赖注入 · 数据访问 · Web 与统一响应 · 事件总线 · 缓存 · 编码与工程。

---

## 一、启动与模块

### Q1 我声明了 `[DependsOnModule(typeof(A))]`，为什么 A 的 `UseModule` 不一定先于本模块执行？

**误区**：以为 `[DependsOnModule]` 会做拓扑排序、保证依赖模块先初始化。

**正解**：模块启用顺序**只按 `ModuleLevel` → `Order` → `FullName` 升序排序，不做拓扑排序**（源码：`_modules.OrderBy(m => m.Level).ThenBy(m => m.Order).ThenBy(m => m.GetType().FullName)`）。`[DependsOnModule]` 只决定「哪些模块被递归纳入」，不保证执行先后。若确有硬顺序需求，必须用 `Level`（Core=1 / Framework=10 / Application=20 / Business=30）或 `Order` 表达。

**依据**：[架构文档 §8](../architecture/架构文档.md)。

### Q2 `AddModule<T>()` 怎么写才对？它是 `IServiceCollection` 的扩展吗？

**正解**：不是。`AddModule<T>()` 是 **`IBingBuilder` 的扩展**（`Bing.Core.Builders`），正确写法：

```csharp
services.AddBing().AddModule<AppModule>();   // ✅
services.AddModule<AppModule>();             // ❌ 编译不过
```

### Q3 非 Web（控制台 / WinForm / 后台任务）怎么启动？

**正解**：`services.AddBing();` 后，用 `serviceProvider.UseBing();`（`Microsoft.Extensions.DependencyInjection`），而非 `app.UseBing()`。参考 `samples/Bing.Samples.Hangfire`。

---

## 二、依赖注入（约定式）

### Q4 `[Dependency]` 特性和 `ISingletonDependency` 标记接口冲突时谁生效？

**正解**：**特性优先**。例如实现 `ISingletonDependency` 同时标注 `[Dependency(ServiceLifetime.Scoped)]`，最终以 **Scoped** 注册。

### Q5 自动注册会把类型注册成哪些服务类型？

**正解**：注册类型实现的**全部接口**（除 `IDisposable` 与带 `[IgnoreDependency]` 者）。无接口时注册自身；`[Dependency(AddSelf = true)]` 时额外注册自身。覆盖控制：首接口按 `ReplaceExisting`（→`Replace`）> `TryAdd`（→`TryAdd`）> 默认（`Add`）。

### Q6 为什么找不到 `AddCache` / `AddCaching`？缓存怎么注册？

**正解**：框架**没有** `ICache` 的注册扩展（`Bing.EasyCaching` 中的实现整段被注释，不可用）。必须手工注册：

```csharp
RedisHelper.Initialization(...);                                  // CSRedisCore
services.AddScoped<ICache, CSRedisCacheManager>();                // 或 FreeRedisCacheManager
```

详见 [使用文档 §8.1](使用文档.md)。

### Q7 启用 AOP 的入口叫什么？

**正解**：**`EnableAop(Action<IAspectConfiguration>)`**（`Bing.DependencyInjection`），**不存在 `AddAop`**。

---

## 三、数据访问

### Q8 EF PostgreSQL 的工作单元注册方法名是什么？

**正解**：**`AddPgSqlUnitOfWork<TService, TImpl>(...)`**（`Bing.Datas.EntityFramework.PgSql`）。**不是** `AddPostgreSqlUnitOfWork`。注意 Dapper 侧则是 `AddPostgreSqlProvider()`——两者命名不一致，别混。

### Q9 FreeSQL 的 `AddMySqlUnitOfWork` 和 EF 的同名方法是一回事吗？

**正解**：**不是**。这是两个不同命名空间下的不同方法、参数也不同：
- EF：`Bing.Datas.EntityFramework.MySql` 的 `AddMySqlUnitOfWork<TService,TImpl>(connection, Action<DataConfig>, Action<DbContextOptionsBuilder>, Action<MySqlDbContextOptionsBuilder>)`
- FreeSQL：`Bing.FreeSQL` 的 `AddMySqlUnitOfWork<TUnitOfWork,TImpl>(connection, Action<IServiceProvider,FreeSqlBuilder>, Action<IServiceProvider,IFreeSql>)`

### Q10 FreeSQL 为什么只有 MySql？能用别的库吗？

**正解**：框架当前只发布 `Bing.FreeSQL.MySql`。核心库 `Bing.FreeSQL` 完全数据库无关，Provider 只是薄壳——按模板复制工程、换 `FreeSql.Provider.*` 包、把 `DataType.MySql` 改为目标库、重命名 `Add*UnitOfWork` 与 `IMap` 即可。详见 [子系统深挖 §2.6](../architecture/子系统深挖.md)。

### Q11 用 FreeSQL 时，异步查询会真的异步执行吗？

**正解**：**不会——而且它不报错。** 完整链路：

1. 你调 `IAsyncQueryableExecuter` 的异步扩展方法（如 `ToListAsync`）；
2. 它用 `FindProvider` 找 provider：`Providers.FirstOrDefault(p => p.CanExecute(queryable))`；
3. FreeSQL 的 `FreeSqlAsyncQueryableProvider.CanExecute` **恒返回 `false`**（占位实现；该文件另有 **65 处** `throw new NotImplementedException()`）；
4. 于是 `FindProvider` 返回 `null`，走兜底分支——**同步执行**：

```csharp
// Bing.Core/Bing/Linq/AsyncQueryableExecuter.cs:40-42
return provider != null
    ? provider.ContainsAsync(queryable, item, cancellationToken)
    : Task.FromResult(queryable.Contains(item));   // ← 同步阻塞调用线程
```

**后果**：能编译、能跑、不抛异常、日志干净，但数据库查询在**调用线程上同步执行**。在 ASP.NET Core 里等于白白占住一个线程池线程，高并发下表现为吞吐下降而**没有任何错误信号**。

EF Core 的 `EfCoreAsyncQueryableProvider` 才是真正生效的实现（真异步）。

**怎么确认自己踩到了**：对 `IQueryable` 调 `ToListAsync()`，若返回的 `Task` **立即**处于 `RanToCompletion`，就是同步降级。完整说明见 [功能完成度 §3](../architecture/功能完成度.md)。

### Q12 仓储接口 `IRepository` / `IStore` 在哪个包？

**正解**：在 **`Bing.Ddd.Domain`**，不是 `Bing.Data`。`Bing.Data` 提供的是查询对象 `Query`/`IQuery`、分页 `Pager`/`PagerList`、条件 `ICondition`。

### Q13 `IUnitOfWork` 接口有哪些方法？

**正解**：接口本身**只有** `int Commit()` 与 `Task<int> CommitAsync(...)`（位于 `Bing.Uow`）。`SaveChanges` / `SaveChangesAsync` 来自其基类（EF 的 `DbContext` 或 FreeSQL 的 `DbContext`）重写，不在接口里；回滚也由基类/事务逻辑处理。

### Q14 EF Core 运行时的命名空间为什么和我引用的包名对不上？

**正解**：EF Core 实现的运行时命名空间是 **`Bing.Datas.EntityFramework.Core`**（不是 `Bing.EntityFrameworkCore.*`）。找 `UnitOfWorkBase` / `RepositoryBase` 请到这个命名空间。

### Q15 用 Dapper 时提示找不到数据源 / Provider？

**正解**：两个注册缺一不可：
```csharp
services.AddSqlServerProvider();                                             // ① 注册 Provider（内部 AddSqlCore）
services.AddSqlDataSource("crm", DatabaseType.SqlServer, connStr,
                          providerKey: "bing.sqlserver");                    // ② 注册具名数据源
```
多 Provider 时必须用具名数据源；`providerKey` 对照 `bing.sqlserver / bing.mysql / bing.postgresql / bing.oracle / bing.sqlite`（Doris 复用 `bing.mysql`）。

---

## 四、Web 与统一响应

### Q16 框架有 `UnifyResult` / `IResult` / `[Result]` 吗？

**正解**：**都没有**。真实机制是 `ApiResult : JsonResult`（`Bing.AspNetCore.Mvc`）+ 全局结果过滤器 `ResultHandlerAttribute`。`StatusCode` 枚举：`Ok=1`、`Fail=2`、`Unauthorized=401`。用 `ApiResult.Success(data)` / `ApiResult.Fail(...)`。

### Q17 异常响应的标识头是什么？

**正解**：常量 **`_BingErrorFormat`**（值为 `"_BingErrorFormat"`），**不是** `Bing-Error-Format`。异常由 `BingExceptionHandlingMiddleware` / `BingExceptionFilter` 捕获，经 `IExceptionToErrorInfoConverter` 生成 `RemoteServiceErrorInfo{Code,Message,...}`。

### Q18 `Bing.Permissions` 能用于非 Web 宿主吗？

**正解**：不能直接使用——`Bing.Permissions` **反向依赖 `Bing.AspNetCore`**（为集成 HTTP/授权），宿主必须含 ASP.NET Core。这是一处已知的反向耦合。

---

## 五、事件总线

### Q19 分布式事件怎么订阅？用 `[CapSubscribe]` 吗？

**正解**：**不用 `[CapSubscribe]` 装饰方法**。CAP 消费者的模型是：处理器类实现 `DotNetCore.CAP.ICapSubscribe`，方法标注 `Bing.Events.EventHandlerAttribute`（它**继承自 CAP 的 `TopicAttribute`**），由 CAP 自动发现派发。本地事件则是实现 Bing 的 `IEventHandler<T>`，由 `ISimpleEventBus` 派发——两者是并行存在的两套模型。

### Q20 事件什么时候真正发出去？

**正解**：`IEventBus.PublishAsync` 先走本地 `ISimpleEventBus`；若事件实现 `IMessageEvent`，再走 `IMessageEventBus`（CAP）——此时只是 `TransactionActionManager.Register(...)` 延迟注册，真正投递发生在工作单元提交时（`capTransaction.CommitAsync`，与业务落库同事务），即 **Outbox 事务一致性**。参考实现：`modules/admin/.../MySql/AdminUnitOfWork.cs`。

### Q21 CAP Outbox 的参考实现在哪个 Provider？

**正解**：在 **MySql** 管理模块（`modules/admin/src/Bing.Admin.Data.EFCore/UnitOfWorks/MySql/AdminUnitOfWork.cs`）。`Bing.EntityFrameworkCore.SqlServer` 工程中**没有**对应的 UoW 实现。

---

## 六、其它易混淆点

### Q22 是不是每个模块都有 `Bing.Xxx.Abstractions` 工程？

**正解**：**不是**。实际存在的只有：`Bing.Validation.Abstractions`、`Bing.Localization.Abstractions`、`Bing.Auditing.Contracts`、`Bing.MultiTenancy.Abstractions`、`Bing.AspNetCore.Abstractions`。Security / Permissions / ExceptionHandling / Caching 的抽象直接并入主工程，无独立 `*Abstractions` 包。

### Q23 有哪些「看起来应该有、实际不存在」的注册入口？

**正解**：`AddCache` / `AddCaching` / `AddCSRedis` / `AddMultiTenancy` / `UseMultiTenancy` / `AddPermissions` / `AddValidation` / `AddExceptionHandling`（只有 `UseBingExceptionHandling`）/ `AddObjectMapping` / `AddSecurity` / `AddEmailing` / `AddSerilog`（只有 `UseBingSerilogEnrichers`）/ `AddBingMvc` / `AddEventBus` / `AddAop`。完整清单见 [架构文档 §12.8](../architecture/架构文档.md)。

---

## 七、编码与工程

### Q24 中文注释 / 文档出现乱码怎么办？

**正解**：全仓库强制 UTF-8（见 `AGENTS.md`）。排查顺序：VS Code 文件编码 → PowerShell `[Console]::OutputEncoding` / `$OutputEncoding` / `chcp 65001` → 写入命令是否显式 `-Encoding utf8` → Python/Node/.NET 是否显式指定 UTF-8 → 旧文件本身是否 GBK。**不要把终端显示乱码直接等同于文件内容损坏**。批量改文件请用 Python `Path.read_text/write_text(encoding="utf-8")`。

### Q25 真实工程依赖在哪里看？`.csproj` 里没有 ProjectReference？

**正解**：依赖集中在各工程的 **`references.props` / `dependency.props`**（`.csproj` 里只有 `<Import>`）。完整依赖表见 [架构文档 §2](../architecture/架构文档.md)。
