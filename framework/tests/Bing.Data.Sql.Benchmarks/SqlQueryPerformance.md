# SqlQuery 读取和 Builder 性能基线

2026-11-19 Round136 补齐固定 BuilderStateProbe-v1 的本机托管方法归因：EventPipe 实际确认 1133 个 Managed / 219 个 External 样本、356 个方法，丢事件/缺失/未解析为零；原始 trace、方法 CSV、重放和 10 项职责/集成证据见 [Round136](../../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round136.md)。Self/Inclusive 使用 Managed 样本作分母，不能把 External 解释为等待或把百分比换算成整机 CPU；包含样本不能跨方法相加。包含探针搭建和旧策略对照，不改变下列 before/after，不宣称线上改进或容量通过。

2026-11-19 Round135 新增独立源码副本的完整 SQL 基准项目引用图构建测量器：Windows Job 统计 root 与全部子孙的同时提交内存峰值，包含 restore、复用包缓存。实际收据、测试与命令见 [Round135](../../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round135.md) 及 [工具说明](../../../eng/perf/README.md)。本次没有修改下列 before/after 基线；提交内存不等于工作集/RSS，整树累计 CPU 时间不替代方法级 CPU 栈，构建资源也不代表 SQL 请求容量。

2026-11-07 的固定 before/after 绑定任务 20261107-sqlquery-command-aot-design 的 Review Fix Round 2。全部原始样本和候选哈希在 ai_docs/tasks/20261107-sqlquery-command-aot-design/artifacts/fix-round-2；完整18组SQLite与9组Builder结果见该任务performance.md的Round2。

工作负载保持现有 SqlGeneratedRuntimeProbe-v1 / SqlBuilderStateProbe-v1，覆盖1/32/256行、无事务/共享事务/非事务Uow、Compatible/GeneratedOnly，以及简单/深层CTE+Union/动态分页。Builder先断言完整SQL与参数等价，再比较后代Clone数。此次变更只消除每结果列映射扫描、重复绑定集合复制和已深克隆子树的重复Clone，没有调整输入以放大收益。

主要基线：256行无事务GeneratedOnly分配44704.40→38536.40 B；深层CTE/Union重复渲染412720.24→241020.32 B、后代Clone30→9。Compatible九组分配不变。弱引用所有权记录使完整深树Clone增加约912 B，简单完整Clone/动态分页增加8 B。通用object setter仍可能装箱；生成路径并非所有模式都更快，默认Compatible。

本地复现（先还原依赖，输出路径可自定）：

```powershell
dotnet run --project framework/tests/Bing.Data.Sql.Benchmarks -c Release -f net8.0 --no-restore -- --generated-runtime-probe --output runtime-current.json
dotnet run --project framework/tests/Bing.Data.Sql.Benchmarks -c Release -f net8.0 --no-restore -- --builder-state-probe --output builder-current.json
```

这是固定短采样诊断，非正式发布门禁或生产SLA；修改输入/Provider/版本后不能直接复用数值。必须保留候选源码/DLL身份、warmup/sample/iterations、完整样本、失败和不利结果；同候选最多诊断一次和确认一次，不反复运行挑选较快样本。TDD职责测试保留在SqlStaticResultReaderPerformanceTest及SqlQueryRenderStateTest，真实转换/事务/错误语义由SQLite集成和Native Smoke约束。

## 2026-11-12：生成式参数绑定场景

新增 `SqlGeneratedParameterBindingBenchmarks`，覆盖单个标量参数及 0、2、16、128 个元素的集合，包含同名标量、SQL 字符串与注释；每次计入 SQLite 命令和参数对象创建，不执行数据库查询。当前候选在 .NET 8.0.30、Windows x64、BenchmarkDotNet 0.14.0 进程内短跑（1 次预热、3 次 100 ms 采样）中，标量分配为 336 B/次，集合分别为 1,536、2,048、6,600、38,248 B/次。集合越长会产生更多参数、名称和 SQL 文本；样本不能归因到具体对象，也没有可比的修改前基线。

常规进程外工具链在当前工作区因历史任务快照包含多个同名 csproj 而无法定位项目；进程内 Dry 和上述短跑可运行。短跑部分采样未达到建议时长，误差较大，结果只用于本地工作负载诊断，不用于判断修复前后的性能增减或生产并发容量。原始本机报告位于 `.codex-build/round40-bdn-short/results/`。

## 2026-11-12：形状模板的顺序槽位绑定

新增 `SqlQueryShapeValueBindingBenchmarks`，在相同候选、相同热缓存形状上比较名称字典 `Render` 与顺序槽位 `RenderValues`。可选条件数为 0、2、8、16，均启用，另有一个固定参数；启动时逐字检查完整 SQL、参数顺序、值和 DbType。分为复用输入与每次新建输入容器两组，四条路径使用相同的预先装箱值，不把装箱或数据库操作混入容器对比。

本机 .NET 8.0.30、Windows x64、BenchmarkDotNet 0.14.0 进程内短跑，1 次预热、3 次 100 ms 采样，16/16 个场景完成。每次调用的托管分配如下：

| 可选条件数（总参数数） | 新建字典后 Render | 新建数组后 RenderValues | 两种输入均复用时 |
| --- | ---: | ---: | ---: |
| 0（1） | 480 B | 296 B | 均为 264 B |
| 2（3） | 768 B | 600 B | 均为 552 B |
| 8（9） | 1,856 B | 1,512 B | 均为 1,416 B |
| 16（17） | 3,176 B | 2,728 B | 均为 2,568 B |

当调用方每次创建输入容器时，顺序数组在这组工作负载中减少 168～448 B/次；输入已经复用时，渲染结果及参数快照的分配保持相同。短采样中槽位路径平均用时较低，但存在较大置信区间和部分不足 100 ms 的迭代告警，不据此承诺稳定吞吐提升。此结果是同候选两种 API 的比较，不是历史版本的修改前后基线。

测量只包含模板缓存查找、绑定、输入容器（新建组）和渲染结果创建；不包含调用方值类型装箱、公开 `Parameters` 访问时的快照、数据库命令绑定和 I/O。实际调用仍有数组、装箱和 SqlParam 等分配，不能将该入口描述为零分配，也不能从本机短跑推断生产并发容量。

复现命令（项目根目录执行，先还原依赖）：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:OutputPath=.codex-build/bin/round43-benchmark/ --verbosity quiet
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round43-benchmark/Bing.Data.Sql.Benchmarks.dll --filter "*SqlQueryShapeValueBindingBenchmarks*" --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts .codex-build/round43-shape-values
```

候选 `Bing.Data.Sql.Benchmarks.dll` SHA-256：`F2280A070C094DF80FC50E9CEB35D1EE2444C759E01DF0D699DA53854390E023`；同目录 `Bing.Data.Sql.dll`：`C02DC16235C76501621DFCBC251904D30619F59C249A34E50CACEF1FFD8A2F73`。原始样本与告警保留于 `.codex-build/round43-benchmark.log`，完整 CSV、Markdown 和 HTML 报告位于 `.codex-build/round43-shape-values/results/`。复用前述进程外工具链不适用的证据，本轮仅做一次进程内诊断，未反复采样挑选结果。

## 2026-11-12 Round 44：模板直接创建文本查询

新增 `SqlShapeQueryBenchmarks`，比较相同候选的两个 API 路径：直接 `query.Sql(template, 0, values)`，以及 `RenderValues` 后读取公开 `fragment.Parameters` 再调用 `query.Sql(sql, parameters)`。两者共享真实 `SqliteSqlQuery` 根对象和热缓存模板，连接不打开、不调用终结方法；Setup 核对完整 SQL、参数顺序、值和元数据。输入容器和值提前创建及装箱；槽位数包含一个二进制参数，其余为整数。

本机 .NET 8.0.30、Windows x64、BenchmarkDotNet 0.14.0 进程内短跑，1 次预热、3 次 100 ms 采样，8/8 个场景完成。托管分配如下：

| 参数槽位数 | 二进制长度 | 先渲染后传公开参数 | 直接 Sql 模板入口 | 每次减少 |
| --- | ---: | ---: | ---: | ---: |
| 1 | 0 B | 1,040 B | 744 B | 296 B |
| 1 | 4,096 B | 13,328 B | 8,936 B | 4,392 B |
| 9 | 0 B | 4,904 B | 3,224 B | 1,680 B |
| 9 | 4,096 B | 17,192 B | 11,416 B | 5,776 B |

这组查询描述构造工作负载的分配减少约 28%～34%，原因是跳过公开 `Parameters` 属性的一次参数及数组值复制；渲染和 `SqlTextQuery` 自身的快照仍保留。直接入口平均耗时为 160.5～979.1 ns，对照为 240.0～1,536.8 ns，但短采样置信区间较大且存在 `MinIterationTime` 告警，不作为稳定吞吐结论。这是同候选两种 API 比较，不是历史版本基线，也不包括数据库绑定、终结计划创建、I/O、连接/事务或生产并发容量。

复现命令（先还原依赖）：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:OutputPath=.codex-build/bin/round44-benchmark/ --verbosity quiet
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round44-benchmark/Bing.Data.Sql.Benchmarks.dll --filter "*SqlShapeQueryBenchmarks*" --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts .codex-build/round44-shape-query
```

