# 升级与迁移指南

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../ReleaseNotes.md)

> 配套文档：[架构文档](../architecture/架构文档.md) ｜ [使用文档](../getting-started/使用文档.md) ｜ [FAQ 与排错](../getting-started/FAQ与排错.md) ｜ [发行说明](../ReleaseNotes.md)

## 0. 先读这一段（范围声明）

- 本仓库是**自研框架仓库，不保证跨版本 API 兼容**。主版本升级（如 6.x → 7.0.0）**可能包含破坏性变更**，且**不提供 `[Obsolete]` 包装层**。
- 本文**只聚合已经确认并记录在案的变更**，是"已知破坏性变更的枢纽 + 升级检查清单"，**不是**逐 API 的 diff 报告。
- 本文**未**做 git 历史挖掘，也不推断未记录的行为变化。若你在升级中遇到本文没写的问题，请补充进本文（见 §6）。
- 单项迁移的详细步骤写在各自的迁移文档里，本文只做索引。

| 迁移文档 | 适用升级 | 内容 |
| --- | --- | --- |
| [SQL 事务 API 迁移到 7.0.0](sql-transaction-api-vNext.md) | → 7.0.0 | Dapper 连接 / 事务公开 API 收敛 |

---

## 1. 已知破坏性变更：7.0.0

完整列表见 [发行说明 §7.0.0](../ReleaseNotes.md)。以下为需要你动手改的部分，按影响面排序。

### 1.1 Dapper 连接与事务 API 收敛（影响最大）

`ISqlTransactionScope` 成为**唯一**的公开事务生命周期对象；Dapper 自有连接统一通过 `ISqlDbConnectionFactoryResolver` 创建。

**已删除的 API**：

- `IDbConnectionManager`、`IDbTransactionManager`
- `ISqlQueryExternalContext`
- `ISqlQuery` 及实现上的 `GetConnection` / `SetConnection` / `GetTransaction` / `SetTransaction`
- `BeginTransaction` / `CommitTransaction` / `RollbackTransaction`
- `IDatabaseFactory` 与各 Provider 的 `XxxDatabaseFactory`（MySql / PostgreSql / SqlServer / Oracle / Sqlite）
- Provider Query / Executor 基类上的 `IDatabase` 构造参数与 `CreateDatabase` / `CreateDatabaseFactory` 扩展点

**改法（事务）**：

```csharp
// 旧
query.BeginTransaction();
executor.ExecuteSql(sql, parameters);
query.CommitTransaction();

// 新
using var scope = transactionScopeFactory.Begin("reporting");
var executor = scope.CreateExecutor();
executor.ExecuteSql(sql, parameters);
scope.Commit();
```

异步用 `BeginAsync` / `CommitAsync` / `await using`。**未完成的 Scope 在释放时自动回滚。**

**改法（连接）**：应用代码不再注入 / 替换 / 读取 `IDbConnection`，改为声明数据源：

```csharp
services.AddSqlCore();
services.AddSqliteProvider();
services.AddSqlDataSource("reporting", DatabaseType.Sqlite, connectionString);
```

然后通过 `ISqlQueryFactory.Create<TQuery>("reporting")` 或 `ISqlTransactionScopeFactory.Begin("reporting")` 选择数据源。

**跨 ORM 边界**：`IDatabaseConnectionAccessor` 与 `IDatabase` **未删除**，继续服务 EF Core / FreeSQL 的跨 ORM 集成；只是 Dapper 不再解析或包装它们。

详细步骤见 [SQL 事务 API 迁移到 7.0.0](sql-transaction-api-vNext.md)。

### 1.2 SQL 查询 Fluent API 收敛

同为 7.0.0 的主版本 Breaking Change，**不新增 `[Obsolete]` 包装层**。要点：

| 变更 | 旧写法 | 新写法 |
| --- | --- | --- |
| 起始阶段泛型结果 | `Query<TResult>()` / `Sql<TResult>()` 等 | 非泛型描述 `Query()` / `Sql()` / `SqlInterpolated()` / `Procedure()` + 终结方法 `ToEntity<TResult>` / `ToList<TResult>` / 分页 / 标量 / 流式 |
| 结果类型转换 | `As<TResult>()` | `Select<TProjection>`（强类型投影） |
| 字符串表来源 | 高层 `FromTable` | `Query().From(string, alias)` |
| 清空选择列 | `ClearSelect()` | 直接使用 `Select(...)` |
| 类型化 Join | `Join` / `LeftJoin` / `RightJoin` / `FullJoin` 带左侧 alias | 只传右侧 alias；左侧 alias 与 schema 走 `SqlOptions` / `SqlJoinOptions` |
| `ISqlQuery.From` | 泛型 `From<TEntity>(...)` 多重载 | 只保留非泛型 `From<TEntity>(alias = null, schema = null)` |
| `WhereIf` | 旧顺序 | **统一条件优先**；条件组用明确的 `AndGroup` / `OrGroup` |
| 高层字典 | `ToDictionary` | `ToList<TResult>().ToDictionary(...)` |
| `SingleOrDefault`（高层） | 存在 | 已删除（与 `ToEntity` 重复） |

