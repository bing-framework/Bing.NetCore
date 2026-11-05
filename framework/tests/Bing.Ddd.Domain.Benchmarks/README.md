# ChangeGraph 性能基准

使用生产Source Generator生成Order模型，测量Capture、Compare、CompareSnapshot、有键列表反序移动，以及整棵忽略子树的捕获。图包含共享对象、自循环和字典套列表；计时外创建固定输入，所有执行读取同一份稳定旧状态。MemoryDiagnoser记录分配和GC。

```powershell
dotnet run --project framework/tests/Bing.Ddd.Domain.Benchmarks/Bing.Ddd.Domain.Benchmarks.csproj -c Release -- --smoke
dotnet run --project framework/tests/Bing.Ddd.Domain.Benchmarks/Bing.Ddd.Domain.Benchmarks.csproj -c Release -- --filter '*' --job short --iterationTime 100 --warmupCount 1 --iterationCount 3 --launchCount 1
```

`--smoke`检查固定4条变化、快照比较、移动输出以及忽略getter不执行；不提供性能数字。第二条命令是短样本基线，适合验证基准可运行。更稳定的统计可采用`--job medium`，保留同一机器、TFM、数据和运行参数，再与后续候选比较。

Count为100/1000，实际执行5种操作，共10项。CaptureIgnoredSubtree有意使用独立空根，只包含不会执行的忽略getter，用来观察固定开销；它不随Count创建额外对象。其他操作包含完整图，Compare输出4条叶子变化。Moves反序所有有键元素，因此输出量随Count增加，不与4条修改的Compare直接比较算法效率。

当前本机短样本记录在`ai_docs/tasks/20261029-domain-change-tracking-graph/validation/p3-benchmarks/results/`，含Markdown、CSV和HTML；运行日志与候选清单在相邻validation目录。没有同等历史实现的before，不宣称性能提升，也不把短样本的宽置信区间作为发布门槛。生产包未新增BenchmarkDotNet依赖。

## 领域对象订单比较基准

`OrderChangeTrackingBenchmarks` 引用 `samples/Bing.Samples.ChangeTracking` 的订单类型，通过生产生成器生成的 `AddChanges` 测量 `GetChanges`。分别比较 2、100、1000 条有身份的明细；每次都包含状态、城市、明细数量各一项修改，以及一项明细新增和删除。`GlobalSetup` 在计时前完整校验路径、描述、新旧值，输入快照的构造不计入测量。该基准不包含 SQLite 数据库读写。

完成依赖还原后，可以运行：

```powershell
dotnet build framework/tests/Bing.Ddd.Domain.Benchmarks/Bing.Ddd.Domain.Benchmarks.csproj -c OrderBench --no-restore -p:Optimize=true -p:DebugType=None
dotnet framework/tests/Bing.Ddd.Domain.Benchmarks/bin/OrderBench/net8.0/Bing.Ddd.Domain.Benchmarks.dll --filter '*OrderChangeTrackingBenchmarks*' --artifacts output/change-tracking-order-benchmark --exporters json
```

基准使用进程内工具链、3 次预热和 7 次测量，并用 `MemoryDiagnoser` 记录托管分配。它是固定样本的本机测量，不能替代跨进程、并发、生产数据库或 NativeAOT 性能验收，也没有预设的性能通过阈值。