候选 Benchmark DLL SHA-256：`2CEA150DF72DA2DA3A44E0E1203032079CB6B93D40FA9A468C06A992FA1B29AE`；同目录 `Bing.Data.Sql.dll`：`50FA366ADB95EF33181718E8144F87F3B4713AEA54B5B859E09A838EB6F962D4`。原始输出保存在 `.codex-build/round44-benchmark.log`；CSV、Markdown 和 HTML 报告在 `.codex-build/round44-shape-query/results/`。沿用已有进程外工具链不适用的证据，本轮只进行一次进程内诊断。

## 2026-11-17 Round 101：SQLite 查询分页作用域

新增 `SqliteDapperPagingBenchmarks` 和 `SqliteDapperPagingSmokeBenchmarks`。工作负载每次创建查询与 Builder，选择 `Name`、按未投影的 `Id` 排序、保留旧 `Take(1)` 后执行第二页；数据库包含三行。Setup 在测量前验证总数为 3 且第二页为 `row-2`。每次测量包含查询工厂、Builder、Count/Page 派生、SQLite 两次命令执行和结果读取，不能将全部分配归因于 Builder 克隆。

在 Windows x64、.NET 8.0.30、BenchmarkDotNet 0.14.0 进程内，使用自定义 `Round101` 配置并启用优化，1 次预热、3 次 100 ms 采样得到平均 `978.4 μs`、误差 `600.6 μs` 和 `76.78 KB/次` 托管分配。样本短且误差较大，仅作为当前工作负载的诊断数据；旧实现不能正确执行该工作负载，没有可比的修改前基线，不据此宣称提速或推导并发容量。

原进程外 `Dry` 作业因仓库历史任务快照包含多个同名 `Bing.Data.Sql.Benchmarks.csproj` 无法定位唯一项目而失败；报告中进程内 `ShortRun` 完成，两个作业不能合称全部通过。常规 Release 构建还遇到共享 `obj` 资源文件拒绝访问，故使用自定义优化配置及独立输出，不把该结果称为干净 Release 构建。完整正式进程外基准、CPU 归因和容量验证仍待完成。

本机复现命令（先还原依赖）：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Round101 -f net8.0 --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:Optimize=true -p:DefineConstants=RELEASE -p:OutputPath=H:\Bing_Framework\Bing.NetCore\output\round101-benchmark-final3\ --verbosity quiet
dotnet output/round101-benchmark-final3/Bing.Data.Sql.Benchmarks.dll --filter '*SqliteDapperPagingSmokeBenchmarks*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round101-paging-final3
```

候选 `Bing.Data.Sql.Benchmarks.dll` SHA-256：`29838193C201BE6A88C328F205B3F12E5BC89A104E0F458C19F8BC3260C6C815`；`Bing.Data.Sql.dll`：`D2D83EB68DD4F99FAFAB0B86E3777ECDE0FFDF0F4B604A08C8632D92FBD15D39`；`Bing.Dapper.Core.dll`：`6A22A8D5638C394C07B08607B3DFC5BBB02897EDC0BF40DE65B43CA5DDDB9A53`。报告和原始日志在本机 `output/round101-paging-final3/`。

## 2026-11-17 Round 102：冻结查询计数规划

新增 `SqlPagingPlanningBenchmarks`。Setup 创建含旧分页的 SQLite Builder 并冻结，逐字验证普通和分组计数 SQL；测量仅包括从冻结计划派生 Count，不执行数据库、读取结果或创建初始查询。参数个数为 1 和 8，分组计数需要外层包装。

Windows x64、.NET 8.0.30、BenchmarkDotNet 0.14.0，进程内短跑、1 次预热、3 次 100 ms 采样得到：

| 场景 | 筛选参数 | 平均耗时 | 误差 | 托管分配／次 |
| --- | ---: | ---: | ---: | ---: |
| 普通 Count | 1 | 3.569 μs | 3.409 μs | 16.16 KB |
| 分组 Count | 1 | 6.021 μs | 3.036 μs | 26.54 KB |
| 普通 Count | 8 | 5.305 μs | 3.336 μs | 23.92 KB |
| 分组 Count | 8 | 10.804 μs | 19.490 μs | 35.84 KB |

这些数字记录当前候选的规划成本，不能单独归因到某个快照或克隆；不存在可比较的修改前基线，不能据此宣称优化带来了性能提升。短采样误差较大且有最短迭代时长告警，环境无法设置基准电源计划，因此也不能用于推导生产吞吐和并发容量。正式进程外对照、方法级 CPU 归因及干净构建峰值仍待验证。

本机复现命令（先还原依赖）：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Round102 -f net8.0 --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:Optimize=true -p:DefineConstants=RELEASE -p:OutputPath=H:\Bing_Framework\Bing.NetCore\output\round102-benchmark\ --verbosity quiet
dotnet output/round102-benchmark/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round102-paging-planning
```

候选 `Bing.Data.Sql.Benchmarks.dll` SHA-256：`C4FF5A7EE5DFC0BD67DFE795939D9562B91006C1B6B53C5ECD0F694558A8C8C1`；`Bing.Data.Sql.dll`：`1D3C647E69C8413C830CAD6A0AC4E249CEDF07A4CC44FDEAE3FF7542052C8A56`。本机报告和原始日志位于 `output/round102-paging-planning/`。

## 2026-11-17 Round 103：子查询合并资源探针

本轮修改 Builder 子查询参数合并，因此复用 `SqlBuilderStateProbe-v1` 检查分配。使用已保存的 Round 102 优化构建作为对照，再顺序执行当前候选优化构建；两侧均为 Windows x64、.NET 8.0.30、2 次预热和 5 个样本，各包含 31 组场景。以下为每组 5 个样本的托管分配中位数，单位为 B／次：

| 场景 | Round 102 | Round 103 |
| --- | ---: | ---: |
| 深度 6 CTE／Union 重复渲染 | 224,260.32 | 224,260.32 |
| 8 项宽 CTE 重复渲染 | 165,044.32 | 165,044.32 |
| 32 项宽 CTE 重复渲染 | 648,932.16 | 655,204.16 |
| 128 项宽 CTE 重复渲染 | 2,975,407.12 | 2,975,407.12 |
| 嵌套 Builder 模板 | 31,129.32 | 31,129.32 |
| 带逻辑键的嵌套 Builder 模板 | 33,281.32 | 33,281.32 |

32 项宽 CTE 的候选样本为 `655,208.08`、`655,204.16`、`655,208.08`、`651,817.28`、`648,936.08 B/次`，存在采样波动；本次单次顺序探针不足以判断稳定回归或提升。它主要使用 SQLite 方言，不能推断非幂等自定义方言的开销；也不测查询／写入执行器构造，不能量化本轮减少 owner 的收益。探针不是正式进程外 Benchmark，不提供方法级 CPU 归因或生产并发容量。

