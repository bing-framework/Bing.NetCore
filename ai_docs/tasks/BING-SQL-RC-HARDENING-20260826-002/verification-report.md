# 验证报告

- task-id: `BING-SQL-RC-HARDENING-20260826-002`
- 状态: `partial`

## 当前结论

本任务完成了 Phase 1 核心正确性修复、Phase 2 API 收敛、Round 4 Join schema 合同和 Round 5 Benchmark/SDK pin 修正。外部 Provider、有效 Root/Join/IN before/after 性能比较和 AppVeyor 远程执行仍缺少可审计证据，整体结论保持 `partial`。

## 已知源码风险

- 完整 mutation family 的直接回归矩阵仍未覆盖所有扩展分支；已覆盖 Raw Fluent 缓存失效、空白 no-op、WhereIf(false) 和代表性失败原子性。
- 多结果集公开 Dispose/DisposeAsync 链、reader/callback/lease exactly-once、retained delegate WeakReference 和主异常优先聚合已有直接职责测试；SQLite 多结果集异常/取消矩阵仍需后续补齐。
- Root/Join FormalHost before/after 需要在独立旧版源码身份下重新建立，前序 partial/invalid artifact 不可复用；本轮 Raw 20/50 已有独立有效 smoke/FormalHost 结果，但不构成 before/after 比较。
- Round 5 IN 已覆盖 `0/1/10/100/500/1000/2100` 并拆分 values 创建、预构造绑定渲染和完整构建渲染；SQLite/Dapper E2E 已新增临时文件基准，但仍只有当前版本结果。

## 测试结果

- 全解决方案 Release 构建：通过，约 167.2 秒，192 个警告，0 个错误。
- Data.Sql net8：1261 passed，0 failed，0 skipped。
- Data.Sql net6：1261 passed，0 failed，0 skipped。
- Data.Sql Analyzer net8：30 passed，0 failed，0 skipped。
- Dapper Core net8：134 passed，0 failed，0 skipped。
- Dapper Core net6：134 passed，0 failed，0 skipped。
- SQLite Unit net8：112 passed，0 failed，0 skipped。
- SQLite Integration net8/net6：302 passed，0 failed，0 skipped。
- `RawFluent_WhenParametersAreClearedAfterToSql_ShouldInvalidateCachedSqlAndSnapshot` net8/net6：通过；直接断言清空参数后的完整 SQL 与 `SqlBuilderRuntimeBridge.CreateExecutionSnapshot` 输出一致，execution snapshot 参数集合为空。
- Data.Sql Benchmarks Release build：通过，0 errors；有 `NETSDK1206` RID 兼容性警告。
- IN Benchmark Dry/FormalHost smoke：通过，42 cases，覆盖 `0/1/10/100/500/1000/2100` 和三个职责方法。
- SQLite/Dapper E2E Benchmark Dry/FormalHost smoke：通过，`RowCount=1/100/1000`，真实 `Query().ToList` 执行，进程退出码 0。
- `global.json`：固定 .NET SDK `10.0.300`，本地 SDK 版本匹配。
- Raw 20/50 Benchmark FormalHost smoke：通过；20 来源 Mean 约 `3.336 us`，50 来源 Mean 约 `5.083 us`，每个 case `45` 个样本，生成 CSV/Markdown/HTML。
- AppVeyor 配置：已更新为 Visual Studio 2022，固定 SDK 通过 `global.json`，配置 TRX、Cobertura、PublicAPI 和两步 Benchmark Dry artifact 收集；未在本地执行远程 AppVeyor。
- `grep` 复核：当前源码/测试/文档无旧 Join 命名参数、生产高层 `FromTable` 或高层 `ClearSelect` 残留；底层 Builder `ClearSelect` 按决策保留。

## Round 6 增量验证

