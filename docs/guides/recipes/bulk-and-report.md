# 配方 6：批量写入与跨库报表

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

> 目标：解决 ORM 支干不了的活——**上万条写入**、**跨库 Join**、**存储过程**。
>
> 用的是 `Bing.Data.Sql + Dapper` 支。⚠ 它**没有 `IUnitOfWork` / `IStore` / 审计 / 乐观锁**，是 SQL 构建器 + 执行器，别当 ORM 用。

## 1. 装包

```bash
dotnet add package Bing.Data.Sql
dotnet add package Bing.Dapper.MySql          # 换成你的库：SqlServer / PostgreSql / Oracle / Sqlite
dotnet add package Bing.Data.Sql.Analyzers    # 建议装：BINGSQL002 注入检查
```

## 2. 注册

```csharp
using Bing.Dapper.MySql;

services.AddMySqlProvider();
services.AddSqlDataSource(
    key: "main",
    databaseType: DatabaseType.MySql,
    connectionString: "...");
```

> 这一支的入口是 `AddXxxProvider()` + `AddSqlDataSource(...)`，**没有 `AddXxxUnitOfWork`**。
> 各库入口：`AddSqlServerProvider` / `AddMySqlProvider` / `AddPostgreSqlProvider` / `AddOracleProvider` / `AddSqliteProvider`。

## 3. 批量写入（务必分批）

```csharp
await executor.InsertBatchAsync(entities, new SqlBatchInsertOptions
{
    BatchSize = 200,          // ⚠ 别用默认值硬扛大集合
    UseTransaction = true
});
```

**为什么必须分批**——实测（net8.0 / BDN 0.14.0）：

| 批量规模 | 构建耗时 | 内存分配 |
| --- | --- | --- |
| **1000 实体一次性构建组合 Insert** | **127 ms** | **463 MB**（Gen2 20,333 次） |

这个开销是**非线性**的，还会引发 Gen2 GC。详见 [性能指南 §3.5](../../operations/性能指南.md)。

## 4. 跨库查询

```csharp
var list = await query
    .From<Order>("main.orders")                 // 库名.表名
    .Join<User>("crm.users", (o, u) => o.UserId == u.Id)
    .Where<Order>(o => o.Status == 1)
    .ToListAsync<UserOrderDto>();
```

框架内置 `ISqlCrossDatabaseQueryValidator`（`AddSqlCore` 默认注册）会**逐个 join 校验**跨库合法性。

## 5. 原生 SQL（必须参数化）

```csharp
// ❌ 会触发 BINGSQL002
ExecuteSql($"SELECT * FROM t WHERE name = '{name}'");

// ✅ 分析器认可
SqlInterpolated($"SELECT * FROM t WHERE name = {name}");

// ✅ 参数对象也可以
ExecuteSql("SELECT * FROM t WHERE name = @name", new { name });
```

> ⚠ `BINGSQL002` 是 **Warning** 不是 Error，构建默认不会失败——建议在 CI 里单独提级。

## 6. 存储过程

```csharp
var result = await query.Procedure("sp_daily_report")
    .AddParameter("date", DateTime.Today)
    .ToListAsync<ReportRow>();

// 或
await executor.ExecuteProcedure("sp_close_day", parameters);
// 输出参数通过 ISqlOutputParameterAccessor 取回
```

> 各 Provider 的存储过程能力由 `SqlProviderProcedureCapabilities` 声明；数据库支持但 Bing 未实现时会**运行时降级**（`SqlCapabilityFailure` 有专门一档），不是编译报错。

## 7. 已知限制（别踩）

1. Lambda 多源 `From` 不支持 → `NotSupportedException`
2. 不支持 `Returning` 子句；`Mutation Set` 不支持「来源列赋值」
3. 含 **CTE 的 `Union` / `Group` / `Distinct`** 查询**不支持自动分页计数**——需预先设置 `TotalCount`
4. **原生 SQL 不支持自动分页**
5. 不支持多表 DTO 投影绑定
6. **Doris 数据源强制只读**（`IsReadOnly = true; SupportsTransactions = false`）
7. **SQLite** 不支持库名/架构名限定，只能写单段对象名

## 8. 与 ORM 混用

「EF Core 管写入 + Bing.Data.Sql 管报表」是常见组合，但要注意：

- **不共享事务**：两条独立链路，跨支一致性靠 CAP 事件最终一致（见 [配方 2](cap-event-outbox.md)）
- **不共享实体映射**：EF 用 `IEntityTypeConfiguration`，本支用 `IEntityMappingResolver`，改列名要同步两处

## 注意事项

1. 🔴 **没有 `IUnitOfWork` / `IStore`**：需要领域层能力请用 EF Core / FreeSQL 支。
2. **批量一定分批**，参考上面实测数字。
3. **高频查询复用已构建的 Builder**：实测重复渲染 `113 ns` vs 首次构建渲染 `6,739 ns`（约 **60 倍**）。
4. 接入 **Doris** 用 `bing.mysql` 这个 provider key（不是第 6 个 Provider），且会被强制只读。

## 相关

- [能力矩阵 §5](../../getting-started/能力矩阵.md)
- [性能指南](../../operations/性能指南.md)（实测数字与优化清单）
- [子系统深挖](../../architecture/子系统深挖.md)（`Bing.Data.Sql` 路由链）