本机复现命令（先还原依赖；Round 102 目录必须保留原始基线）：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Round103Bench -f net8.0 --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:Optimize=true -p:DefineConstants=RELEASE -p:OutputPath=H:\Bing_Framework\Bing.NetCore\output\round103-benchmark\ --verbosity quiet
dotnet output/round102-benchmark/Bing.Data.Sql.Benchmarks.dll --builder-state-probe --output output/round103-builder-before.json
dotnet output/round103-benchmark/Bing.Data.Sql.Benchmarks.dll --builder-state-probe --output output/round103-builder-after.json
```

候选 `Bing.Data.Sql.Benchmarks.dll` SHA-256：`2D47CD74D979188443E2E2B2758D8D1DDD423CD83FD8EF5CCF6A0124396CEF50`；`Bing.Data.Sql.dll`：`D3208F94CC31445B7FE20950B43405E36B6867557369A606D1B0F84A4FDD1544`。基线 DLL 身份见上一节；两份原始探针 JSON 位于上述 `output/` 路径。基准项目为 `SqlPagingPlanningBenchmarks` 补齐显式 `System`／`System.Linq` 引用，使独立配置构建不依赖隐式 using。

## 2026-11-17 Round 104：资源职责迁移的测量范围

查询事务策略和短事务标记迁入既有查询 owner，写入改用独立事务入口，移除写入查询清理字段及异步空清理调用。没有新增资源 owner 对象、缓存或逐行处理层；查询 owner 的字段布局也随之变化。未执行可比的构造/操作分配测量，因此不量化净内存变化，也不声明 CPU、吞吐或延迟提升。

本轮未触及映射缓存、格式化、Builder 克隆或 SQL 输出，现有 Builder 基线工作负载不适用本次资源变更，标记 `NOT_APPLICABLE`，不因更新文档重复运行 Round 103 探针。Core、SQLite 行为测试和 Native 查询冒烟仅作为正确性证据；方法级 CPU 归因、完整干净构建峰值和生产并发容量仍待外部环境验证。

## 2026-11-17 Round 105：冻结查询派生减少一次 Builder 克隆

本轮在已有规划源副本上直接生成 Count/Page，移除递归包装计划造成的第二次完整 Builder 克隆。新增 SimplePage/GroupedPage 工作负载；Setup 在两侧测量前均断言完整 SQL、参数名称、顺序和值，输入包含旧 Skip/Take，派生时替换它们。简单/分组各测 1、8 个筛选参数，共八组。分页两侧均创建一个备用 SQLite Provider Builder，未通过改变输入放大候选收益。

原始与候选分别使用 `Round105Before`/`Round105After` 优化构建；BenchmarkDotNet 0.14.0、Windows x64、.NET 8.0.30，进程内一轮预热、三轮 100 ms 采样。先后各运行一次；未与构建、测试或另一组测量并发。表中 KB 为托管分配/次，1 KB = 1024 B；数值使用 BDN 输出精度，降低比例为近似值。

| 场景 | 筛选数 | 原始分配 KB | 候选分配 KB | 约减少 | 原始均值 μs | 候选均值 μs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| SimpleCount | 1 | 16.16 | 11.56 | 28.5% | 3.629 | 9.097 |
| GroupedCount | 1 | 26.54 | 21.80 | 17.9% | 6.020 | 4.410 |
| SimplePage | 1 | 25.88 | 21.22 | 18.0% | 6.543 | 4.978 |
| GroupedPage | 1 | 26.91 | 22.17 | 17.6% | 6.943 | 5.879 |
| SimpleCount | 8 | 23.92 | 18.28 | 23.6% | 7.286 | 5.810 |
| GroupedCount | 8 | 35.84 | 30.11 | 16.0% | 12.056 | 10.452 |
| SimplePage | 8 | 34.42 | 28.78 | 16.4% | 12.805 | 10.461 |
| GroupedPage | 8 | 36.02 | 30.30 | 15.9% | 13.219 | 11.042 |

八组分配均减少；克隆预算由直接测试约束为一次。时间证据尚不足：单参数 SimpleCount 候选三个样本为 10.710、9.606、6.976 μs，均值 9.097 μs，标准差 1.918 μs，99.9% 区间为 [-25.899, 44.093] μs。保留该变慢结果，不用其他组替代它。短预热下的时间不稳定，不能据此认定稳定回归或 CPU 提升；本轮不重复采样挑选较快结果。

两侧均有 MinIterationTime 和无法设置电源计划的提示，候选最短样本约 76.587 ms。只测冻结计划的派生，不含初次 Freeze、数据库执行、命令绑定或结果物化。上述结果仅支持此工作负载的托管分配诊断，不能推导进程峰值、完整构建峰值、生产并发容量或查询端到端吞吐，也不替换历史正式基线。方法级 CPU 与进程外稳态时间仍为 NOT_VERIFIED。

本地复现命令（依赖已还原；原始构建必须来自修改前备份，不能用当前源码重建并覆盖它）：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj --no-restore -c Round105Before -p:Optimize=true -p:DefineConstants=RELEASE -m:1 -nr:false -p:UseSharedCompilation=false --verbosity quiet
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round105Before/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round105-before-bench
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj --no-restore -c Round105After -p:Optimize=true -p:DefineConstants=RELEASE -m:1 -nr:false -p:UseSharedCompilation=false --verbosity quiet
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round105After/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round105-after-bench
```

两侧工作负载源码 SHA-256 相同：`8323af6eb5c66cc5c6de0d5552d4adf7a0720cd09f0a6cea1b439c6c0436a07b`。原始 Benchmark DLL：`2acdacfdde9bc2386e2b02061d91ec990a5f3619f3a200d64f0fd89130ca5c67`；原始 SQL DLL：`c51c0679a276b48088caf7340a7753f197257b19d96c420494ac366f69347880`。候选 Benchmark DLL：`f579a0ce06d765521410e0c1cfc56b5d252c1cb1c14bebf16b10de54769f3894`；候选 SQL DLL：`389331fa2873f215057f05adce8df2bd169ec426a7f30c5c0ec62753858fe0c8`。

完整 CSV/Markdown、原始样本与告警分别在 `output/round105-before-bench/`、`output/round105-after-bench/`，汇总、源码身份、回归 TRX 和复现命令见 `output/round105-results.json`。本轮未测资源 owner 构造，也未移除源计划的规划 Builder，不声明完整 IR 或物理资源拆分完成。

## 第 106 轮：从不可变 Select 边界派生 Count

本轮不修改已有 Benchmark 工作负载，直接复用 `Round105After` 的固定二进制和第 105 轮候选报告作为原始侧；最终候选构建为 `Round106Final`。`SqlPagingPlanningBenchmarks.Setup` 继续校验完整 Count/Page SQL、参数名称、顺序和值，两侧语义相同。四类冻结 Count 不再调用 Builder.Clone 或 ToSql；Page 仍保留规划 Builder。

| 方法 | 筛选数 | 原始分配 KB | 候选分配 KB | 原始均值 μs | 候选均值 μs |
| --- | ---: | ---: | ---: | ---: | ---: |
| SimpleCount | 1 | 11.56 | 1.12 | 9.097 | 0.164 |
| GroupedCount | 1 | 21.80 | 1.27 | 4.410 | 0.156 |
| SimpleCount | 8 | 18.28 | 2.65 | 5.810 | 0.455 |
| GroupedCount | 8 | 30.11 | 2.80 | 10.452 | 0.450 |
| SimplePage | 1 | 21.22 | 21.31 | 4.978 | 5.287 |
| GroupedPage | 1 | 22.17 | 22.64 | 5.879 | 5.550 |
| SimplePage | 8 | 28.78 | 28.87 | 10.461 | 6.757 |
| GroupedPage | 8 | 30.30 | 30.77 | 11.042 | 7.391 |

