# Benchmark 基线

- task-id: `BING-SQL-RC-HARDENING-20260826-002`
- 状态: `blocked`

## 源码身份

- HEAD: `5c9bc739f944a98953da597b931a6b761c012caa`
- Branch: `dev_v6.0-refactor-sqlquery`
- 初始工作树: 仅本任务 `plan.md` 未跟踪；无已跟踪 dirty diff。
- SDK: `.NET SDK 10.0.300`
- Runtime: `.NET 8.0.27`、`.NET 6.0.36`
- BenchmarkDotNet: 项目文件声明 `0.14.0`

## 前序证据裁决

前序任务 `BING-SQL-RC-HARDENING-20260825-001` 的 Review 已确认：Round 3 provenance 无效，Round 4-10 Root/Join before 不完整或缺失。因此这些结果只能作为历史问题线索，不能作为本任务 before/after delta。

## 本任务基线

本任务未形成有效 Root/Join/IN before/after。前序 FormalHost 结果存在 partial/invalid provenance，不能复用；当前已在本轮源码下完成独立的 Raw 20/50 smoke/FormalHost 结果，但没有旧版源码身份，因此不能计算性能 delta。

因此性能结论为 `blocked`，未将任何历史结果解释为本任务收益或回归，也未在缺少有效 before 的情况下进行性能优化。

## Round 4 当前矩阵

- `SqlLambdaRootBenchmarks`：`RootCount=1,2,5,10`；不再与 IN 参数规模交叉。
- `SqlLambdaInBenchmarks`：`ParameterCount=0,1,10,100,500,1000,2100`；拆分输入创建、预构造值绑定渲染和完整构建渲染。
- `SqlLambdaJoinBenchmarks`：`SourceCount=1,2,5,10`；1 表示根来源，后续值表示实际来源总数。
- `SqlLambdaFixedJoinBenchmarks`：无参数，独立测量重复实体 Join 和 Join 失败路径。
- `SqlRawFromBenchmarks`：`SourceCount=20,50`；Raw 来源字符串独立构造和渲染。
- `SqliteDapperE2EBenchmarks`：`RowCount=1,100,1000`；临时 SQLite 文件上的真实 Dapper `Query().ToList`，与纯 SQL 构建基准分开。
- Raw 20/50 命令：`dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build -- --filter "*SqlRawFromBenchmarks*" --job Dry`。
- 实际结果：Raw 20 FormalHost Mean `3.336 us`，Raw 50 FormalHost Mean `5.083 us`；均为 .NET 8.0.27、BenchmarkDotNet 0.14.0、FormalHost `LaunchCount=3/WarmupCount=6/IterationCount=15`。
- 制品：`BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlRawFromBenchmarks-report.csv`、`-report-github.md`、`-report.html`。
- 制品 SHA-256：CSV=`D103E0CDAD0B317A6B31E8489E52EA25492B741770424E3642089CC945DE8FCE`；Markdown=`9834234CBC795510346F50102A1054BD6D4D78DEC313401AA6B947E8A6EFA130`；HTML=`1CA69EDB032381903ACFA635B9B58C86F9318AB3E264099DC59FADD5349838EC`。
- 当前基线状态仍为 `blocked`：只有当前 Raw 场景有有效单版本结果，Root/Join/IN before/after 及性能准入尚未建立。
- Round 5 当前结果：IN 42-case Dry/FormalHost smoke 和 SQLite/Dapper E2E 3-case Dry/FormalHost smoke 均通过；这些仍是当前版本单版本结果，不构成 before/after。

## Round 6 当前结果