另外两处容易漏：

- **`Builders.Internal.Helper`、`JoinItem.SetDependency(Helper)`、`JoinItem.Clone(Helper)` 已内部化**，第三方扩展必须改用公开 Fluent API 或已声明的 Provider SPI，**没有兼容 facade**。
- **可选参数签名被拆分**：为治理 RS0026，`SqlTextQuery` / `SqlFluentQuery` / `SqlLambdaQuery` 及 Fluent 扩展中受影响的可选参数签名拆成了"显式短重载 + 完整参数重载"。**依赖反射 / 动态调用 / 源生成器的调用方不能再假定 `ParameterInfo.HasDefaultValue` 或默认参数元数据存在**，必须按实际重载选择调用路径。

---

## 2. 目标框架与 SDK 事实

升级前先对齐构建环境。以下均取自仓库当前配置：

| 项 | 值 | 位置 |
| --- | --- | --- |
| 版本号 | `7.0.0`（`VersionQuality` = `20250319-1`） | `version.props` |
| 类库默认 TFM | **`netstandard2.0`**（`netstandard2.1` 已注释掉） | `framework.props` |
| 测试工程 TFM | `net8.0;net6.0` | `framework.tests.props` |
| 构建 SDK | **8.0.424**（`rollForward` 依 `global.json`） | `global.json` |
| Web / 上层工程 | 多为 `net6.0`（如 `Bing.AspNetCore.Mvc.UI`、`Bing.Extensions.SkyApm.Diagnostics.Sql`） | 各工程 `.csproj` |
| 多目标工程 | 目前仅少数（如 `Bing.AspNetCore.Abstractions`）；**不要假定所有包都能被 `netstandard2.0` 消费者引用** | 各工程 `.csproj` |

**升级动作**：

1. `dotnet --version` 确认 SDK 与 `global.json` 一致（不一致时升级会有莫名的编译差异）；
2. 若你的应用是 `net6.0`，引用 `netstandard2.0` 类库没问题，但引用 `net6.0` 的包（如 `Bing.AspNetCore.Mvc.UI`）就必须自身 ≥ `net6.0`；
3. 若你的应用要跑 `net8.0`，测试工程已经在 `net8.0;net6.0` 双目标验证过，可放心。

---

## 3. 注册入口重命名与陷阱速查

升级后最常见的编译错误不是 API 删除，而是**入口名字对不上**。完整清单见 [架构文档 §12](../architecture/架构文档.md)（含命名空间与重载签名）与 [使用文档 §3.3](../getting-started/使用文档.md)（精简版）。

### 3.1 名字与直觉不符的入口

| 你想要 | 实际入口 | 说明 |
| --- | --- | --- |
| 启用 AOP | **`EnableAop(...)`** | 没有 `AddAop` |
| EF Core + PostgreSQL | **`AddPgSqlUnitOfWork<,>(...)`** | 不是 `AddPostgreSqlUnitOfWork` |
| Serilog 集成 | **`UseBingSerilogEnrichers(app)`** | 没有 `AddSerilog` / `AddBingSerilog` |
| 异常处理 | **`UseBingExceptionHandling()`** | 没有 `AddExceptionHandling` |
| Exceptionless 日志 | **`WriteTo.Exceptionless(...)`** | 挂在 Serilog 的 `LoggerSinkConfiguration`，**不在 `IServiceCollection`** |
| SQL 链路追踪（SkyAPM） | **`AddSqlQuery()`** | 挂在 `SkyApmExtensions`，**不在 `IServiceCollection`** |
| 注册模块 | **`IBingBuilder.AddModule<T>()`** | 不是 `IServiceCollection.AddModule<T>()` |

### 3.2 同名但不同方法（最容易踩）

| 名字 | 两套实现 | 区分点 |
| --- | --- | --- |
| `AddMySqlUnitOfWork<,>` | EF Core 版 & FreeSQL 版 | 命名空间不同：`Bing.Datas.EntityFramework.MySql` vs **`Bing.FreeSQL`**；参数签名也不同 |

### 3.3 根本没有内置入口的