Count 分配减少约 85.5%～94.2%；Page 增加约 0.09～0.47 KB。计数测量不含首次冻结成本，因而补充 `SqlLambdaRootBenchmarks.CreateExecutionSnapshot` 的同构对比，直接测量捕获 SQL、参数与结构边界的代价：

| 根来源数 | 原始分配 KB | 候选分配 KB | 原始均值 μs | 候选均值 μs |
| --- | ---: | ---: | ---: | ---: |
| 1 | 7.50 | 7.61 | 1.781 | 1.942 |
| 2 | 8.71 | 8.82 | 2.015 | 2.311 |
| 5 | 12.23 | 12.34 | 2.884 | 3.343 |
| 10 | 18.13 | 18.24 | 4.267 | 4.805 |

捕获分配增加约 0.11 KB；这仍不是完整 Freeze、数据库、绑定、物化或端到端查询基准。只保存位置、布尔值和已冻结的别名/参数名称，不保存 Clause、Builder、连接或事务。缓存 SQL 与结构一起失效和回滚，不能将结构快照替换为缓存运行时参数值。

两侧使用 .NET 8.0.30，候选 SDK 为 10.0.400；有效行均为 InProcessEmitToolchain、单进程、1 次预热、3 次 100ms 短采样。最终四组捕获均值增加，时间、Error、StdDev 和不利结果全部保留；不认定稳定 CPU 提升或回归，不自行批准性能门禁。捕获基准的 `[SimpleJob]` 还附带四个 FormalHost 作业/侧，这些默认工具链作业因根目录下同名 Benchmark 项目副本导致生成失败，结果为 NA；只比较双方成功的 ShortRun/InProcess 行，没有重跑或删除失败证据。

`Round106After` 是换行改为跟随运行平台前的候选，原始报告保留在 `output/round106-after-bench/` 与 `output/round106-capture-after-bench/`。该候选的三组 Page 均值增加，5 根来源捕获均值为 3.207 μs，同样保留。最终确认是源码修正后的独立构建，不用较快的候选样本替换较慢结果；两次构建的身份和报告均记录在汇总中。

本轮候选与捕获对比复现命令如下。原始侧只能使用保留二进制，禁止从当前源码重建并覆盖：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj --no-restore -c Round106Final -p:Optimize=true -p:DefineConstants=RELEASE -m:1 -nr:false -p:UseSharedCompilation=false --verbosity quiet
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round106Final/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round106-final-bench
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round105After/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlLambdaRootBenchmarks.CreateExecutionSnapshot*' --inProcess --job short --launchCount 1 --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round106-capture-before-bench
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round106Final/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlLambdaRootBenchmarks.CreateExecutionSnapshot*' --inProcess --job short --launchCount 1 --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round106-final-capture-bench
```

原始和候选源码/二进制 SHA-256、完整 CSV/日志、测试收据及复现信息见 `output/round106-results.json`。生产容量、方法级 CPU、进程峰值及完整干净构建峰值仍待目标环境验证；源计划的 Page/CTE/Union 规划 Builder 仍未移除。

## 第 107 轮：已有分页 SQL 的不可变页面派生

复用第 106 轮最终二进制及 CSV 为固定前态，身份校验通过后才比较；工作负载两个源码 SHA-256 均未变化。候选配置为 Round107Final。SqlPagingPlanningBenchmarks 的 Setup 继续核对完整 SQL、参数顺序和数值，没有以改变查询语义换取收益。

| 方法 | 筛选数 | 前态分配 | 候选分配 | 前态均值 | 候选均值 |
| --- | ---: | ---: | ---: | ---: | ---: |
| SimpleCount | 1 | 1.12 KB | 1.13 KB | 163.7 ns | 175.0 ns |
| GroupedCount | 1 | 1.27 KB | 1.28 KB | 155.7 ns | 165.1 ns |
| SimplePage | 1 | 21.31 KB | 7.08 KB | 5,286.9 ns | 801.0 ns |
| GroupedPage | 1 | 22.64 KB | 7.08 KB | 5,550.0 ns | 788.2 ns |
| SimpleCount | 8 | 2.65 KB | 2.66 KB | 455.2 ns | 812.9 ns |
| GroupedCount | 8 | 2.8 KB | 2.81 KB | 450.4 ns | 839.3 ns |
| SimplePage | 8 | 28.87 KB | 8.23 KB | 6,757.3 ns | 1,956.9 ns |
| GroupedPage | 8 | 30.77 KB | 8.23 KB | 7,391.0 ns | 1,953.2 ns |

沿用排序的分页派生分配减少约 66.78%～73.25%。这里只测已冻结计划的派生，仍包含调用方预先创建的 Provider Builder；快速路径不消费该 Builder。未来应收拢执行器的提前创建行为，但本轮没有扩大公共 API。首次 Freeze、数据库、命令绑定、读取物化和并发容量均不在此计时区间。

每个快照新增一个可空方言引用，Count 分配约增加 8 B（报告四舍五入为约 0.01 KB）。已有捕获工作负载继续测量首次渲染成本：

| 根来源数 | 前态分配 | 候选分配 | 前态均值 | 候选均值 | 候选 Error |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 7.61 KB | 7.63 KB | 1.942 μs | 10.716 μs | 52.4202 μs |
| 2 | 8.82 KB | 8.84 KB | 2.311 μs | 3.166 μs | 2.0310 μs |
| 5 | 12.34 KB | 12.36 KB | 3.343 μs | 4.254 μs | 0.8006 μs |
| 10 | 18.24 KB | 18.26 KB | 4.805 μs | 6.113 μs | 1.7618 μs |

捕获分配约增加 24 B（报告约 0.02 KB），不能只报告分页收益。四组捕获耗时均增加，单根来源为 10.716 μs，Error 52.4202 μs；8 参数 Count 从 455.2/450.4 ns 升为 812.9/839.3 ns。不利数据完整保留，没有重采样或选用更快样本。短进程内测量不能判定稳定 CPU 回归或提升，方法级归因仍为 NOT_VERIFIED，应在目标环境做独立分析。

环境沿用 .NET 8.0.30、SDK 10.0.400、BenchmarkDotNet 0.14.0，InProcessEmitToolchain、1 次预热、3 次 100ms 短采样。继承属性仍附带四个 FormalHost 默认工具链捕获作业，因同名项目副本的既有准备失败报告 NA；这些作业保留在原始日志中，只比较同构成功的 ShortRun 行，不据此批准正式性能门禁。

候选复现命令（前态禁止从当前源码重建覆盖）：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj --no-restore -c Round107Final -p:Optimize=true -p:DefineConstants=RELEASE -m:1 -nr:false -p:UseSharedCompilation=false --verbosity quiet

dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round107Final/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round107-final-bench

dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round107Final/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlLambdaRootBenchmarks.CreateExecutionSnapshot*' --inProcess --job short --launchCount 1 --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round107-final-capture-bench
```

原始报告在 output/round107-final-bench 和 output/round107-final-capture-bench，源码、二进制、TRX、原始样本和告警的 SHA-256 与前态复用证据见 output/round107-results.json。生产容量、完整干净构建峰值、进程峰值和方法级 CPU 仍待目标环境验证。

## 第 108 轮：执行入口按需创建 Provider Builder

`SqlPagingPlanningBenchmarks` 保留 SimplePage/GroupedPage 的 eager Builder 创建方式，新增 SimplePageExecution/GroupedPageExecution 复用既有 ISqlQueryBuilderSource，反映 Core 分页的按需选择。Setup 同时断言完整 SQL、参数名称/顺序/值，以及结构化路径不创建 Provider Builder。

以下为同一个 Round108Final 候选内两个入口的成本比较。查询语义和数据相同，但方法入口不同；不将新增方法伪称为历史版本同方法 before/after，也没有重建 Round106/107 固定二进制。

