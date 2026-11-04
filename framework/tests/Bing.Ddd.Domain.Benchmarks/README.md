# ChangeGraph 性能基准

使用生产Source Generator生成Order模型，测量Capture、Compare、CompareSnapshot、有键列表反序移动，以及整棵忽略子树的捕获。图包含共享对象、自循环和字典套列表；计时外创建固定输入，所有执行读取同一份稳定旧状态。MemoryDiagnoser记录分配和GC。

```powershell
dotnet run --project framework/tests/Bing.Ddd.Domain.Benchmarks/Bing.Ddd.Domain.Benchmarks.csproj -c Release -- --smoke
dotnet run --project framework/tests/Bing.Ddd.Domain.Benchmarks/Bing.Ddd.Domain.Benchmarks.csproj -c Release -- --filter '*' --job short --iterationTime 100 --warmupCount 1 --iterationCount 3 --launchCount 1
```

`--smoke`检查固定4条变化、快照比较、移动输出以及忽略getter不执行；不提供性能数字。第二条命令是短样本基线，适合验证基准可运行。更稳定的统计可采用`--job medium`，保留同一机器、TFM、数据和运行参数，再与后续候选比较。

Count为100/1000，实际执行5种操作，共10项。CaptureIgnoredSubtree有意使用独立空根，只包含不会执行的忽略getter，用来观察固定开销；它不随Count创建额外对象。其他操作包含完整图，Compare输出4条叶子变化。Moves反序所有有键元素，因此输出量随Count增加，不与4条修改的Compare直接比较算法效率。

当前本机短样本记录在`ai_docs/tasks/20261029-domain-change-tracking-graph/validation/p3-benchmarks/results/`，含Markdown、CSV和HTML；运行日志与候选清单在相邻validation目录。没有同等历史实现的before，不宣称性能提升，也不把短样本的宽置信区间作为发布门槛。生产包未新增BenchmarkDotNet依赖。
