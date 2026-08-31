# Benchmark 报告

## 状态

`PARTIAL`。修复 benchmark 类型隔离后，新的 Smoke 与 listener-off FormalHost after 已生成；没有同 key 的 before 制品，因此没有性能比较结论。

## 比较键

每个可比较 case 必须匹配 benchmark type、method、参数、job id、SDK、runtime、OS、CPU、GC、commit、命令和 artifact hash。任一键不匹配时为 `NOT_COMPARABLE`。

## 当前阻塞

本任务开始前没有可复核的 FormalHost before artifact。以下 after 结果是单次当前基线，不声明性能改善或回归：

| Benchmark | Job | RowCount | Mean | Allocated | 结论 |
| --- | --- | --- | --- | --- | --- |
| `SqliteDapperE2EBenchmarks.QueryToList` | FormalHost | 1 | 1.188 ms | 24.55 KB | NOT_COMPARABLE |
| `SqliteDapperE2EBenchmarks.QueryToList` | FormalHost | 100 | 1.291 ms | 36.60 KB | NOT_COMPARABLE |
| `SqliteDapperE2EBenchmarks.QueryToList` | FormalHost | 1000 | 2.207 ms | 142.14 KB | NOT_COMPARABLE |

Smoke 使用 `SqliteDapperE2ESmokeBenchmarks` 的 Dry job，证明默认 listener-off 可扩展路径可运行，不与 FormalHost 数据比较。共享基础设施不声明 `[Benchmark]` 或 `[Params]`；`RowCount=1/100/1000` 仅适用于八个可扩展的 listener-off 路径。steady listener-on、subscribe-plus-query、预取消、基数异常、Activity 与 Trace 都是各自独立的固定输入 benchmark 类型，不继承无关测量项。默认 listener-off 基线不会订阅 `DiagnosticListener.AllListeners`。

当前制品来自 dirty worktree：Source HEAD 为 `faba0eee924b7c992dc0aaad414099d92308f5f9`，benchmark source binary diff SHA-1 为 `22c00a6038eed6082eca70c2db6bc8f116c59d0e`。完整 SHA-256、命令、环境和脱敏状态见 [制品索引](artifact-index.md)；历史 `rc28-e2e-smoke` 与 `rc28-formal-after` 制品不代表当前类型拓扑。

```powershell
dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build --no-restore -- --e2e-smoke --filter "*SqliteDapperE2ESmokeBenchmarks*" --artifacts "BenchmarkDotNet.Artifacts\rc28-e2e-smoke-review"
dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-build --no-restore -- --filter "*SqliteDapperE2EBenchmarks.QueryToList*" --artifacts "BenchmarkDotNet.Artifacts\rc28-formal-after-review"
```