| 方法 | 筛选数 | 分配 | 均值 | Error | StdDev |
| --- | ---: | ---: | ---: | ---: | ---: |
| SimplePage | 1 | 7.08 KB | 739.3 ns | 458.16 ns | 25.11 ns |
| SimplePageExecution | 1 | 1.44 KB | 220.1 ns | 66.40 ns | 3.64 ns |
| GroupedPage | 1 | 7.08 KB | 758.2 ns | 268.87 ns | 14.74 ns |
| GroupedPageExecution | 1 | 1.44 KB | 219.3 ns | 81.05 ns | 4.44 ns |
| SimplePage | 8 | 8.23 KB | 1,081.2 ns | 44.51 ns | 2.44 ns |
| SimplePageExecution | 8 | 2.59 KB | 545.6 ns | 285.12 ns | 15.63 ns |
| GroupedPage | 8 | 8.23 KB | 1,078.0 ns | 339.75 ns | 18.62 ns |
| GroupedPageExecution | 8 | 2.59 KB | 550.5 ns | 221.47 ns | 12.14 ns |

四组约减少 5.64 KB/次托管分配；从四舍五入报告值计算约减少 68.53%～79.66%。它只覆盖已分页、排序不变的冻结 IR 派生，不含来源实例创建、首次 Freeze、原生 SQL 包装、动态排序、数据库执行、绑定、物化或并发请求。

环境为 Windows x64、.NET 8.0.30、SDK 10.0.400、BenchmarkDotNet 0.14.0，InProcessEmitToolchain、1 次预热、3 次 100 ms 短采样；八行全部完成。仅运行一次，不与构建或测试并发，保留 MinIterationTime/电源计划提示和全部误差。短采样均值不能替代方法级 CPU 归因和稳态生产测量。

Count/首次捕获的生产实现及工作负载未变，本轮 REUSED_UNCHANGED_SCOPE：仍保留第 107 轮约 8 B/24 B 增量、8 参数 Count 与四组捕获较慢计时、FormalHost NA 等证据，不用本轮较快 Page 数据覆盖。正式性能门禁、方法级 CPU、完整干净构建峰值和生产容量继续 NOT_VERIFIED/BLOCKED_EXTERNAL。

复现候选入口比较：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj --no-restore -c Round108Final -p:Optimize=true -p:DefineConstants=RELEASE -m:1 -nr:false -p:UseSharedCompilation=false --verbosity quiet

dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round108Final/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks*Page*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round108-final-bench
```

原始报告为 output/round108-final-bench/results/Bing.Data.Sql.Benchmarks.SqlPagingPlanningBenchmarks-report.csv；完整命令、原始日志、程序集与工作负载身份、TDD/回归 TRX 见 output/round108-results.json。

## 第 109 轮：未分页有序计划从 IR 追加分页

新增 UnpagedSimplePage、UnpagedGroupedPage、UnpagedFreeze；两侧使用完全相同的新工作负载。前态为本轮保存的修改前生产源码，经 output/round109-baseline.targets 替换 Compile 输入；候选为最终生产代码。两侧 Optimize=true，不恢复工作区，不重建第 106/107/108 轮二进制。Setup 核对完整 SQL 和参数，分页入口复用已有 Builder 来源实例。

| 方法 | 筛选数 | 前态 Allocated | 候选 Allocated | 前态 Mean | 候选 Mean |
| --- | ---: | ---: | ---: | ---: | ---: |
| UnpagedSimplePage | 1 | 16.63 KB | 1.79 KB | 20.444 μs | 257.1 ns |
| UnpagedGroupedPage | 1 | 17.78 KB | 1.83 KB | 5.420 μs | 269.0 ns |
| UnpagedSimplePage | 8 | 24.01 KB | 3.22 KB | 6.451 μs | 560.6 ns |
| UnpagedGroupedPage | 8 | 25.91 KB | 3.26 KB | 7.149 μs | 572.3 ns |
| UnpagedFreeze | 1 | 23.74 KB | 24.02 KB | 6.246 μs | 6253.0 ns |
| UnpagedFreeze | 8 | 41.99 KB | 42.28 KB | 11.367 μs | 11028.3 ns |

页面派生分配约减少 86.6%～89.7%；首次创建/冻结多约 0.28～0.29 KB（0.7%～1.2%），包含冻结分页描述和命名/排序捕获成本。页面工作负载排除首次 Freeze、数据库、绑定、物化、网络和请求并发，UnpagedFreeze 单独包含 Builder 创建与冻结。

.NET 8.0.30 / SDK 10.0.400 / BenchmarkDotNet 0.14.0，ShortRun/InProcessEmitToolchain，每侧一次成功执行，1 次预热、3 次 100 ms 采样。完整 Error/StdDev/Gen0/样本均保留；首个基线页面 Error 为 19.8970 μs、StdDev 1.0906 μs，首次冻结 1 参数计时也没有变快，不重新采样挑选结果。最初非优化宿主拒绝采样及首次基线重复 Compile 构建失败各自保留日志，不计为产品错误或额外成功性能样本。

这些数据只用于分配诊断，不构成稳态 CPU、正式阈值门禁或生产容量结论。方法级 CPU、完整干净构建进程峰值和生产容量继续 NOT_VERIFIED/BLOCKED_EXTERNAL；已有历史较慢结果和捕获代价仍保留。

复现固定前态与候选（原始成功样本不覆盖）：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj --no-restore -c Round109Baseline -p:Optimize=true -p:CustomBeforeMicrosoftCommonTargets=H:/Bing_Framework/Bing.NetCore/output/round109-baseline.targets -m:1 -nr:false -p:UseSharedCompilation=false --verbosity quiet
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj --no-restore -c Round109Candidate -p:Optimize=true -m:1 -nr:false -p:UseSharedCompilation=false --verbosity quiet

dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round109Baseline/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks.Unpaged*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round109-baseline-reproduction
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round109Candidate/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks.Unpaged*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round109-candidate-reproduction
```

原始成功报告在 output/round109-baseline-bench 与 output/round109-candidate-bench；编译输入、两侧 DLL/工作负载 SHA-256、TRX、Native AOT 和失败宿主日志身份见 output/round109-results.json。

## 第 110 轮：冻结 IR 的无排序与追加排序页面


本轮在修改生产代码之前编译并测量 Round110Baseline，再编译最终 Round110Candidate。两侧同一新工作负载，同为 .NET 8.0.30 / SDK 10.0.400 / Windows x64 / BenchmarkDotNet 0.14.0，Optimize=true；每侧仅一次成功 ShortRun/InProcessEmitToolchain，1 次预热、3 次 100 ms 采样。无并发重负载。

| 方法 | 筛选数 | 前态 Allocated | 候选 Allocated | 前态 Mean | 候选 Mean |
| --- | ---: | ---: | ---: | ---: | ---: |
| UnorderedPage | 1 | 17.08 KB | 3.37 KB | 5.987 μs | 1.207 μs |
| AppendedOrderPage | 1 | 17.13 KB | 3.64 KB | 6.214 μs | 1.244 μs |
| UnorderedPage | 8 | 24.64 KB | 5.07 KB | 7.226 μs | 2.266 μs |
| AppendedOrderPage | 8 | 24.70 KB | 5.62 KB | 7.823 μs | 2.430 μs |
| UnpagedFreeze | 1 | 24.02 KB | 24.09 KB | 6.822 μs | 17.638 μs |
| UnpagedFreeze | 8 | 42.28 KB | 42.31 KB | 11.541 μs | 10.630 μs |

四组页面派生分配约减少 77.2%～80.3%；首次创建／Freeze 多约 0.03～0.07 KB。单参数 Freeze 候选 Mean 明显上升，Error 91.6887 μs、StdDev 5.0258 μs，原态 Error 22.899 μs；完整样本与告警保留，不重复采样挑选结果，也不能据此判定稳态 CPU 提升。

工作负载 Setup 断言完整 SQL／参数。页面样本不包含初次冻结、数据库、绑定、物化、网络或请求并发；UnpagedFreeze 单独包含 Builder 创建与冻结。并发单元测试证明隔离，不证明生产容量。

