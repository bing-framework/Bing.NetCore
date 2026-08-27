# 执行进度

- task-id: `BING-SQL-RC-HARDENING-20260826-002`
- 当前主阶段: `partial`
- 当前阶段: Phase 6 - 最终报告

## 状态

| 阶段 | 状态 | 说明 |
| --- | --- | --- |
| Phase 0 | `completed` | 环境、全方案构建、核心 net8 测试和消费者矩阵已记录；历史 FormalHost before 判定为不可复用。 |
| Phase 1 | `completed` | 扩展 mutation gateway 和多结果集异步双回调清理已实现，目标现有测试通过；直接新增回归仍随 Phase 2 补齐。 |
| Phase 2 | `completed` | 高层 API 删除、Select 语义收敛、Join options 和消费者迁移已完成。 |
| Phase 3 | `partial` | 核心单元/SQLite/部分 Provider 编译验证完成，完整 CI 矩阵未完成。 |
| Phase 4 | `partial` | IN 边界、职责拆分和 SQLite/Dapper E2E 已补齐并完成 smoke；独立 before/after provenance 仍 blocked。 |
| Phase 5 | `blocked` | 无有效 before 基线，不进行数据驱动性能优化。 |
| Phase 6 | `partial` | SDK pin、VS2022 CI 配置和 Benchmark smoke 步骤已更新；远程 CI、外部 Provider 和发布制品仍需环境确认。 |

## Phase 0 已完成

- 已读取并遵循根目录 `AGENTS.md`、`.github/copilot-instructions.md`、`.editorconfig` 和 `execute-plan` Skill。
- 已读取当前 `plan.md`，未重新规划。
- 已登记任务运行状态为 `implementing`。
- 已确认 HEAD、分支、工作树初始状态和 .NET SDK。
- 初始基线确认过高层 `FromTable`、高层 `ClearSelect`、扩展方法直接操作 Builder 和双回调交换缺口；这些问题已在本任务实现中修复。
- 全解决方案 Release 构建通过，耗时约 167.2 秒，共 192 个警告；未发现编译错误。

## Phase 0 结果

- 全解决方案 Release 构建通过：约 167.2 秒，192 warnings，0 errors。
- Data.Sql Unit net8：1249 passed，0 failed，0 skipped。
- Data.Sql Analyzer Unit net8：27 passed，0 failed，0 skipped。
- Dapper Core Unit net8：131 passed，0 failed，0 skipped。
- SQLite Unit net8：112 passed，0 failed，0 skipped。
- 已定位生产、测试、Benchmark、文档中的 FromTable/ClearSelect 消费者；未发现需要新增生产 IVT 的 Runtime SPI 消费者。
- 前序 FormalHost before/after artifact 不完整或 provenance 无效，不作为本任务 before。

## Phase 1 已完成

- 新增 internal `SqlQueryOperationAccessor.Mutate(...)`，独立查询描述的扩展 mutation 成功后统一触发一次 `Touch()`。
- From/Join/Select/Where/GroupBy/OrderBy 扩展已迁移到统一入口；普通 Builder 仍保持原有路径。
- `SqlMultipleQueryResult` 构造函数收窄为 internal；`CompleteAsync()` 开始时原子交换同步和异步回调，避免 retained delegate 和交叉重复执行。
- Data.Sql 与 Dapper Core 目标测试复跑通过。

## Phase 2 已完成

- 删除 `ISqlQuery.FromTable`、`SqlLambdaQuery.FromTable` 和高层 `SqlLambdaQuery.ClearSelect`，保留底层 Builder 的独立 `ClearSelect` 职责。
- `Select<TEntity>(bool)` 改为原子投影替换；`AppendSelect` 继续追加。
- 新增 `SqlJoinOptions`，类型化实体 Join 四类操作收敛为普通 `rightAlias` 和高级 options 两个入口。
- 迁移 Analyzer compile contract、SQLite/SQL Server 测试、Benchmark 和用户文档中的旧双 alias 调用。
- 新增 Raw Fluent 缓存失效、空白追加 no-op 回归，Data.Sql net8 通过 1251/1251。

## 命令与结果

| 命令 | 结果 | 备注 |
| --- | --- | --- |
| `git rev-parse HEAD` | `5c9bc739f944a98953da597b931a6b761c012caa` | 当前 HEAD。 |
| `git branch --show-current` | `dev_v6.0-refactor-sqlquery` | 当前分支。 |
| `git status --short --untracked-files=all` | 当前包含本任务过程文件及 Phase 1 源码修改 | 初始工作树未发现用户既有业务修改；未执行 reset/clean/checkout。 |
| `git diff --check` | 通过 | 初始工作树无已跟踪差异。 |
| `dotnet --info` | 通过 | Windows 10.0.19045，SDK 10.0.300，Runtime 8.0.27/6.0.36 可用。 |
| `dotnet --list-sdks` | 通过 | 仅发现 SDK 10.0.300。 |

