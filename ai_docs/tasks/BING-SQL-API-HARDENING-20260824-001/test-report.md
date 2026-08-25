# 测试报告

## 环境
- OS：Windows
- .NET SDK：10.0.300
- MSBuild：18.6.3
- Unit/Integration：Debug 双 TFM，普通输出目录。
- Analyzer/Benchmark/整解：Release 或隔离输出；Analyzer 构建使用 `RunAnalyzers=false`、`NoWarn=ALL` 仅用于避免共享输出锁，不跳过 Roslyn 契约测试本身。
- Public API Analyzer：无 RS0016/RS0017 阻断错误；仍有 RS0026/RS0027 可选参数重载警告。

## 测试结果
- `Bing.Data.Sql.Tests`：2518 passed，0 failed，0 skipped。
- `Bing.Dapper.Core.Tests`：262 passed，0 failed，0 skipped。
- `Bing.Dapper.Sqlite.Tests`：222 passed，0 failed，0 skipped。
- `Bing.Dapper.Sqlite.Tests.Integration`：284 passed，0 failed，0 skipped。
- `Bing.Dapper.MySql.Tests`：354 passed，0 failed，0 skipped。
- `Bing.Dapper.PostgreSql.Tests`：268 passed，0 failed，0 skipped。
- `Bing.Dapper.SqlServer.Tests`：564 passed，0 failed，0 skipped。
- `Bing.Dapper.Oracle.Tests`：180 passed，0 failed，0 skipped。
- `Bing.Data.Sql.Analyzers.Tests.SqlOperationCompileContractTest`：17 passed，0 failed，0 skipped。

## Build/Format
- `dotnet restore .\Bing.All.sln`：通过。
- `dotnet build .\Bing.All.sln -c Release -m:1 -p:OutputPath=output/release-final-isolated/ -p:RunAnalyzers=false -p:NoWarn=ALL`：通过，230 条既有警告，无错误。
- `git diff --check`：通过。

## 未完成
- 外部 Provider Integration 未执行，原因是没有授权的安全测试数据库和 Gate 配置；不能计为通过。
- `SqlBuilderRuntimeBridge` 深度职责拆分未完成。
- Benchmark 尚未覆盖完整 GetPlan、诊断组合、分页、流式、多结果集和同机旧/新正式基线；本轮 Dry 结果仅作结构和高元数冒烟证据。