原始报告：output/round110-before-bench、output/round110-after-bench；两侧 DLL、工作负载和结果 SHA-256 及命令收据见 output/round110-results.json。output/round110-before.json 仅保存开始时选定的 8 个文件，后续新增方言字段涉及的 SqlQueryPagination 未事先备份；不伪称完整前态源码归档，两侧实际构建二进制均保留。历史 ROUND109 不包含本轮无排序／追加排序工作负载，故本轮重新建立同口径比较；历史证据仍对其原工作负载有效。


原始完整 BDN 表（包含 Error/StdDev/GC）：

前态：
```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.6466/22H2/2022Update)
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 8.0.30 (8.0.3026.36720), X64 RyuJIT AVX2

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
IterationTime=100ms  LaunchCount=1  WarmupCount=1  

```
| Method            | Filters | Mean      | Error     | StdDev    | Gen0   | Allocated |
|------------------ |-------- |----------:|----------:|----------:|-------:|----------:|
| **UnpagedFreeze**     | **1**       |  **6.822 μs** | **22.899 μs** | **1.2552 μs** | **1.2523** |  **24.02 KB** |
| UnorderedPage     | 1       |  5.987 μs |  4.391 μs | 0.2407 μs | 0.8954 |  17.08 KB |
| AppendedOrderPage | 1       |  6.214 μs |  3.043 μs | 0.1668 μs | 0.8886 |  17.13 KB |
| **UnpagedFreeze**     | **8**       | **11.541 μs** |  **6.490 μs** | **0.3557 μs** | **2.2124** |  **42.28 KB** |
| UnorderedPage     | 8       |  7.226 μs |  2.543 μs | 0.1394 μs | 1.3085 |  24.64 KB |
| AppendedOrderPage | 8       |  7.823 μs |  3.086 μs | 0.1691 μs | 1.2887 |   24.7 KB |

候选：
```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.6466/22H2/2022Update)
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 8.0.30 (8.0.3026.36720), X64 RyuJIT AVX2

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
IterationTime=100ms  LaunchCount=1  WarmupCount=1  

```
| Method            | Filters | Mean      | Error      | StdDev    | Gen0   | Gen1   | Allocated |
|------------------ |-------- |----------:|-----------:|----------:|-------:|-------:|----------:|
| **UnpagedFreeze**     | **1**       | **17.638 μs** | **91.6887 μs** | **5.0258 μs** | **1.2755** |      **-** |  **24.09 KB** |
| UnorderedPage     | 1       |  1.207 μs |  0.0890 μs | 0.0049 μs | 0.1779 |      - |   3.37 KB |
| AppendedOrderPage | 1       |  1.244 μs |  0.3613 μs | 0.0198 μs | 0.1889 |      - |   3.64 KB |
| **UnpagedFreeze**     | **8**       | **10.630 μs** | **10.1632 μs** | **0.5571 μs** | **2.2431** | **0.1020** |  **42.31 KB** |
| UnorderedPage     | 8       |  2.266 μs |  0.6855 μs | 0.0376 μs | 0.2743 |      - |   5.07 KB |
| AppendedOrderPage | 8       |  2.430 μs |  0.6353 μs | 0.0348 μs | 0.2843 |      - |   5.62 KB |


只重放已固定的二进制，将结果写入新目录，禁止用当前源码重建前态来伪造同一基线：

    dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round110Baseline/net8.0/Bing.Data.Sql.Benchmarks.dll --filter "*SqlPagingPlanningBenchmarks.UnorderedPage*" "*SqlPagingPlanningBenchmarks.AppendedOrderPage*" "*SqlPagingPlanningBenchmarks.UnpagedFreeze*" --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round110-before-reproduction

    dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round110Candidate/net8.0/Bing.Data.Sql.Benchmarks.dll --filter "*SqlPagingPlanningBenchmarks.UnorderedPage*" "*SqlPagingPlanningBenchmarks.AppendedOrderPage*" "*SqlPagingPlanningBenchmarks.UnpagedFreeze*" --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round110-after-reproduction

## 第 111 轮：CTE 冻结派生（2026-11-18）

生产修改之前构建 Round111Baseline，最终构建 Round111Candidate；同一新增 CTE 工作负载各成功测量一次，不复用 ROUND110 的非 CTE 数字、不覆盖原始样本。

| 方法 | 筛选数 | 前态分配 | 候选分配 | 前态 Mean | 候选 Mean |
| --- | ---: | ---: | ---: | ---: | ---: |
| CteCount | 1 | 26.61 KB | 1.49 KB | 19.563 μs | 161.3 ns |
| CtePage | 1 | 32.23 KB | 3.63 KB | 9.083 μs | 1,204.8 ns |
| CteFreeze | 1 | 49.17 KB | 49.42 KB | 9.091 μs | 9,640.0 ns |
| CteCount | 8 | 39.86 KB | 3.02 KB | 7.643 μs | 551.0 ns |
| CtePage | 8 | 47.02 KB | 5.34 KB | 11.852 μs | 1,505.0 ns |
| CteFreeze | 8 | 82.17 KB | 82.8 KB | 16.019 μs | 16,529.6 ns |

CteCount/CtePage 为已冻结计划派生；CteFreeze 包含 Builder 创建、CTE 克隆和首次冻结。前者分配下降约 88.6%～94.4%，后者增加约 0.25～0.63 KB。单参数 Count 前态 Error 75.348 μs；候选单参数 Freeze Mean 9.640 μs、Error 8.020 μs，前态 Mean 9.091 μs；耗时与不利值按原样保留，不推导端到端 CPU 或并发吞吐。

环境 .NET 8.0.30 / SDK 10.0.400 / Windows x64 / BDN 0.14.0，Optimize=true；每侧一次 InProcess ShortRun、1 次预热、3 次 100 ms 采样。Setup 先完整断言 SQL 与参数。测量不含数据库、网络、绑定和物化，B/op 不是保留堆或进程峰值。

重放测量（已有目录保留真实已测二进制；不修改／重建历史基线）：

```powershell
dotnet output/round111-baseline/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks.CteCount*' '*SqlPagingPlanningBenchmarks.CtePage*' '*SqlPagingPlanningBenchmarks.CteFreeze*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round111-before-replay
dotnet output/round111-candidate/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks.CteCount*' '*SqlPagingPlanningBenchmarks.CtePage*' '*SqlPagingPlanningBenchmarks.CteFreeze*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round111-after-replay
```

原始 CSV、采样配置、命令、二进制 SHA-256：output/round111-results.json 与 output/round111-before-bench、output/round111-after-bench。当前无需重跑；这里提供后续可复现入口。

## 第 112 轮：集合冻结派生（2026-11-18）

新增同语义 SetCount/SetPage/SetFreeze。SetCount/Page 从已冻结、未排序 UnionAll 追加计数或 Name Desc 分页；SetFreeze 单独包含 Builder 创建、分支及首次冻结。生产修改前已编译并测量实际基线，之后编译测量候选，未从最终源码反向恢复工作区。基线/候选的 harness 相同；初次 Setup 对合法同值参数共享的错误预期改用不同分支值，修正后各侧只一次成功采样，失败日志保留。

| 方法 | 筛选数 | 前态分配 | 候选分配 | 前态 Mean / Error | 候选 Mean / Error |
| --- | ---: | ---: | ---: | --- | --- |
| SetCount | 1 | 38.07 KB | 1.36 KB | 44.44 μs / 42.541 μs | 173.2 ns / 135.1 ns |
| SetPage | 1 | 34.91 KB | 3.73 KB | 20.38 μs / 30.768 μs | 1,327.8 ns / 1,747.7 ns |
| SetFreeze | 1 | 53.29 KB | 53.97 KB | 11.06 μs / 7.857 μs | 11,682.1 ns / 1,565.1 ns |
| SetCount | 8 | 63.78 KB | 4.05 KB | 13.39 μs / 4.222 μs | 807.7 ns / 407.9 ns |
| SetPage | 8 | 62.65 KB | 7.25 KB | 16.50 μs / 9.860 μs | 2,010.6 ns / 1,671.8 ns |
| SetFreeze | 8 | 107.5 KB | 108.63 KB | 24.94 μs / 7.382 μs | 25,644.4 ns / 21,811.2 ns |

