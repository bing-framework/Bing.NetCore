<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-SQL-API-HARDENING-20260824-001
AI_EXECUTION_FINISHED_AT: 2026-08-25T08:44:04.3197490+08:00

# 实施执行报告

> 本次执行按批准计划完成核心 API 收敛、消费者迁移、Runtime 物理归位、测试和发布门禁；仍有计划内 Runtime Bridge 深度拆分、完整 Benchmark 矩阵和外部数据库 Integration 未完成，因此终态为 `PARTIAL`。

## 当前执行结论

核心查询 API 收敛已完成并通过本地验证。旧泛型 Root/描述入口未恢复；非泛型 Fluent/Raw 2～7 多映射、SQLite 真实执行、Provider Unit、Analyzer 契约、Public API 基线和 Runtime 文件归位均已验证。外部数据库和部分计划性能场景仍受安全 Gate 或工作量约束，未伪装为通过。

## 已完成事项

- 修复 Analyzer 动态编译项目对 `Bing.Dapper.Core` 的项目引用和 MetadataReference。
- 在非泛型 Fluent/Raw SQL 描述中保留 Dapper 2～7 同步/异步多映射终结。
- 删除 Advanced 泛型扩展和泛型查询描述生产文件。
- 迁移 SQLite Unit/Integration、EF Core、MySQL Integration、PostgreSQL Integration、SQL Server Unit/Integration 的主要旧消费者。
- 将目标查询链中的已知不支持路径改为 `NotSupportedException`。
- 补充默认条件、未知 Operator 和 Provider TypeConverter 的直接异常/原子性测试。
- 修复 Roslyn 动态编译对 Dapper Core 发货程序集的引用，Analyzer 契约 17/17 通过。
- 清理 Data.Sql、Dapper Core 的 Public API Shipped/Unshipped 旧符号，保留最终非泛型多映射和子查询声明。
- 将 `SqlMultiLambdaQuery.cs`、`SqlLambdaQuery.NonGeneric.cs`、`SqlFluentQuery.NonGeneric.cs`、`SqlTextQuery.NonGeneric.cs` 归位为实际类型文件；将 Runtime Executor、Builder Source、Accessor、Binding、Plan/Snapshot 合同归入 `Runtime/Abstractions` 和 `Runtime/Plans`。
- 扩充 Lambda Benchmark Root/Join 到 20/50，并执行 Dry Job；同步更新迁移、使用说明、发行说明和追踪矩阵。
- 创建并维护 `progress.md`、`api-migration.md`、`test-report.md`、`benchmark-report.md`、`review-report.md`、`final-summary.md` 和 `source-generator-decision.md`。

## 测试和编译证据

- `Bing.Data.Sql.Tests`：2518/2518 passed。
- `Bing.Dapper.Core.Tests`：262/262 passed。
- `Bing.Dapper.Sqlite.Tests`：222/222 passed。
- `Bing.Dapper.Sqlite.Tests.Integration`：284/284 passed。
- `Bing.Dapper.MySql.Tests`：354/354 passed。
- `Bing.Dapper.PostgreSql.Tests`：268/268 passed。
- `Bing.Dapper.SqlServer.Tests`：564/564 passed。
- `Bing.Dapper.Oracle.Tests`：180/180 passed。
- `Bing.Data.Sql.Analyzers.Tests.SqlOperationCompileContractTest`：17/17 passed。
- 条件异常和原子性定向测试：双 TFM 24/24 passed。
- `dotnet restore .\Bing.All.sln`：通过。
- `dotnet build .\Bing.All.sln -c Release -m:1 -p:OutputPath=output/release-final-isolated/ -p:RunAnalyzers=false -p:NoWarn=ALL`：通过，230 条既有警告，无错误。
- `git diff --check`：通过。
- Benchmark Dry：126 个 case 执行成功，覆盖 Root/Join 1、2、5、10、20、50；Root/Join 报告已生成。

已知警告包括 `NETSDK1206`、.NET 6 EOL、依赖包和可选参数重载规则；未发现本轮新增编译错误。

## 部分/未完成事项

- `SqlBuilderRuntimeBridge`/`SqlQueryBase` 按来源、渲染、参数、计划和诊断的深度职责拆分未完成。
- Benchmark 尚未覆盖完整 GetPlan、诊断组合、分页 Count/Data、同步/异步流、多结果集和同机旧/新正式基线；本轮 Dry 结果不作为性能回归结论。
- MySQL、PostgreSQL、SQL Server、Oracle Integration 未执行真实外部数据库；原因是没有授权的安全测试库和 Gate 配置。
- `SqlQueryLifecycleTest`、`SqliteExecutionIntegrationTest` 尚未按 P3-T01 的职责进一步物理拆分；现有测试行为已通过。

## 操作边界

未执行 `git add`、`git commit`、`git push`、reset、clean 或 PR 操作。

## 修改文件范围

- 查询 API：`framework/src/Bing.Dapper.Core`、`framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries`、`framework/src/Bing.Data.Sql/Bing/Data/Sql/Extensions`、Public API 基线。
- Runtime：`framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Abstractions`、`Runtime/Plans`，保持 `Bing.Data.Sql` 命名空间不变。
- 测试：Analyzer、Data.Sql、Dapper Core、MySQL/PostgreSQL/SQL Server/Oracle/SQLite Unit 和 SQLite Integration 消费者。
- 文档：README/SQL 查询使用文档、发行说明、SQL Metadata 追踪和本任务报告。

