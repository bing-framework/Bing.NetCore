# Benchmark 报告

## 状态
PARTIAL。已补充并执行 20/50 根来源和 Join、执行快照场景；尚未建立同机旧/新基线，也未新增计划要求的 GetPlan、诊断组合、分页、流式和多结果集场景。

## 已知约束
- 工作区存在上一任务未提交修改，不能把当前输出直接当作干净 HEAD 基线。
- 计划要求使用同机、同配置、同 BenchmarkDotNet 参数进行前后对比。

## 后续
本轮执行命令：

```powershell
dotnet run -c Release --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -p:OutputPath=output/bench-isolated/ -- --filter '*SqlLambda*' --job Dry
```

环境：Windows 10、Intel Core Ultra 7 270K Plus、.NET SDK 10.0.300、Host .NET 8.0.27、BenchmarkDotNet 0.14.0。共执行 126 个 Benchmark case，包含 `RootCount`/`JoinCount` 1、2、5、10、20、50；20/50 场景均成功生成结果。

结果文件：`BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlLambdaRootBenchmarks-report-github.md`、`BenchmarkDotNet.Artifacts/results/Bing.Data.Sql.Benchmarks.SqlLambdaJoinBenchmarks-report-github.md`。

观察值：RootCount=50 时 `SetRootsAndRender` 约 3.90～4.07 ms、253.74 KB，`CreateExecutionSnapshot` 约 4.17～4.32 ms、79.41 KB；JoinCount=50 的结果已写入 Join 报告。Dry Job 仅每次迭代一次，存在 BenchmarkDotNet 的最小迭代时间警告，不作为正式性能回归结论。