四组派生 B/op 下降约 88.4%～96.4%；首次创建/冻结多 0.68～1.13 KB（约 1.1%～1.3%），两个 Mean 略升。完整误差与不利结果保留，不重复采样追求更好数字。环境 .NET 8.0.30/SDK 10.0.400/Windows x64/BDN 0.14.0，Optimize=true；InProcess ShortRun，1 次预热、3 次 100 ms 采样，无并发重负载。

Setup 完整断言 SQL 与参数。Count/Page 不含首次冻结、数据库、绑定/物化或请求并发；SetFreeze 包含首次创建和冻结。B/op 不是 retained heap、干净构建进程峰值；高 Error 和短进程内采样不支持方法级稳态 CPU 或吞吐结论。并发职责测试仅证明源/派生参数隔离。

原始 CSV 位于 output/round112-before-final-bench 与 output/round112-after-bench；两侧实际 DLL、选定前态源码和命令 SHA-256 位于 output/round112-results.json。output/round112-before.json 保存 7 个选定输入，API 清单另在修改前备份，不是完整仓库归档。

只重放已保存二进制并写入新目录，不覆盖历史证据；本轮已有成功证据，无需立即重跑：

```powershell
dotnet output/round112-baseline/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks.SetCount*' '*SqlPagingPlanningBenchmarks.SetPage*' '*SqlPagingPlanningBenchmarks.SetFreeze*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round112-before-replay

dotnet output/round112-candidate/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks.SetCount*' '*SqlPagingPlanningBenchmarks.SetPage*' '*SqlPagingPlanningBenchmarks.SetFreeze*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round112-after-replay
```

生产容量、方法级 CPU 与完整干净构建峰值仍需目标环境验证，不由本轮短基准替代。

## 第 113 轮：独立 owner 的构造与释放


新增 SqlResourceConstructionBenchmarks.Query/Writer/Multiple，构造真实执行基类并释放尚未使用的资源。相同 harness 在生产修改前编译并测量基线，修改后编译候选；每侧一次成功采样。新场景测得三个入口均少分配 48 B，符合移除 owner 外围具体门面和单独状态对象、改为 owner 内嵌值类型状态的变化。


| 入口 | 前态 B/op | 候选 B/op | 减少 | 前态 Mean / Error | 候选 Mean / Error |
| --- | ---: | ---: | ---: | --- | --- |
| Query | 888 B | 840 B | 48 B（5.4%） | 120.3 ns / 46.08 ns | 128.0 ns / 39.59 ns |
| Writer | 856 B | 808 B | 48 B（5.6%） | 128.6 ns / 77.33 ns | 123.1 ns / 52.95 ns |
| Multiple | 872 B | 824 B | 48 B（5.5%） | 116.6 ns / 154.54 ns | 122.3 ns / 81.87 ns |


环境为 Windows x64、.NET 8.0.30 / SDK 10.0.400 / BDN 0.14.0，Optimize=true，InProcess ShortRun、1 次预热、3 次 100 ms 测量。保留 Query/Multiple 较慢的 Mean 与所有 Error，不宣称 CPU 提速。场景包括入口 Guid、委托和构造/Dispose，排除连接打开、实际事务、SQL、绑定、物化及并发；不将 48 B 外推为整次请求或生产吞吐收益。B/op 不是保留堆、干净构建峰值或容量。

原始 CSV 在 output/round113-before-bench 与 output/round113-after-bench；实际基线/候选 DLL、选定前态输入、命令、失败日志与 SHA-256 见 output/round113-results.json。前态清单不是完整仓库归档；旧 ExecutionLease 测试夹具和三份纯注释文件在各自修改前补充留存，清单已注明时序。方法级 CPU、完整干净构建峰值和生产容量继续为 BLOCKED_EXTERNAL。


重放时使用已保存二进制和新的结果目录，不覆盖原始证据。本轮已经有成功样本，无需立即重复：

~~~powershell
dotnet output/round113-baseline/Bing.Data.Sql.Benchmarks.dll --filter '*SqlResourceConstructionBenchmarks*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round113-before-replay

dotnet output/round113-candidate/Bing.Data.Sql.Benchmarks.dll --filter '*SqlResourceConstructionBenchmarks*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round113-after-replay
~~~

## 第 114 轮：Skip-only 复用 offset

| 场景 | Filters | 前态分配 | 候选分配 | 前态 Mean / Error | 候选 Mean / Error |
| --- | ---: | ---: | ---: | --- | --- |
| SkipOnlyPage | 1 | 16.00 KB | 1.86 KB | 13.326 μs / 38.387 μs | 313.5 ns / 297.94 ns |
| SkipOnlyPage | 8 | 23.56 KB | 3.29 KB | 5.943 μs / 2.012 μs | 672.5 ns / 99.67 ns |
| SkipOnlyFreeze | 1 | 25.76 KB | 25.82 KB | 6.509 μs / 4.444 μs | 17,133.3 ns / 29,172.76 ns |
| SkipOnlyFreeze | 8 | 44.79 KB | 44.85 KB | 11.207 μs / 9.858 μs | 14,256.8 ns / 2,802.14 ns |

新增 SkipOnlyPage/SkipOnlyFreeze，Filters=1/8。首次冻结包含创建 Builder；Page 仅冻结后的规划，无数据库/物化/并发。两侧实际编译、同 harness、每侧一次成功 InProcessEmitToolchain ShortRun；Windows x64、.NET 8.0.30、SDK 10.0.400、BDN 0.14.0、Optimize=true，1 次预热、3 次 100 ms 测量。

Page 约少 86%～88% 分配；Freeze 多约 0.06 KB 且两个 Mean 更慢，全部误差保留。KB 是 CSV 舍入值，不能推出精确字节差；不代表 CPU 归因、峰值或生产容量。原始 CSV、两侧 DLL 哈希和命令在 output/round114-results.json。重放命令如下（候选使用 round114-candidate 与 round114-after-bench）：

```powershell
dotnet output/round114-baseline/Bing.Data.Sql.Benchmarks.dll --filter '*SqlPagingPlanningBenchmarks.SkipOnly*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round114-before-bench
```

## 第 115 轮：必要规划来源的存活成本

| 条件数 | 前态分配 | 候选分配 | 前态 Mean / Error | 首次候选 Mean / Error | 后续候选 Mean / Error |
| ---: | ---: | ---: | --- | --- | --- |
| 1 | 21.75 KB | 21.81 KB | 6.536 μs / 6.004 μs | 6.565 μs / 1.882 μs | 6.874 μs / 1.682 μs |
| 8 | 29.59 KB | 29.65 KB | 7.906 μs / 4.738 μs | 11.394 μs / 1.938 μs | 10.996 μs / 3.177 μs |

SqlFallbackPlanningBenchmarks.Page，Conditions=1/8，仅从冻结扩展计划派生第一个页面，Setup 校验完整 SQL/参数。一次实际修改前基线；先后两次候选均保留，后一次为保留非基类 Clone 边界后的重建，不按速度筛选。两次候选分配相同；表中保留全部 Mean/Error。

InProcess ShortRun；1 次预热、3 次 100 ms；Windows x64/.NET8.0.30/SDK10.0.400/BDN0.14.0/Optimize=true。最后虚方法修正仅进入动态过滤分支，该静态扩展基准未受影响，按 CIA 复用已测二进制证据；最终 Native 二进制另存身份。

增加约 0.06 KB 为必要规划来源保留成本，KB 是舍入值；平均耗时未改善，不能声称 CPU 或容量收益。没有数据库、保留堆或进程峰值测量。原始 CSV、命令与测量 DLL 哈希见 output/round115-results.json。重放命令（候选替换为 round115-candidate）：