| 能力 | 现状 |
| --- | --- |
| `ICache` | **无注册扩展**，需 `RedisHelper.Initialization(...)` 后手工 `services.AddScoped<ICache, CSRedisCacheManager>()` |
| `AddCache` / `AddCaching` / `AddCSRedis` / `AddFreeRedis` | **都不存在**。`Bing.EasyCaching` 的 `AddCaching` 整段被注释且 `CachingOptions` 是 `internal` |
| `AddMultiTenancy` / `UseMultiTenancy` / `AddPermissions` / `AddValidation` / `AddObjectMapping` / `AddSecurity` / `AddEmailing` / `AddBingMvc` / `AddEventBus` | **都不存在**，见 [架构文档 §12.8](../architecture/架构文档.md) 防误用清单 |

### 3.4 组件库入口的形态差异

`Bing.Biz.Payments` 的 `AddPay` 系列**返回 `void`，不能链式调用**；且这两个包位于 `components/src`（不在 `framework/src`），不在 docfx API 元数据范围内。详见 [组件库文档](../guides/组件库文档.md)。

---

## 4. 覆盖边界（明确声明）

| 事项 | 现状 |
| --- | --- |
| PublicAPI 基线 | **只有 7 个工程**维护了 `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`：`Bing.Data.Sql`、`Bing.Dapper.Core`、`Bing.Dapper.MySql`、`Bing.Dapper.Oracle`、`Bing.Dapper.PostgreSql`、`Bing.Dapper.SqlServer`、`Bing.Dapper.Sqlite`。**其余工程的公开 API 变化不会被 RS0016/RS0017 分析器拦截** |
| 逐 API diff | **本文不做**。请以 [发行说明](../ReleaseNotes.md) 与 PublicAPI 基线文件为准 |
| git 历史挖掘 | **本文未做**。已知变更以文档与源码现状为准 |
| 测试数字 | 历史验证记录中的测试数量**不构成当前 commit 的验收证据**，以任务报告 / TRX / JSON 与对应源 HEAD 为准 |
| docfx API 站点 | 元数据范围只含 `framework/src`，`components/src` 不在内（详见 [API 速查索引](../getting-started/API速查索引.md)） |

---

## 5. 验收清单

沿用 [SQL 事务迁移文档](sql-transaction-api-vNext.md) 的 `## 验收` 约定。升级完成后逐项确认：

**编译期**

- [ ] 业务代码不再引用 §1.1 / §1.2 中已删除的类型与方法（`IDbConnectionManager`、`IDatabaseFactory`、`XxxDatabaseFactory`、`As<T>()`、高层 `ToDictionary`、`SingleOrDefault`、`FromTable`、`ClearSelect()` 等）。
- [ ] 没有依赖 `ParameterInfo.HasDefaultValue` 或默认参数元数据的反射 / 动态调用 / 源生成器路径。
- [ ] SDK 版本与 `global.json` 一致；目标框架与所引包的 TFM 兼容（见 §2）。

**运行期（Dapper / SQL）**

- [ ] 所有需要原子性的 Dapper 操作都位于一个 `ISqlTransactionScope` 内，且成对出现 `Commit()` / 自动回滚。
- [ ] 多数据源场景通过 `dbKey`、数据源元数据与 Scope 选择目标数据库，而不是自己管 `IDbConnection`。
- [ ] Dapper 测试替身通过 `ISqlDbConnectionFactoryResolver` 提供自有连接，**不是**实现 `IDatabase`。
- [ ] `ToSql()` 之后的结构变更会正确失效缓存（Raw Fluent 扩展已统一经过 mutation gateway）。

**运行期（框架装配）**

- [ ] 模块注册用的是 `IBingBuilder.AddModule<T>()`，且 `UseBing()`（Web）或 `serviceProvider.UseBing()`（非 Web）已调用。
- [ ] 缓存若使用，是手工注册的（框架没有 `AddCache`，见 §3.3）。
- [ ] 升级后跑一遍 `Bing.Data.Sql` 相关单元测试 + SQLite 本地集成测试。

> 外部数据库（SQL Server / MySQL / PostgreSQL / Oracle）集成测试受 Gate 控制，需按项目配置启用**受控测试库**，不得指向生产库。参见 [数据库集成测试](../testing/integration-testing.md)。

---

## 6. 补充本文

如果你在升级中踩到了本文没记录的坑，请按下面顺序处理：

1. 先确认是不是已在 [FAQ 与排错](../getting-started/FAQ与排错.md) 里（那里有 25 条常见坑）；
2. 若是**破坏性变更**，补进本文 §1，并在 [发行说明](../ReleaseNotes.md) 对应版本下记一笔；
3. 若是**入口名字/用法问题**，补进 §3，并同步 [架构文档 §12](../architecture/架构文档.md) 与 [使用文档 §3.3](../getting-started/使用文档.md)；
4. 若影响面较大，单独建一份 `docs/migrations/<主题>.md`，并在本文 §0 的表格里加一行索引。
