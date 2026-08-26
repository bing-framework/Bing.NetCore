# Benchmark 报告

- task-id: `BING-SQL-RC-HARDENING-20260825-001`
- 状态：既有 Round 3 before/after provenance 被独立 Reviewer 判定无效；Round 4 尝试在全新 detached worktree 重建 before，但 Root 仅完成部分运行、Join 未运行，当前无新的完整 before/after 结论。

## 环境

- Windows 10 22H2
- Intel Core Ultra 7 270K Plus，24 physical/24 logical cores
- .NET SDK `10.0.300`
- Runtime `.NET 8.0.27`
- BenchmarkDotNet `0.14.0`
- GC：Concurrent Workstation；AVX2

## 已验证

- `dotnet build` Benchmark 项目 Release：通过，保留 `NETSDK1206` RID 警告。
- `--job Dry --filter '*SqlLambdaJoinBenchmarks*'`：36 个组合执行完成，`JoinCount` 为 1/2/5/10，全部为公开类型化 Lambda Join 路径。
- `--job Dry --filter '*SqlLambdaRootBenchmarks*'`：执行完成，方法名为 `BuildRootsAndRender`；RootCount 1/2/5/10 使用类型化 `From<TEntity>()`，20/50 明确属于连续来源/原始表来源压力场景。

## Round 2 FormalHost After

- Root 运行命令：`dotnet run --project framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore --filter "Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks*" --artifacts E:\\Bing_Framework\\Bing.NetCore\\BenchmarkDotNet.Artifacts\\review-fix-round2-root`
- 结果：`72` 个组合执行完成，运行时间约 `46:25`。
- Job：`FormalHost`；`IterationCount=15`；`LaunchCount=3`；`WarmupCount=6`。
- Artifact：
	- `BenchmarkDotNet.Artifacts/review-fix-round2-root/results/Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-report.csv`
	- `BenchmarkDotNet.Artifacts/review-fix-round2-root/results/Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-report-github.md`
	- `BenchmarkDotNet.Artifacts/review-fix-round2-root/results/Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-report.html`
	- `BenchmarkDotNet.Artifacts/review-fix-round2-root/Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-20260825-155004.log`
- 示例：`RootCount=1, ParameterCount=10` 的 `BuildRootsAndRender` 为 `6,430.2 ns`，分配 `17,721 B`；`RootCount=50, ParameterCount=1000` 为 `57,370.9 ns`，分配 `244,900 B`。
- 告警：报告包含 `MultimodalDistribution` 和 outlier warnings；这些告警降低统计稳定性，不能忽略。

## Round 3 FormalHost Before/After

- Before worktree：detached `HEAD=142380be`，路径 `..\\Bing.NetCore-before-BING-SQL-RC-HARDENING-20260825-001`。
- Root before：`BenchmarkDotNet.Artifacts/review-fix-round3-before-root/results/Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-report.csv`。
- Join before：`BenchmarkDotNet.Artifacts/review-fix-round3-before-join/results/Bing.Data.Sql.Benchmarks.SqlLambdaJoinBenchmarks-report.csv`。
- Root after：`BenchmarkDotNet.Artifacts/review-fix-round2-root/results/Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-report.csv`。
- Join after：`BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlLambdaJoinBenchmarks-report.csv`。
- 配置：before/after 均为 `FormalHost`、`IterationCount=15`、`LaunchCount=3`、`WarmupCount=6`、.NET 8.0.27、BenchmarkDotNet 0.14.0、Concurrent Workstation GC、AVX2。
- 样本：Root `72/72` case 可比较；Join `36/36` case 可比较。

## Round 3 Delta 与阈值

- 阈值：单 case Mean 或 Allocated 增长超过 `10%` 记为回归候选；`NA` 指标不参与该指标比较。
- Root：`18/72` case Mean 超过阈值；最高为 `CreateExecutionSnapshot`、RootCount 50、ParameterCount 1000，`+48.07%`。分配增长超过阈值 `0/72`。
- Join：`6/36` case Mean 超过阈值；最高为 `WhereIfFalse`、JoinCount 1，`+39.24%`。可用分配样本中未发现超过阈值的增长；after 有 `1` 个分配字段为 `NA`。
- 统计告警：before/after 均存在多峰分布或离群值提示；该告警作为残余风险记录，未被静默忽略。
- 结论：before/after 可追溯性问题已解决，但性能阈值检查不是全绿，当前不能声称无回归。

## Round 4 Before 重建

- Worktree：`E:\\Bing_Framework\\Bing.NetCore-review-fix-round4-before`，detached `HEAD=142380be3ec62cbd4a26cde8e2795d0eacae47fb`。
- Harness：仅临时同步 `SqlLambdaRootBenchmarks.cs`、`SqlLambdaJoinBenchmarks.cs` 的 FormalHost、方法名和参数矩阵；未修改主工作区源码。
- 构建：before worktree restore/build 成功，产物位于该 worktree 的 `output\\release`。
- Root：运行确认独立输出路径和 Runtime/BDN/Job 正确，但长跑中止于第 5 个 benchmark 左右；Round 4 CSV 仅 `1` 行，不能作为 `72` case before。
- Join：未运行；没有有效 Round 4 Join before artifact。
- 结论：Round 4 未完成性能基线重建，任务保持 `PARTIAL`；不使用不完整 CSV 计算 delta。

## 解释边界

Dry 作业为单次冷启动迭代，BenchmarkDotNet 明确提示观测时间低于 100ms，因此旧 Dry 结果只证明入口、初始化和分配测量可运行。Round 3 已建立可比 FormalHost before/after，但时间回归候选和多峰/离群告警仍阻止无回归结论，也不支持“近零分配”声明。