```powershell
dotnet output/round115-baseline/Bing.Data.Sql.Benchmarks.dll --filter '*SqlFallbackPlanningBenchmarks*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round115-before-bench
```

## 第 116 轮：默认冻结 IR 复用

| 入口 / 条件数 | 前态分配 | 首次候选分配 | 最终候选分配 | 前态 Mean / Error | 首次候选 Mean / Error | 最终候选 Mean / Error |
| --- | ---: | ---: | ---: | --- | --- | --- |
| 首次终结 / 1 | 23.29 KB | 23337 B | 23553 B | 4.605 μs / 1.728 μs | 4658.3 ns / 1103.72 ns | 21308.2 ns / 59710.78 ns |
| 重复终结 / 1 | 7.48 KB | 800 B | 800 B | 1.662 μs / 1.882 μs | 135.5 ns / 123.55 ns | 133.9 ns / 54.79 ns |
| 首次终结 / 8 | 42.25 KB | 42753 B | 42753 B | 10.068 μs / 18.958 μs | 9596.0 ns / 9161.67 ns | 9966.8 ns / 2332.71 ns |
| 重复终结 / 8 | 13.28 KB | 800 B | 800 B | 2.746 μs / 2.161 μs | 128.4 ns / 38.65 ns | 133.0 ns / 107.07 ns |

相同 SqlQueryPlanReuseBenchmarks harness，实际前态编译于生产修改前；一次前态测量和两次候选，最终候选因嵌套过滤修复重建；未反复测量筛选结果。FirstTerminal 包含构造与冻结，RepeatedTerminal 预热后只创建下一次计划；无 DB。重复终结分配明显降低，首次入口 Mean 没有明确改善。保留全部误差，KB/B 显示单位随 BDN 表格自动选择。

Windows x64/.NET8.0.30/SDK10.0.400/BDN0.14.0；InProcess ShortRun，预热 1、3 次 100 ms、Optimize=true。不代表保留堆、端到端 DB 吞吐、CPU 归因、干净构建峰值或生产容量。命令收据/原始 CSV/二进制身份见 output/round116-results.json。重放命令（候选替换为 round116-candidate）：

```powershell
dotnet output/round116-baseline/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryPlanReuseBenchmarks*' --inProcess --job short --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round116-before-bench
```

最终检查增加 CTE/Union/嵌套 CTE 后代动态过滤的 3 个 RED。只排除缓存后仍观察到旧值，随后在需要渲染的准备路径递归检查后代，并在外层不会自行创建过滤快照时先隔离后代。静态命中路径不递归检查。最终新增 1 个 SQLite 守卫，验证后代过滤每次取新值，同次 Count/Data 共用值。三类职责用例在缓存修正前均失败，最终全部通过；补充 SQLite 初稿 using 位置及既有 With Recursive 输出期望错误保留日志，不算产品 RED。

因最终修复改变首次终结路径，按 CIA 重建独立最终候选并额外诊断一次；唯一实际修改前基线不重跑，两个候选全部保留，未按快慢筛选。最终 1 条件首次终结 Mean 为 21.3082 μs（Error 59.71078 μs），比前态更慢且误差很大；不将它隐去或解释为已改善。重复终结分配稳定为 800 B；本轮没有足够证据承诺首次终结耗时或端到端 CPU 收益，不继续重复短测。

## Round117 追加说明：首次终结结构归属

使用同一 `SqlQueryPlanReuseBenchmarks` harness，修改前基线与候选各一次成功短测。首次终结 1/8 条件的分配为 23337→23329 B / 42753→42745 B；重复终结保持 800 B。新增 `FirstTerminalRetainingQuery` 也少 8 B。完整六组 Mean/Error、实际命令和二进制身份见 `ai_docs/tasks/20261107-sqlquery-command-aot-design/round117.md` 与 `output/round117-results.json`。

该测量验证分配，没有量化驻留堆、端到端吞吐或方法 CPU。保留更慢的首次终结 Mean，不为短测误差重复重跑；外部完整干净构建峰值和生产容量继续待验证。


## Round118：共享 CTE 定义的创建与渲染分配（2026-11-18）

新增 SqlCteDefinitionBenchmarks.CreateDefinition / RenderSharedDefinition，Parameters=1/8；Setup 完整核对 SQL、参数名和值。实际生产修改前基线和候选各独立构建、成功测量一次，内建片段的重复渲染由 3096/14680 B 降至 2192/11696 B，创建分配维持 32 B。创建 Mean 3.931→4.867 ns、4.543→5.057 ns，重复渲染 Mean 592.407→428.261 ns、4078.415→2425.959 ns，所有 Error 很大，完整四组 Mean/Error 见 round118.md。

自定义片段的 Freeze 从每次消费移到构造，本基准未量化它的构造开销；不声明 CPU、驻留堆、端到端吞吐或容量收益。基准初次构建发现既有 System 导入缺失，前态与候选均使用相同 ImplicitUsings=enable 命令属性，不修改无关文件，失败日志保留。Windows x64/.NET8.0.30/SDK10.0.400/BDN0.14.0，InProcess ShortRun，预热 1、3 次 100 ms，Optimize=true，无 DB。

原始 CSV、实际命令、TRX 与两个输出目录的二进制 SHA-256 见 output/round118-results.json。重放示例（候选替换为 round118-candidate / round118-after-bench）：

~~~powershell
dotnet output/round118-baseline/Bing.Data.Sql.Benchmarks.dll --filter '*SqlCteDefinitionBenchmarks*' --job short --inProcess --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts output/round118-before-bench
~~~

## 2026-11-18 Round126：自动生成完整 WhereIf 查询形状

新增 SqlGeneratedQueryTemplateBenchmarks：同一候选、同一 SQLite Provider/共享映射服务、当前请求新建查询和值集合，比较逐操作生成桥接与整条热模板。Setup 先断言完整 SQL、参数次序和值，再断言模板跨请求 SQL 引用复用。ToSql 与 BoundSnapshot 各覆盖 IN 1/8/32，共 12 格，未调用数据库。

本机 .NET 8.0.30/x64，预热 2048，每组 10000 次，共 5 组。下表为中位数 B/次；不是历史候选 before/after，也未包含编译器在用户方法中构造的表达式树、冷 miss 或数据库 I/O。

| IN 数量 | ToSql 桥接→模板 B/次 | 绑定快照桥接→模板 B/次 |
| --- | ---: | ---: |
| 1 | 56105→9200 | 57737→9216 |
| 8 | 69945→16832 | 76137→16904 |
| 32 | 111769→40281 | 133234→40545 |

六个对照减少约 64%～84% 的托管分配，仍存在请求状态、数组/装箱与参数快照分配。原始 elapsed 样本只作本机观察，不替代方法级 CPU、正式多进程 BenchmarkDotNet 统计、完整干净构建进程树峰值或生产容量。缓存 256 为每生成调用点/共享映射解析器上限，不是总内存限制。

复现（先已有依赖还原，PowerShell，UTF-8）：

~~~powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Round126 --no-restore -p:ImplicitUsings=enable -p:Optimize=true -p:DefineConstants=RELEASE -p:UseSharedCompilation=false -m:1
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round126/net8.0/Bing.Data.Sql.Benchmarks.dll --generated-query-template-probe --output output/round126-allocation-data.json
~~~

既有基准源文件未显式引用 System，因此自定义 Configuration 需要上述 ImplicitUsings 参数；首次失败日志仍保留，未改无关源文件。正式 BDN 场景带 FormalHost（3 launch/6 warmup/15 iteration），可用相同构建产物 --filter '*SqlGeneratedQueryTemplateBenchmarks*' 在受控宿主运行；本轮未运行该正式作业。候选/样本哈希、全部 12 格与步骤收据见 output/round126-results.json、output/round126-allocation-data.json。只运行一次成功采样，没有筛选更快轮次。