- Benchmark Release build：通过，0 errors。
- 最新 `SqlCiSmokeBenchmarks`：通过；只发现/执行 `BuildRawQuery` 1 个 `Dry` case，进程退出码 0，生成 `BenchmarkDotNet.Artifacts/round6-ci-smoke-latest` CSV/Markdown/HTML。
- 最新 `SqliteDapperE2EBenchmarks.QueryToEntityCardinalityFailure`：`RowCount=1/100/1000`、`Dry + FormalHost` 共 6 个 case 通过；进程退出码均为 0，异常计数为预期路径，未出现 NA 或 process failure。
- SQLite E2E 代表矩阵源码已补齐 14 个 case，包含终结、流式、映射、多结果集、诊断、取消、异常和 Dispose；完整矩阵的旧程序集长跑在部分完成后主动终止，不能替代最新程序集的完整报告。
- Trace 路径已由无输出 LoggerProvider 实际启用 `LogLevel.Trace`；DiagnosticListener 和 Activity 路径保持独立 case。
- CI smoke 只执行 Dry：通过独立 `[DryJob]` 类型和 `--ci-smoke` 显式入口验证；AppVeyor 远程执行/上传仍 blocked。
- 性能结论继续为 blocked：没有独立旧源码身份和同 Job 完整 before/after，不声明性能 delta 或 RC 准入。

## Round 8 增量验证

- AppVeyor 等价快速 smoke：通过，1 个 `SqlCiSmokeBenchmarks.BuildRawQuery` Dry case。
- AppVeyor 等价 E2E smoke：通过，42 个唯一 Dry case，14 个方法 × `RowCount=1/100/1000`；CSV 结构化核对无重复键，未发现 process failure。
- Benchmark Release build：通过，0 errors；Data.Sql net8：1261 passed；Analyzer net8：30 passed；`git diff --check`：通过，仅有换行转换提示。
- 现有 `review-fix-round3-before-*` 目录的旧 Root/Join 报告与日志已核查：参数矩阵分别为旧 Root 72 case、旧 Join 36 case，缺少源码身份和 dirty diff hash，不能作为当前调整后矩阵的 before。
- 当前进程环境未发现 `RUN_*_INTEGRATION_TESTS`、`ConnectionStrings__*` 或 `ALLOW_DATABASE_RESET_FOR_TESTS`；MySQL/PostgreSQL/SQL Server/Oracle/Doris 继续 blocked。
- AppVeyor 远程 job、远程日志和可下载制品仍 blocked；本地等价命令不能替代远程证据。
- 性能结论继续为 blocked：没有独立旧源码身份和同 Job 完整 before/after，不声明性能 delta 或 RC 准入。

## Round 7 增量验证

- Benchmark Release build：通过，0 errors。
- 最新 `SqliteDapperE2ESmokeBenchmarks`：42 个唯一 Dry case，覆盖 14 个方法与 `RowCount=1/100/1000`；CSV 结构化核对无重复键，未发现 process failure。
- GlobalSetup 契约验证覆盖 Query/ToEntity/ToList、同步/异步流、取消、2/5/7 映射、多结果集、提前释放、基数异常、Activity、DiagnosticListener 和 Trace；错误返回会在计时前抛出验证异常。
- 最新 E2E 制品：`BenchmarkDotNet.Artifacts/round7-e2e-smoke`，CSV/Markdown/HTML 已生成并记录 hash。
- `SqlCiSmokeBenchmarks` 快速 Dry smoke 保持通过且仅执行一个 case；AppVeyor 已增加 E2E smoke 命令和 `ci-e2e` 制品路径。
- Analyzer/Data.Sql、AppVeyor 静态诊断和 `git diff --check`：通过；AppVeyor 远程执行与外部 Provider 真实集成继续 blocked。
- 性能结论继续为 blocked：没有独立旧源码身份和同 Job 完整 before/after，不声明性能 delta 或 RC 准入。

## Blocked

- 外部 Provider 真实数据库集成：`blocked`，未配置安全连接与 Gate。
- FormalHost 有效 before/after：`blocked`，缺少独立源码身份和完整同 Job 结果。
- AppVeyor 远程执行与 artifact 实际上传：`blocked`，当前环境无法执行 AppVeyor 远程作业。
- AppVeyor Benchmark artifact 生成：本地命令已通过，目标远程作业上传仍 `blocked`。
- MySQL/PostgreSQL/SQL Server/Oracle/Doris 真实集成：`blocked`，缺少安全连接和对应 `RUN_*_INTEGRATION_TESTS=true` Gate。
