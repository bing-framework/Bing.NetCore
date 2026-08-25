# 实施进度

## 任务
`BING-SQL-API-HARDENING-20260824-001`

## 状态
- P0-T01：COMPLETED。已确认 Windows、.NET SDK 10.0.300、MSBuild 18.6.3；工作区既有未提交修改已保留。
- P0-T02：COMPLETED。旧入口消费者已迁移，生产源码扫描仅剩合法 Dapper `connection.Query<TResult>` 和故意的负向契约。
- P1-T01：COMPLETED。Analyzer 已引用 Bing.Dapper.Core，真实 Roslyn 契约 17/17 通过。
- P1-T02：COMPLETED。生产异常语义和直接异常/原子性测试已完成，相关测试 24/24 通过。
- P2-T01：COMPLETED。非泛型 Fluent/Raw SQL 已提供 Dapper 2～7 同步/异步多映射终结。
- P2-T02：COMPLETED。旧泛型描述、Root 泛型入口和 Public API 基线残留已清理。
- P3-T01：COMPLETED。查询描述、Lambda Core、Runtime Abstractions 和 Runtime Plans 已按职责归位。
- P3-T02：COMPLETED。Executor、Builder Source、内部 Builder Accessor、Binding Controller 和 Plan/Snapshot 合同已物理拆分，命名空间保持稳定。
- P3-T03：PARTIAL。`SqlBuilderRuntimeBridge` 和 `SqlQueryBase` 的深度职责拆分尚未完成。
- P4-T01/P4-T02：COMPLETED。API 契约、Unit 矩阵和最终符号追踪已验证。
- P4-T03：COMPLETED。SQLite Unit 222/222，SQLite Integration 284/284 通过。
- P5/P6：PARTIAL。Benchmark 已覆盖并执行 20/50 Root/Join 和执行快照；完整 GetPlan、诊断组合、分页、流式、多结果集和同机前后基线尚未完成。
- P7：PARTIAL。整解隔离 Release Build 和 `git diff --check` 通过；外部 Provider Integration 未在无安全 Gate 时执行。

## 已验证消费者项目
- Data.Sql Unit：2518/2518
- Dapper Core Unit：262/262
- SQLite Unit：222/222
- SQLite Integration：284/284
- MySQL Unit：354/354
- PostgreSQL Unit：268/268
- SQL Server Unit：564/564
- Oracle Unit：180/180
- Analyzer Roslyn Contract：17/17
- EF Core Tests：已完成前阶段编译验证
- MySQL/PostgreSQL/SQL Server/Oracle Integration：受既有 Gate 和安全连接配置约束，未执行真实外部数据库

## 当前下一步
1. 后续迭代拆分 `SqlBuilderRuntimeBridge`/`SqlQueryBase` 的来源、渲染、参数、计划和诊断职责。
2. 补齐计划要求的完整 Benchmark 场景及同机旧/新基线。
3. 在受控 Provider Gate 和安全测试库可用时执行外部 Integration。