- `SqliteDapperE2EBenchmarks` 当前源码包含 14 个 SQLite/Dapper 代表 case，覆盖 Query/ToEntity/ToList、同步/异步流式、取消、2/5/7 映射、多结果集、提前释放、基数异常、Activity、DiagnosticListener 和 Trace。
- 最新基数异常定向命令使用 `RowCount=1/100/1000`，生成 `BenchmarkDotNet.Artifacts/round6-cardinality-latest`；`Dry + FormalHost` 共 6 个 case，进程退出码均为 0。该结果用于正确性/资源路径验证，不构成性能 before/after。
- 最新 CI smoke 使用独立 `SqlCiSmokeBenchmarks`，生成 `BenchmarkDotNet.Artifacts/round6-ci-smoke-latest`；只执行 1 个 `Dry` case，未携带 `FormalHost`。
- 曾启动的完整 E2E 长矩阵使用修复前编译程序集，部分完成后主动停止；其退出码 1 不作为代码失败或完整 E2E 通过证据。
- 独立旧源码身份、同 Job 完整 before/after 和性能准入：`blocked`。AppVeyor 远程制品和外部 Provider 真实执行同样 `blocked`。

## Round 7 当前结果

- 新增独立 `SqliteDapperE2ESmokeBenchmarks` `[DryJob]` 和 `--e2e-smoke` 入口，避免类级 FormalHost 污染 CI smoke。
- 最新程序集完成 42 个唯一 E2E Dry case：14 方法 × `RowCount=1/100/1000`；CSV/Markdown/HTML 位于 `BenchmarkDotNet.Artifacts/round7-e2e-smoke`。
- 源码 hash：`SqliteDapperE2EBenchmarks.cs`=`90561E1A989E76BC604E37C01107D4E9E4F33DD83D4D7F047C3D35B6E9D54E9E`；`SqlMetadataBenchmarks.cs`=`D09A1898B4CC283800568734800173B00B0988103D1A6ACB7F1FA50201B75303`。
- `appveyor.yml` hash=`84ED8619AC681D997832608BB639BF2707F2AEE0E8567E99C936DC5173935ABF`。
- 制品 hash：CSV=`6313F97067B4EBCECC7BDED8D34303C2698D3A2101CAA14989784232B983B01B`；Markdown=`BF1B9F5F95CF24B5C487B1265E88542CBCCABE1F14353ABCDD3EEA338CC667FF`；HTML=`D8D7184F3789C7101ED5D0B3FCA280FDDDAC2A7ECEDA74D57AD5A1C1AB51AFDE`。
- 结果仅证明当前版本可重复执行，不构成性能 before/after；独立旧源码、FormalHost before/after、远程 AppVeyor 和外部 Provider 仍 `blocked`。

## Round 8 当前结果

- 按 AppVeyor 配置完成本地等价 smoke：`ci-equivalent` 1 个 Dry case，`ci-e2e-equivalent` 42 个 Dry case。
- `ci-equivalent` CSV hash=`1DB4917ECCF9E2249F3197E4F6B84CE03310B2F51559B5DDAB015AC3C0E918AD`；`ci-e2e-equivalent` CSV hash=`E0BB529CD3D19222EF2409BF842D7BA0894FCEF14396B3EFC9876C412710D572`。
- `appveyor.yml` hash=`84ED8619AC681D997832608BB639BF2707F2AEE0E8567E99C936DC5173935ABF`；`global.json` hash=`8E7272916B97B7C032B0D07F5D50C47D7C4FC32369A28A2AEF6CFEA133FEE1B0`。
- 历史 `review-fix-round3-before-root`、`review-fix-round3-before-join`、`review-fix-round4-before-root` 只有旧 Root 72/Join 36 等不匹配矩阵，且缺少旧源码、dirty diff 和独立构建 provenance；裁决为历史/无效，不作为当前 before。
- AppVeyor 远程上传、外部 Provider 和有效 FormalHost before/after 继续 `blocked`；Round 8 结果不构成性能准入。

## 统计口径

逐 case key 使用 `Method + 全部 Params + Job + Runtime`。记录 Mean、Median、Error、StdDev、Allocated、Gen0/Gen1/Gen2；P95 只能来自原始样本或独立采样，不能把 Error 当 P95。每个 artifact 同时保存命令、开始/结束时间、环境、源文件 hash 和输出 hash。