## API/配置变化

- 删除 `SqlAdvancedQueryExtensions`、Root 泛型 `Query<TResult>`/`Sql<TResult>`/`Procedure<TResult>` 及泛型查询描述。
- 结果类型移动到终结方法；Dapper 2～7 映射保留在非泛型 Fluent/Raw 描述。
- `SqlSubquery<TProjection>` 保留；Runtime SPI 仅保持 `EditorBrowsable(Never)` 公共跨程序集合同和 internal Builder 访问器。
- 未新增生产 `InternalsVisibleTo`，未连接生产数据库，未提交凭据或连接字符串。

## 风险与 Reviewer 注意事项

- 本任务是计划内主版本 Breaking Change，调用方需迁移到结果后置终结语法。
- RS0026/RS0027 可选参数重载警告仍需后续 API 设计评估，当前未通过关闭规则掩盖。
- 工作区原有大量未提交修改和输出锁定进程仍存在；本次未恢复、清理或终止用户进程。

## Git 状态

工作区保持未提交状态。未自动执行 `git add`、`git commit`、`git push`、reset、clean 或创建 PR。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`
- Review 文件：`ai_docs/tasks/BING-SQL-API-HARDENING-20260824-001/review.md`
- 修复模式：`review-fix`

#### FIX-001

- 严重程度：`BLOCKER`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 对应计划项：P2-T01、P3-T01、P4-T01、P7-T01、P7-T03
- 修改文件：
	- 删除 `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlTextQuery.NonGeneric.cs`
- 根因：归位后的 `SqlTextQuery.cs` 已包含完整非泛型实现，但旧 `.NonGeneric.cs` 文件仍保留同一完整类，SDK 默认 Compile glob 将两份文件同时编译。
- 修复：删除重复旧实现，只保留文件名与类型一致的 `SqlTextQuery.cs`；未通过项目文件排除规则掩盖重复源文件。
- 验证：
	- `dotnet build .\\framework\\src\\Bing.Data.Sql\\Bing.Data.Sql.csproj -c Release -nologo -v quiet`：PASS
	- `dotnet build .\\framework\\src\\Bing.Dapper.Core\\Bing.Dapper.Core.csproj -c Release -nologo -v quiet`：PASS
	- `dotnet test .\\framework\\tests\\Bing.Data.Sql.Analyzers.Tests\\Bing.Data.Sql.Analyzers.Tests.csproj -c Release -nologo -v minimal`：27/27 PASS
	- `dotnet test .\\framework\\tests\\Bing.Data.Sql.Tests\\Bing.Data.Sql.Tests.csproj -c Release -nologo -v minimal`：2518/2518 PASS
	- `dotnet test .\\framework\\tests\\Bing.Dapper.Sqlite.Tests\\Bing.Dapper.Sqlite.Tests.csproj -c Release -nologo -v minimal`：222/222 PASS（1 个 NETSDK1206 警告）
	- `dotnet test .\\framework\\tests\\Bing.Dapper.Sqlite.Tests.Integration\\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -nologo -v minimal`：284/284 PASS（1 个 NETSDK1206 警告）

#### FIX-002

- 严重程度：`HIGH`
- 处理要求：`MUST_FIX`
- 执行状态：`COMPLETED`
- 对应计划项：P0-T01、P3-T01、P3-T02、P7-T01
- 修改文件：
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQuery.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlLambdaQueryCore.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Abstractions/ISqlQueryBuilderAccessor.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Abstractions/ISqlQueryBuilderSource.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Abstractions/ISqlQueryPlanExecutor.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Abstractions/ISqlQueryPlanExecutor.PagingStreaming.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Abstractions/ISqlQueryRuntimeBindingController.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Plans/ISqlOutputParameterAccessor.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Plans/SqlBuilderExecutionSnapshot.cs`
	- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Runtime/Plans/SqlQueryPlan.cs`
	- 本任务既有未跟踪执行文档和迁移文档
- 根因：文件归位通过工作区文件系统完成，但这些新增路径尚未进入 Git 索引，因此普通 `git diff` 只显示旧路径删除，不能完整重建当前工作区实现。
- 修复：逐项核对 10 个关键生产新增文件，确认全部存在、非空；确认列出的旧路径全部不存在且新路径全部存在；确认源码中只有一个 `SqlTextQuery` 定义。按照操作边界未执行 `git add`，不伪造暂存或提交状态；交付清单已记录在本执行报告中。
- 验证：
	- 生产新增文件清单：10/10 存在且非空，PASS
	- 关键旧/新移动路径配对：5/5 旧路径删除且新路径存在，PASS
	- `SqlTextQuery` 定义搜索：1 个定义，PASS
	- `git diff --check`：PASS
	- Data.Sql、Dapper Core、Analyzer、Data.Sql Unit、SQLite Unit/Integration 回归：PASS，结果见 FIX-001

### Round 1 汇总

- MUST_FIX：2
- 已完成：FIX-001、FIX-002
- PARTIAL：原计划中 Runtime Bridge 深度拆分、完整 Benchmark 矩阵、外部 Provider Integration 仍按原执行报告保持未完成；本轮未扩大范围。
- BLOCKED：无
- FAILED：无
- 回归验证：核心项目默认 Release 构建和要求的 Analyzer/Unit/SQLite Integration 均通过；保留 NETSDK1206 既有警告。
- 下一步：执行任务终态收口，随后进行独立 Review 复审。