## 阻断项

- 外部 Provider 真实数据库集成未配置安全测试库和 Gate，按计划记为 blocked。
- FormalHost 有效 before/after 尚未建立；前序 partial/invalid artifact 不作为本任务基线，因此 Phase 4/5 blocked。
- `appveyor.yml` 已声明 Visual Studio 2022；`global.json` 固定 SDK 10.0.300。AppVeyor 远程作业、外部 Provider 和发布制品上传仍 blocked。

## Round 5 结果

- `SqlLambdaInBenchmarks` 已覆盖 `ParameterCount=0/1/10/100/500/1000/2100`，并拆分输入创建、预构造值绑定渲染和完整构建渲染。
- 已移除重复自定义 Gen2 列，改用 BenchmarkDotNet `MemoryDiagnoser` 原生 Gen0/Gen1/Gen2 输出。
- 新增 `SqliteDapperE2EBenchmarks`，使用临时 SQLite 文件验证真实 Dapper `Query().ToList`，RowCount 为 `1/100/1000`。
- IN Dry/FormalHost smoke：42 cases，PASS；SQLite E2E Dry/FormalHost smoke：PASS，3 个 RowCount case，进程退出码 0。
- `global.json` 固定 .NET SDK `10.0.300`；AppVeyor 增加两步 Benchmark Dry smoke 与 `BenchmarkDotNet.Artifacts\\ci` 制品收集。
- 仍 blocked：无独立旧源码身份的有效 before/after、AppVeyor 远程执行和实际上传、外部 Provider 安全 Gate/连接。

## Round 6 结果

- `SqliteDapperE2EBenchmarks` 已扩展为 14 个真实 SQLite/Dapper case：`QueryToList`、`QueryToEntity`、同步/异步流式、取消、2/5/7 映射、多结果集、提前释放、基数异常、Activity、DiagnosticListener、Trace。
- `QueryToEntityCardinalityFailure` 使用 `Union All` 稳定制造多行结果，最新程序集在 `RowCount=1/100/1000` 的 `Dry + FormalHost` 共 6 个 case 中全部进程退出码 0。
- Trace 场景已注册无输出 LoggerProvider 并启用 `LogLevel.Trace`，不再只是设置最低日志级别。
- 新增独立 `SqlCiSmokeBenchmarks`，最新 smoke 只发现/执行 1 个 `Dry` case，未附带 FormalHost，生成 `BenchmarkDotNet.Artifacts/round6-ci-smoke-latest` 制品。
- Benchmark Release build：PASS，0 errors；旧版长矩阵因使用旧程序集主动停止，未将其退出码 1 计为代码失败。
- 仍 blocked：独立旧源码身份和有效 before/after、AppVeyor 远程作业与远程 artifact 上传、外部 Provider 安全 Gate/连接。

## Round 7 结果

- 抽取 `SqliteDapperE2EBenchmarkBase`，新增独立 `[DryJob]` `SqliteDapperE2ESmokeBenchmarks` 和 `--e2e-smoke` 入口，FormalHost 与 Dry smoke 不再混用类级 Job。
- GlobalSetup 在计时前验证全部 14 个 E2E 方法的预期返回值；取消、基数异常和多结果集路径错误时直接抛出验证异常，不再依赖 Benchmark 返回值静默表达失败。
- 最新程序集 E2E Dry smoke：42 个唯一 case，14 个方法 × `RowCount=1/100/1000`，仅 `Dry` Job，无 process failure；制品位于 `BenchmarkDotNet.Artifacts/round7-e2e-smoke`。
- AppVeyor 新增独立 `ci-e2e` E2E smoke 命令和制品收集路径，同时保留快速 `ci` smoke。
- 仍 blocked：独立旧源码身份和有效 before/after、AppVeyor 远程执行/上传、外部 Provider 安全 Gate/连接。

## Round 8 结果

- 复审确认 Round 7 最新程序集 42-case SQLite/Dapper E2E Dry 与 GlobalSetup 强契约验证有效；本轮重新按 AppVeyor 配置执行 CI 等价快速 smoke 和 E2E smoke。
- CI 等价快速 smoke：1 个唯一 Dry case；CI 等价 E2E smoke：42 个唯一 Dry case，14 方法 × `RowCount=1/100/1000`，无重复键和 process failure。
- `review-fix-round3-before-root`、`review-fix-round3-before-join`、`review-fix-round4-before-root` 仅有旧参数矩阵制品和日志，缺少旧源码身份、dirty diff hash、独立构建 provenance，按历史/无效 evidence 处理，不作为 before。
- Benchmark Release build、Data.Sql net8 `1261/1261`、Analyzer net8 `30/30`、`git diff --check` 通过。
- 仍 blocked：独立旧源码与有效 FormalHost before/after、AppVeyor 远程执行/制品上传、MySQL/PostgreSQL/SQL Server/Oracle/Doris 安全 Gate/连接/重置授权。
