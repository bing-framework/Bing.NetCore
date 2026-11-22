# SQL 元数据与执行资源测量

## PostgreSQL 并发读（Round155）

`--capacity-probe` 通过真实 PostgreSQL 比较运行时 Builder 和共享查询模板。按 1、16、64、256 并发采集成功吞吐、延迟分位、失败数、客户端 CPU、分配与 GC。运行方法、报告口径和已验证范围见 [容量工具说明](../../../eng/perf/README.md#postgresql-并发读验证)。

## 托管方法归因（Round136）

现有 `SqlBuilderStateProbe-v1` 可由单用途 EventPipe 工具在运行托管入口前接入采样，保留原始 nettrace、逐样本方法栈、Self/Inclusive CSV 和重放收据。工具引用既有基准输出的 Diagnostics 程序集，无新增包；业务和探针负载不变。最终本机确认 1133 个 Managed 样本、356 个方法、0 丢事件/缺失/未解析，7 职责和 3 真实集成测试通过。

构建/采样/重放命令见 [工具说明](../../../eng/perf/README.md)，详细口径和结果见 [Round136](../../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round136.md)。此采样包含启动/JIT/探针搭建和旧 full-tree-clone 对照，只表示当前固定负载的托管方法归因，不是稳态 Query、原生/内核全栈或生产容量验证；原有 before/after 基线保持原口径。

## 干净构建进程树峰值（Round135）

```powershell
pwsh -NoProfile -File eng/perf/Invoke-SqlCleanBuildPeak.ps1
```

新工具在独立源码副本内，从没有 bin/obj/.codex-build 和工程同名生成 XML 文档的状态构建本工程的完整项目引用图。保留构建配置、EditorConfig、源码逐文件摘要、完整日志与入口程序集身份；包含 restore，复用宿主包缓存。Windows Job 在根进程恢复前绑定，记录全部子孙（包括短命进程）的同时提交内存峰值及累计 CPU 时间；关闭编译服务器和 MSBuild 节点复用。超时终止整棵树，失败不转成成功。

指标 `WindowsJobCommittedBytes` 是提交内存，不是工作集/RSS；累计 CPU 时间不等于方法级归因。该工具不修改原有性能基线，不运行 SQL 请求，不配置发布阈值。参数、两组职责测试和报告字段见 [工具说明](../../../eng/perf/README.md)，本轮测量及未验收范围见 [Round135](../../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round135.md)。默认构建范围是本工程及其引用项目，不是整个仓库解决方案。

## 默认构建与基准发现回归

从仓库根目录运行：

```powershell
pwsh -NoProfile -File eng/ci/tests/Invoke-SqlBenchmarkSmoke.Tests.ps1
```

该集成测试沿用项目默认配置构建，不指定 `-f`，不传全局 `ImplicitUsings=enable/disable`，随后从实际编译产物枚举资源构造的 Query/Writer/Multiple 与自动查询模板的四个基准方法。工程在导入公共配置前声明 `TargetFramework`，使默认构建和显式选择 net8.0 的构建均能读取同一框架配置，后续复现不再需要额外开启隐式导入。可指定 `-Configuration <名称>`、`-ResultsDirectory <目录>`；仅已恢复依赖时使用 `-NoRestore`。

脚本从 MSBuild 读取 `TargetPath`，在新建的 run 目录保留构建、发现、并发计量测试和离线门控日志。七个方法清单和所有离线测试均须通过。收据记录 `evidenceKind=BuildAndDiscovery`、`performanceVerified=false`。同一回归在 [SQL CI](../../../.github/workflows/sql-aot.yml) 执行并上传收据。下文历史命令中的 `ImplicitUsings=enable` 是当时的构建补丁。

## Round122 生成键 Insert/Returning 分配

SqlGeneratedKeyInsertBenchmarks 使用同一生成模型、键策略、静态参数绑定器和共享映射计划，每次按真实执行路径创建独立 Insert Builder。UseGeneratedSql 切换完整生成 SQL；DefaultValues 区分普通写列和仅身份键。无数据库 IO，不宣称方法 CPU 或生产容量结果。

~~~powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj --no-restore -c Round122Perf -p:ImplicitUsings=enable -p:UseSharedCompilation=false -p:Optimize=true -m:1 -nr:false
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round122Perf/net8.0/Bing.Data.Sql.Benchmarks.dll --generated-key-insert-probe --output output/round122-key-allocation.json
~~~

.NET 8.0.30 Windows x64，预热 2048 次，每格 5×10000 次：普通写列 8056→5520 B/次；DEFAULT VALUES 3664→2440 B/次。探针先验证完整 SQL、常量引用、Returning 和静态绑定。是同候选分支比较，不覆盖固定历史 baseline，非正式 BenchmarkDotNet 门禁。原始样本及二进制身份见任务 Round122 收据。数据与包装器收据应使用不同文件名，避免互相覆盖。

## Round121 完整生成 CRUD SQL

`SqlGeneratedMutationBenchmarks` 在同一候选内比较完整生成 SQL 与逐次渲染，两个分支都使用相同生成 Getter、静态绑定器、列映射、实体值及独立预热计划。覆盖单实体 Insert/Update/Delete 的完整命令构建及参数快照，不包含数据库 IO，也不把兼容路径当作历史版本基线。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj --no-restore -c Round121Perf -p:ImplicitUsings=enable -p:UseSharedCompilation=false -m:1 -nr:false
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round121Perf/net8.0/Bing.Data.Sql.Benchmarks.dll --generated-mutation-probe --output output/round121-allocation.json
```

探针先检查完整 SQL、常量引用复用及静态绑定器，再预热 2048 次，每格 5 个样本、每样本 10000 次操作，记录当前线程托管分配。生成 SQL 关闭/开启分别为 Insert 10120/7040 B、Update 11984/9176 B、Delete 8192/6368 B；这是 .NET 8.0.30 Windows x64 本机诊断，原始样本和二进制身份见任务 Round121 收据。`ImplicitUsings=enable` 沿用现有 Benchmark 构建方式，项目已有其他文件依赖该开关。没有执行正式 BenchmarkDotNet Job，不宣称 CPU、并发吞吐、进程峰值或容量门槛通过。

`SqlMetadataBenchmarks` 提供 BenchmarkDotNet 场景。固定次数的诊断探针可通过
`--generated-runtime-probe`、`--generated-allocation-probe`、`--generated-width-probe` 和 `--builder-state-probe` 运行，
均以 `--output <文件>` 输出 UTF-8 JSON。

## Round133 Getter 缓存与严格 AOT 边界

本轮修正首次带空白属性名时的 Getter 键规范化、保留会因实体数据抛错的已创建 Getter，并将单行 Insert/Update、批量 Update 和生成键 Insert 统一通过映射计划读取；严格模式缺少匹配的生成 Getter 时拒绝回退到运行时反射缓存。既有完整生成 SQL 稳态基准已使用生成 Getter 与静态参数绑定，不经过该反射回退缓存，因此这些修复不能解释为常见稳态路径的 CPU 或分配收益。SQLite CRUD Native 格增加 `IL2072` 错误门禁与严格读取标记；发布运行证据以 Round133 任务记录为准。本轮没有运行 Benchmark，也不报告 CPU、吞吐、构建峰值或生产容量结论。

2026-11-07 Round 4 复用原有 v1 工作负载，固定 warmup、采样和迭代数，不修改计时区间：

- 结果绑定：实际 SQLite 查询，2/8/32 列和 1/32/256 行，比较 Compatible 与 GeneratedOnly。
- 参数快照：深度 6，以及宽度 1/8/32/128 的 CTE 树；宽树中每个子查询的参数与根参数冲突，
  能直接覆盖逐步增长的目标参数集合及元数据克隆成本。
- 同时核对 SQL/参数稳定性和子树 Clone 次数，避免通过减少隔离边界改变语义。

本轮前后原始样本及四个程序集的 SHA-256 位于
`ai_docs/tasks/20261107-sqlquery-command-aot-design/artifacts/fix-round-4/`。
详细结论以任务 `performance.md` 的 Round 4 为准。托管 B/op 不等于进程峰值或生产容量；
固定诊断的耗时不能替代正式 BenchmarkDotNet 性能门槛。

## 独立 Builder 与结果读取 A/B 计时

`framework/tests/Bing.Data.Sql.Benchmarks/paired-probe.mjs`
使用两个独立的完整 .NET 输出目录，顺序调用原有 `--builder-state-probe` 和
`--generated-width-probe`。两个目录需包含相同版本的 v1 探针驱动及各自候选的完整依赖；脚本不构建候选，也不复制或修改 C# 工作负载。

从仓库根目录运行。输出目录的父目录必须已存在，输出目录本身必须为空或尚不存在，并且需位于两个候选目录之外：

```powershell
node framework/tests/Bing.Data.Sql.Benchmarks/paired-probe.mjs --before-dir ai_docs/tasks/20261107-sqlquery-command-aot-design/artifacts/fix-round-5/before --after-dir ai_docs/tasks/20261107-sqlquery-command-aot-design/artifacts/fix-round-5/after --output-dir ai_docs/tasks/20261107-sqlquery-command-aot-design/artifacts/fix-round-5/paired-run
```

每种探针固定执行两对独立进程：`before → after`，随后 `after → before`；不会追加第三次运行。Builder 每进程沿用 2 次预热、5 个样本、32 次草稿操作和 100 次重复渲染；width 每进程沿用 32 次预热和 5 个样本。每种探针的每个形状/操作/模式每个候选汇集 10 个原始样本。报告包含 median、min、max、按 `(n - 1) * 0.95` 线性插值得到的 p95、逐进程原始 JSON、日志、PID、命令、.NET FrameworkDescription、候选程序集与依赖哈希、机器/CPU、Windows 当前电源计划，以及已发现的 CPU profiler 命令（仅盘点、不运行）。width 探针另保留其自身的程序集身份文件。

该探针是诊断分布，不构成性能门槛或生产容量证明。脚本要求八个进程使用相同 .NET FrameworkDescription，并核对 before/after 的工作负载形状、参数/行列规模及操作数；脚本不控制 CPU 频率、温度、亲和性或机器其他负载。托管 B/op 不代表进程峰值内存。不得仅据此宣称门槛通过或默认策略应变更。

## Round 6 内部精简验证

命令包装、冻结状态和参数预检入口精简直接复用既有 v1 场景，无需新增同义 C# Benchmark：
runtime 的18组覆盖 Raw/事务路径，allocation 的24组覆盖 LambdaWrapped/LambdaBound/First/Single，
Builder 的21组覆盖草稿修改与深度6、宽度1/8/32/128的重复渲染。
每组预热、样本和迭代数保持不变；参数预检仍需保留失败原子性、第三方Clone和方言转换合同。

本轮原始数据、固定二进制身份及范围位于
`ai_docs/tasks/20261107-sqlquery-command-aot-design/artifacts/fix-round-6/`，
以任务 performance.md 的 Round 6 为当前结论。Raw/Fluent 大多减少32B/次，宽128的分配没有回退。
Builder基线样本直接复用Round 5 after；当前单次诊断不替代交替耗时分布或正式性能门槛。

## Round 16 Mutation 计划 Getter

`SqlGeneratedAccessBenchmarks` 新增 `GeneratedPlanGetterWarm`，用于比较 Mutation Plan
预构建生成 Getter 与兼容 Getter 缓存的热路径。该场景只测属性读取委托，不包含 SQL
构建、参数工厂或数据库执行；必须与 `CompatibleGetterWarm`、`GeneratedGetterWarm`
一起解释，不能单独推导端到端吞吐或常驻内存结论。

## Round 17 查询形状与二进制参数

`SqlQueryShapeBenchmarks` 比较稳定形状命中与 512 个路由轮换造成的持续未命中，覆盖一个和四个可选片段；`SqlQueryShapePayloadBenchmarks` 测量 4 KiB / 1 MiB 二进制参数的独立快照。两者都只测模板渲染，不含数据库调用，且形状缓存容量是每个模板 256 项。

`ScopedShape` 另行测量固定形状在指定作用域下的 SQL 参数改名及请求快照；它只覆盖普通参数名和单线程渲染。Round 23 的工作负载与分配收据见任务 `performance.md`。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:OutputPath=.codex-build/bin/round17-final4-benchmark/
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round17-final4-benchmark/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryShapeBenchmarks*' --inProcess --warmupCount 3 --iterationCount 7 --invocationCount 1048576 --unrollFactor 16 --artifacts framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/round17-shape-repro
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round17-final4-benchmark/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryShapePayloadBenchmarks*' --inProcess --warmupCount 2 --iterationCount 5 --invocationCount 256 --unrollFactor 16 --artifacts framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/round17-payload-repro
```

本机结果和限制见任务 `performance.md`。二进制用例迭代时间不足，分配量可用于诊断，耗时不可作为性能门槛；所有结果均不能代表并发吞吐或生产容量。

## Round 18 生成比较常量

`SqlGeneratedConstantBenchmarks` 隔离测量预构造表达式的常量读取、数值逐次装箱和只读字段复用。该用例不测表达式树创建，也不测完整查询与数据库；只读字段有每个生成调用点的常驻成本。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:OutputPath=.codex-build/bin/round18-final5-benchmark/
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round18-final5-benchmark/Bing.Data.Sql.Benchmarks.dll --filter '*SqlGeneratedConstantBenchmarks*' --inProcess --warmupCount 3 --iterationCount 7 --invocationCount 1048576 --unrollFactor 16 --artifacts framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/round18-constant-repro
```

本机迭代时间不足，分配结果可用于局部诊断，计时不可作为性能门槛。

## Round 19 生成查询描述

`SqlGeneratedDescriptorBenchmarks` 只模拟单列比较的固定类型数组、来源名数组和列描述逐次创建与静态复用，不包含表达式树、SQL 构造、数据库或并发，也不测静态描述的常驻内存。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:OutputPath=.codex-build/bin/round19-benchmark/
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round19-benchmark/Bing.Data.Sql.Benchmarks.dll --filter '*SqlGeneratedDescriptorBenchmarks*' --inProcess --warmupCount 3 --iterationCount 7 --invocationCount 1048576 --unrollFactor 16 --artifacts framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/round19-descriptor-repro
```

本机迭代时间不足，分配结果仅用于局部诊断，计时和比值不作为性能门槛。详见任务 `performance.md`。

## Round 24 共享参数槽

`SqlQueryShapeBenchmarks.SharedSlot` 使用两个同名但默认值不同的片段，并在每次渲染时
显式提供当前请求值，测量已缓存形状的分配。它始终只有一个共享槽，`OptionalCount` 的两个
参数值不会改变该工作负载；不应将其视作两种不同查询。`StableShape` 对照两个或五个独立参数。
这些场景不覆盖冷形状编译、数据库执行、并发或大默认值比较。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:OutputPath=.codex-build/bin/round24-repro/
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round24-repro/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryShapeBenchmarks.SharedSlot*' --inProcess --warmupCount 2 --iterationCount 3 --invocationCount 131072 --unrollFactor 16 --artifacts framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/round24-shared-repro
```

本轮前后对照、环境及限制见任务 `performance.md`；短迭代计时不作为性能结论。

## Round 31 固定尾部形状

`SuffixStableShape` 测量可选条件之后带 `ORDER BY ... LIMIT` 的缓存命中，
`SuffixChurningRoutes` 使用 512 个路由触发超过模板 256 项上限的持续未命中。
二者包含一个或四个可选条件以及独立的尾部参数，只测模板渲染，不包含数据库和并发。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:OutputPath=.codex-build/bin/round31-benchmark/
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round31-benchmark/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryShapeBenchmarks.Suffix*' --inProcess --warmupCount 2 --iterationCount 3 --invocationCount 16384 --unrollFactor 16 --artifacts framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/round31-shape-suffix-final
```

分配数据及其限制见任务 `performance.md`；该短时探测不能用于判断吞吐或生产容量。

## Round 35 有序片段形状

`OrderedStableShape` 和 `OrderedChurningRoutes` 分别测量固定分组两侧包含可选条件的形状命中与路由持续未命中；
仍使用 1 或 4 个全部启用的可选条件。旧 `Suffix*` 工作负载继续用作相同语义的前后对照。
结果不包含模板构造、数据库和并发；新增 API 在旧版本没有等价工作负载。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:OutputPath=.codex-build/bin/round35-after/
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round35-after/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryShapeBenchmarks.Suffix*' '*SqlQueryShapeBenchmarks.Ordered*' --inProcess --warmupCount 2 --iterationCount 3 --invocationCount 16384 --unrollFactor 16 --artifacts framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/round35-after-results
```

分配数据和限制见任务 `performance.md`。短迭代结果不用于判断延迟、吞吐或生产容量。

## Round 46 整数读取与派生实例访问

`SqlIntegerReaderBenchmarks` 在相同的真实 SQLite Reader 和 Int64 值上，对比原有
`GetValue` / `Convert.ChangeType` / object Setter 与生成式 `GetInt64` / checked 直接赋值的形态。
委托由基准手写，与生成器输出等形；实际生成代码由生成器职责测试和 Native SQLite Smoke 验证。
本场景只测单列重复读取，不包含查询、绑定、对象创建、NULL、自定义转换器或并发。
`SqlGeneratedAccessBenchmarks.GeneratedPlanDerivedGetterWarm` 补充登记基类的派生实例 Getter，
与同候选 `GeneratedPlanGetterWarm` 对照；二者仍返回 object，整数结果仍会装箱。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:OutputPath=.codex-build/bin/round46-benchmark/
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round46-benchmark/Bing.Data.Sql.Benchmarks.dll --filter '*SqlIntegerReaderBenchmarks*' '*SqlGeneratedAccessBenchmarks.GeneratedPlan*' --inProcess --warmupCount 2 --iterationCount 3 --invocationCount 1048576 --unrollFactor 16 --artifacts framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/round46-reader-results
```

分配结果仅用于局部诊断，不能推导完整查询的延迟、吞吐或生产容量。映射缓存键、SQL 格式化及
Builder 克隆没有变化，不重复它们的历史基准。

## Round 48 生成参数与字典绑定

`SqlModelParameterBindingBenchmarks` 测量实际 `DefaultSqlParameterBinder` 的登记生成模型和严格/首选
字典路径，宽度为 1/8/32。修改前后使用相同的映射解析器、参数工厂、输入值和调用次数；生成模型
Getter 的值类型装箱属于工作负载本身。候选运行：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false /p:OutputPath=.codex-build/bin/round48-after/
dotnet framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/bin/round48-after/Bing.Data.Sql.Benchmarks.dll --filter '*SqlModelParameterBindingBenchmarks*' --inProcess --warmupCount 2 --iterationCount 3 --invocationCount 16384 --unrollFactor 16 --artifacts framework/tests/Bing.Data.Sql.Benchmarks/.codex-build/round48-after-results
```

本轮修改使生成模型每次绑定少一次数据库上下文解析，并去掉临时属性数组；字典非空绑定预分配最终参数数组并复用一次上下文，空字典不解析上下文。结果仅用于局部分配诊断：短迭代低于 100 ms，不能据此推断吞吐、CPU 归因、进程峰值或生产容量。前后明细与 SHA-256 见任务 `performance.md` 的 Round 48。

## Round 49 结构化参数映射上下文

本轮把 `ISqlParameterMap` 绑定路径改为一次解析并复用 `DatabaseContext`。现有基准没有与该路径等价的映射项、列元数据和参数定制器工作负载，因此不新增不可比的 BenchmarkDotNet 场景，Round 49 性能基准记为 `NOT_APPLICABLE`。上下文解析次数由 Core 职责测试直接断言，完整 SQL 由 SQLite 集成测试验证；这两项证据不代表端到端吞吐或生产容量。

## Round 50 生成式标量强类型读取

`SqlStaticResultReader.ReadValue` 对字段类型与目标类型完全一致的 `short`、`bool`、`float`、`decimal`、`DateTime`、`Guid` 直接调用 `DbDataReader` 类型化 Getter；类型不匹配仍走原有 `GetValue`/转换回退。现有 BenchmarkDotNet 没有与这六类标量读取、真实字段类型和装箱计数等价的工作负载，因此本轮标记 `NOT_APPLICABLE`，使用 `SqlStaticResultReaderPerformanceTest.Bind_DefaultScalarTypes_ShouldUseTypedProviderGetters` 的 `CountingReader.BoxedReads=0` 作为局部证据，不推导端到端吞吐或生产容量。

## Round 51 整数与字节精确读取

`SqlStaticResultReader.ReadValue` 继续补齐常用精确字段：`int` 使用 `GetInt32`，`byte` 使用 `GetByte`；`long → int` 的 SQLite 受检转换和其他类型回退不变。现有基准没有与该内部 `int`/`byte` Reader 读取和装箱计数等价的工作负载，因此本轮标记 `NOT_APPLICABLE`，使用 `SqlStaticResultReaderPerformanceTest.Bind_DefaultIntegerAndByteScalars_ShouldUseTypedProviderGetters` 的 `CountingReader.BoxedReads=0` 作为局部证据。

## Round 52 生成查询整数物化兼容

本轮修复 Source Generator 生成 Reader 对 SQLite 风格 `Int64` 整数列的物化兼容，使用 `GetInt64` 加 `checked` 转换覆盖 `int`、`short`、`byte` 及枚举底层整数类型。当前 BenchmarkDotNet 没有与生成查询 Reader、动态程序集和字段存储类型完全等价的工作负载，因此标记 `NOT_APPLICABLE`；生成器回归测试 `Generate_WhenIntegerColumnUsesInt64Storage_ShouldConvertIntegerProperties` 只作为局部语义证据。

## Round 53 生成查询窄整数与字符物化

本轮只扩展 Source Generator 的结果 Reader 标量发射，没有可直接复用的 BenchmarkDotNet 方法。无符号/窄整数的精确字段读取还受 `DbDataReader` API 限制，强行加入基准会把 Provider 类型、动态程序集加载和通用转换混在一起，因此标记 `NOT_APPLICABLE`。TDD 证据采用生成器全量 **304/304**、窄整数/字符物化回归和 Native AOT GeneratedSmoke；这些证据不代表端到端吞吐或生产容量。

## Round 54 基数终结生成入口

本轮只修正 Source Generator 对 `First`、`FirstOrDefault`、`Single` 及异步版本的调用路由，运行时执行器没有新增热路径。现有 BenchmarkDotNet 没有可隔离的编译期发射工作负载，因此标记 `NOT_APPLICABLE`；TDD 证据采用基数终结生成契约测试和生成器全量 **304/304**，不代表端到端吞吐或生产容量。

## Round 55 模型结果 Reader 的字符与枚举路径

本轮只扩展 Source Generator 的模型结果 Reader 标量发射，没有可直接复用的 BenchmarkDotNet 方法。`char` 和有符号枚举的类型化 Getter、Int64 受检转换与兼容回退混合了 Provider 存储类型，强行加入基准会制造不可比的端到端假设，因此标记 `NOT_APPLICABLE`。TDD 证据采用模型职责测试 **50/50**、生成器全量 **306/306**、SQLite 合同各 **20/20** 和 Native AOT GeneratedSmoke；这些证据不代表端到端吞吐或生产容量。

## Round 56 收窄 Lambda 终结映射边界

本轮移除已删除 Lambda `SingleOrDefault` 的不可达生成分支，只保留公开的 `First`、`FirstOrDefault`、`Single` 及异步版本映射；没有新增运行时热路径。没有与“死映射删除”同构的 BenchmarkDotNet 场景，标记 `NOT_APPLICABLE`；生成器定向 **1/1**、全量 **306/306** 通过，复用既有 Native AOT/SQLite 证据。

## Round 57 模型结果 Reader 的时间类型路径

本轮只补齐 Source Generator 模型结果 Reader 对 `DateTimeOffset`、`TimeSpan` 及可空版本的精确字段检查和显式转换。`DbDataReader` 没有统一的时间类型化 Getter，因此没有新增与真实 Provider 存储类型、动态程序集加载和对象读取同构的 BenchmarkDotNet 场景，标记 `NOT_APPLICABLE`。TDD 定向 **1/1**、枚举回归 **1/1**、生成器全量 **307/307** 通过；这些证据不代表端到端吞吐、并发容量、CPU 归因或进程峰值。

## Round 58 模型结果 Reader 的二进制路径

本轮为 `SqlModelGenerator` 补齐 `byte[]` 与 `byte[]?` 的生成 Setter，并加入精确字段类型检查。由于 `DbDataReader` 没有统一的二进制类型化 Getter，匹配路径仍使用一次 `GetValue` 和显式目标类型转换；这不增加反射或表达式编译，也不宣称二进制读取零装箱。没有与模型 Reader、真实 Provider 二进制字段和动态程序集加载完全同构的 BenchmarkDotNet 工作负载，因此标记 `NOT_APPLICABLE`。TDD 定向 **1/1**、生成器全量 **308/308** 通过；这些证据不代表端到端吞吐、并发容量、CPU 归因、构建峰值或生产容量。
## Round 59 模型结果 Reader 的窄整数路径

本轮补齐 `SqlModelGenerator` 对直接 `sbyte`、`ushort`、`uint`、`ulong` 及可空属性的生成 Setter。`Int64` 存储继续使用 `GetInt64` 和 `checked` 转换，精确窄整数存储使用 `GetValue` 和显式目标类型转换。现有基准没有与模型 Reader、Provider 字段存储类型和动态程序集加载完全同构的工作负载，因此标记 `NOT_APPLICABLE`。TDD 定向 **4/4**、生成器全量 **312/312** 通过；这些证据不代表端到端吞吐、并发容量、CPU 归因、构建峰值或生产容量。

## Round 60 模型结果 Reader 的 DateOnly/TimeOnly 路径

本轮为 `DateOnly`、`DateOnly?`、`TimeOnly`、`TimeOnly?` 补齐模型结果 Reader 的精确字段分支。由于 `DbDataReader` 没有统一的日期/时间 Getter，匹配字段仍使用一次 `GetValue`；测试只验证静态 Setter、类型检查和兼容回退，不宣称零装箱或端到端性能提升。没有同构的 BenchmarkDotNet 场景，本轮标记 `NOT_APPLICABLE`。TDD 定向 **1/1**、生成器全量 **313/313** 通过。

## Round 61 模型结果 Reader 的无符号枚举路径

本轮为 `sbyte`、`ushort`、`uint`、`ulong` 底层枚举及可空枚举补齐生成 Reader。由于这些类型没有统一的 `DbDataReader` 类型化 Getter，匹配字段仍使用一次 `GetValue` 并完成底层类型转换；没有同构的 BenchmarkDotNet 工作负载，本轮标记 `NOT_APPLICABLE`。TDD 定向 **4/4**、生成器全量 **317/317** 通过。

## Round 62 模型结果 Reader 的原值路径

本轮为显式 `[SqlResult]` 模型的 `object`/`object?` 属性生成直接 `GetValue` Setter，避免通用转换分派；`DBNull` 继续由上层读取器跳过。没有与真实 Provider 原值和动态模型 Reader 完全同构的 BenchmarkDotNet 场景，标记 `NOT_APPLICABLE`。TDD 定向 **1/1**、生成器全量 **318/318** 通过；不推导端到端吞吐、CPU 归因、构建峰值或生产容量。

## Round 63 模型结果 Reader 的字段类型缓存

本轮让生成 Reader Setter 在一次赋值内只调用一次 `GetFieldType`，复用字段类型完成 `Int64` 受检转换和精确类型检查。没有与真实 Provider 元数据访问成本、模型物化和并发请求完全同构的 BenchmarkDotNet 场景，标记 `NOT_APPLICABLE`。TDD 定向 **1/1**、生成器全量 **319/319** 通过；不推导端到端吞吐、CPU 归因、构建峰值或生产容量。

## Round 66 严格字典诊断参数路径

本轮修复严格生成模式下 `GetSqlParams(object, ...)` 的字典诊断读取。现有 `SqlModelParameterBindingBenchmarks`
只覆盖执行绑定，不覆盖诊断入口，因此没有可比的 BenchmarkDotNet 场景，记为 `NOT_APPLICABLE`。
目标测试与 Dapper.Core 全量回归分别在 net6.0/net8.0 通过 **7/7** 与 **241/241**；这些结果不推导端到端
吞吐、CPU 归因、构建峰值或生产容量。

## Round 67 原生文本计划参数冻结

本轮没有新增 BenchmarkDotNet 场景。SqlQueryPlan.Parameters 现在对公开字典、数组和 SqlParam
容器返回深拷贝，复制分配是冻结语义的必要成本；现有基准没有“公开参数被修改后仍保持计划状态”
的同构 before/after 工作负载，记为 NOT_APPLICABLE。计划测试 net6.0/net8.0 各 8/8 通过，
生成器全量隔离输出 net8.0 319/319 通过；不据此推导端到端吞吐、方法级 CPU、构建进程峰值或
生产容量。

## Round 68 执行准备参数副本复用

本轮优化执行准备阶段的快照参数局部复用，没有新增 BenchmarkDotNet 场景。它减少公开不可变参数属性的
重复深拷贝，但真实收益依赖参数数量、集合参数展开、Provider 绑定和并发比例；现有基准不能隔离这些
因素，记为 NOT_APPLICABLE。双框架 SQL 与 Dapper.Core 全量测试通过，不据此推导端到端吞吐、方法级
CPU、构建进程峰值或生产容量。

## Round 69 类型化字典参数快照

`SqlParameterSnapshot` 现在对字符串键的非泛型 `IDictionary` 做递归快照，覆盖 `Dictionary<string, int>` 及嵌套类型化字典，避免它们落入通用枚举展开。此变化发生在计划边界，没有与参数规模、Provider 绑定和并发执行完全同构的基准场景，本轮标记 **`NOT_APPLICABLE`**；双框架 SQL 与 Dapper.Core 全量测试通过，不推导端到端吞吐或容量提升。
## Round 70 类型化字典数组参数快照

`SqlParameterSnapshot.SnapshotArray` 现在能处理递归快照后元素类型发生变化的引用类型数组，例如 `Dictionary<string, int>[]`；只在必要时降级为 `object[]`，避免 `Array.SetValue` 异常。该边界没有与真实 Provider 绑定和并发请求完全同构的基准场景，本轮标记 **`NOT_APPLICABLE`**；双框架 SQL 与 Dapper.Core 全量测试通过，不推导端到端吞吐或容量提升。
## Round 71 查询生成器属性访问器

查询生成器现在为实体元数据生成直接 Getter/Setter 委托，减少生成式查询映射阶段的反射入口。现有 BenchmarkDotNet 场景没有覆盖生成器编译、生成程序集加载和属性访问器冷启动的同构路径，因此本轮记为 **`NOT_APPLICABLE`**，不据此推导端到端吞吐、方法级 CPU、构建进程峰值或生产容量。验证证据为生成器 **321/321**、SQL 双框架各 **1749/1749**、Dapper.Core 双框架各 **241/241**。

## Round 72 继承模型生成 Getter

本轮只修复生成式 CRUD Getter 在派生实体上的模型查找边界。Getter 缓存首次建立时按运行时类型、基类和接口
寻找生成委托，命中后不增加热路径扫描。现有基准没有与继承模型、GeneratedOnly 和缓存冷启动完全同构的场景，
因此记为 **`NOT_APPLICABLE`**；不能从该修复推导端到端吞吐、CPU 归因、干净构建峰值或生产容量。验证证据为
定向 TDD **3/3**、SQL 双框架各 **1752/1752**；SQLite `SqliteDeclaredKeyPolicyTest` 双框架各 **22/22**。

## Round 73 ToEntity 终结点生成映射

本轮只让 Source Generator 将 `ToEntity<TResult>`/`ToEntityAsync<TResult>` 映射到既有
`SingleOrDefault` 静态执行入口。现有基准没有覆盖生成器编译、生成代码装载和该终结点调用的同构场景，
因此标记 **`NOT_APPLICABLE`**；定向测试 **1/1**、生成器全量 **322/322** 通过。这些证据不推导端到端
吞吐、并发容量、方法级 CPU、干净构建峰值或生产容量。

## Round 74 生成参数模型与未登记对象回退

`SqlCompatibleUnregisteredParameterBindingBenchmarks.CompatibleUnregistered` 覆盖兼容模式下未登记参数对象的绑定。
Windows/.NET 8.0.30 进程内 ShortRun（3 次）平均 **63.39 ns/次**，分配列报告为 `-`；误差 **99.39 ns**，且无法设置
高性能电源计划。尚无旧版本同条件基线，性能收益为 **`NOT_VERIFIED`**。该场景不涵盖真实 Provider 执行、并发请求
或完整生产负载，不能推导吞吐、CPU、干净构建峰值或容量。SQLite 定向集成在 net6.0/net8.0 各通过 **16/16**。

## 第 107 轮：已有分页的纯 IR 派生

本轮复用 SqlPagingPlanningBenchmarks 和 SqlLambdaRootBenchmarks.CreateExecutionSnapshot 的原有工作负载，不增加第二套基准。固定前态为 Round106Final，候选为 Round107Final；Setup 继续断言完整 SQL 和参数。

冻结且排序不变的 Page 分配从约 21.31～30.77 KB 降为 7.08～8.23 KB（约减少 66.78%～73.25%）。Count 多约 8 B，首次捕获多约 24 B；部分 Count 与全部捕获均值上升，包含单根捕获 10.716 μs / Error 52.4202 μs 的不利结果。没有重新采样挑选结果。

详细表格、FormalHost NA、复现命令与边界见 [SqlQueryPerformance.md](SqlQueryPerformance.md) 第 107 轮及 output/round107-results.json。只支持当前冻结派生工作负载的分配诊断，不能推导稳态 CPU 或生产容量。

## 第 108 轮：分页执行入口的按需 Builder 来源

保留 eager 的 SimplePage/GroupedPage，新增 SimplePageExecution/GroupedPageExecution；Setup 核对完整 SQL/参数和零 Provider Builder 创建。四组已分页同排序工作负载在同一候选内从 7.08/8.23 KB 降到 1.44/2.59 KB，约少分配 5.64 KB/次。这里比较两个入口成本，不伪称旧版本同方法 before/after；来源实例、首次 Freeze、数据库与并发不在测量内。

仅进行一次进程内短跑；原始误差与告警保留。未变 Count/首次捕获沿用第 107 轮证据，包括不利计时和分配代价。完整八行、复现命令及限制见 [SqlQueryPerformance.md](SqlQueryPerformance.md) 第 108 轮和 output/round108-results.json；方法级 CPU、完整干净构建峰值与生产容量仍需目标环境验证。

## 第 109 轮：未分页页面与首次冻结

SqlPagingPlanningBenchmarks 新增 UnpagedSimplePage、UnpagedGroupedPage 与 UnpagedFreeze；旧、新两侧使用同一工作负载，并在 Setup 断言完整 SQL/参数。修改前生产源码副本通过本轮 targets 替换编译输入，工作区未恢复旧代码。

1/8 个筛选参数下，普通/分组页面从 16.63～25.91 KB 降为 1.79～3.26 KB；首次创建和冻结多约 0.28～0.29 KB（0.7%～1.2%）。保留首次冻结成本、原始较慢计时和较大 Error，不将分页收益外推为所有查询或生产 CPU 提升。

每侧一次成功进程内短跑，三次 100 ms 采样；宿主非优化拒绝采样和初次基线 Compile 替换失败另留日志。完整表格、命令、源码/DLL 身份与限制见 [SqlQueryPerformance.md](SqlQueryPerformance.md) 第 109 轮、output/round109-results.json。

## 第 110 轮：无排序／追加排序

SqlPagingPlanningBenchmarks 新增 UnorderedPage 与 AppendedOrderPage，Setup 断言完整 SQL 和参数，另测 UnpagedFreeze 以保留首次捕获成本。修改前编译 Round110Baseline，最终源码编译 Round110Candidate，两侧新工作负载一致且各一次成功短采样。

1/8 个筛选参数下，页面分配由 17.08～24.70 KB 降至 3.37～5.62 KB；首次创建／冻结多约 0.03～0.07 KB。单参数 Freeze Mean 由 6.822 升至 17.638 μs，候选 Error 91.6887 μs；保留该不利结果，不将短采样解释为稳态 CPU 提升。

完整六组前后值、采样配置和重放命令见 [SqlQueryPerformance.md](SqlQueryPerformance.md) 第 110 轮；源码和二进制身份见 output/round110-results.json。原始 CSV 与全部时间误差不覆盖，生产容量仍需目标环境验证。

## 第 111 轮：CTE Count/Page 与首次冻结

SqlPagingPlanningBenchmarks 新增 CteCount/CtePage/CteFreeze，Filters=1/8。同工作负载的两侧真实二进制先后各测一次；Setup 校验完整 SQL 与参数。派生分配约下降 88.6%～94.4%，首次创建与 Freeze 多约 0.25～0.63 KB。

耗时方差、不利结果及两个真实输出目录全部保留。命令／方法限制见 [SqlQueryPerformance.md](SqlQueryPerformance.md) 第 111 轮，源码／二进制身份见 output/round111-results.json；这些短诊断不证明生产容量或保留堆峰值。

## 第 112 轮：集合 Count/Page 与首次冻结

SqlPagingPlanningBenchmarks 新增 SetCount/SetPage/SetFreeze，Filters=1/8；同工作负载在修改前实际编译基线、修改后编译候选，Setup 完整核对 SQL/参数。已冻结集合计划的派生分配下降约 88.4%～96.4%，首次构建/Freeze 增加 0.68～1.13 KB。

两侧各一次成功短测，时间误差及首次冻结的不利值原样保留，不推导稳态 CPU 或生产容量。详细六组 Mean/Error、采样配置、基线身份与复现命令见 [SqlQueryPerformance.md](SqlQueryPerformance.md) 第 112 轮及 output/round112-results.json。

## 第 113 轮：执行资源构造与释放

SqlResourceConstructionBenchmarks 的 Query/Writer/Multiple 构造真实执行入口并释放未使用资源；不需要数据库。生产修改前编译测量基线，最终候选另测，两侧各一次成功短采样，三类入口均减少 48 B/op（约 5.4%～5.6%）。保留 Query/Multiple 较慢的 Mean 与全部 Error，不能由此判断 CPU 或生产吞吐。

完整口径、前后 Mean/Error 和命令见 [SqlQueryPerformance.md](SqlQueryPerformance.md) 第 113 轮；源码/二进制哈希、TRX 与失败日志见 output/round113-results.json。查询、事务执行、绑定和物化未计入此场景。

## 第 114 轮：Skip-only 分页与首次冻结

SqlPagingPlanningBenchmarks 新增 SkipOnlyPage/SkipOnlyFreeze，Filters=1/8；Setup 完整断言 SQL 与参数。相同 harness 在生产修改前测量基线，候选另测，两侧各一次成功短诊断。Page 分配约下降 86%～88%，首次创建/Freeze 多约 0.06 KB，两个 Freeze Mean 均更慢。

完整四组 Mean/Error、限制与命令见 SqlQueryPerformance.md 第 114 轮及 output/round114-results.json；短测不证明方法 CPU、干净构建峰值、保留堆或生产容量。

## 第 115 轮：第三方 SQL 回退分页

新增 SqlFallbackPlanningBenchmarks.Page，Conditions=1/8；Setup 完整断言 SQL 与参数。冻结扩展计划的首次分页必须保留隔离规划来源。两次候选每次约多 0.06 KB，平均耗时未改善；全部 Mean/Error 和不利结果保留。

实际前态基线与两次候选的原因、动态过滤最终调整的未受影响范围复用依据见 SqlQueryPerformance.md 第 115 轮及 output/round115-results.json；此处没有 DB、保留堆、方法 CPU、干净构建峰值或容量结论。

## 第 116 轮：默认静态查询首次与重复终结

新增 SqlQueryPlanReuseBenchmarks.FirstTerminal / RepeatedTerminal，Conditions=1/8。前者包含草稿构造与首次冻结，后者使用预热后的默认查询。Setup 校验完整 SQL/参数，不执行 DB 命令。一次实际前态和一次候选，重复终结从 7.48/13.28 KB 降至 800 B；首次终结无明确耗时改善。完整 Mean/Error、测量 DLL 哈希和命令见 SqlQueryPerformance.md 第 116 轮及 output/round116-results.json。未测长期保留堆或端到端吞吐。

最终检查增加 CTE/Union/嵌套 CTE 后代动态过滤的 3 个 RED。只排除缓存后仍观察到旧值，随后在需要渲染的准备路径递归检查后代，并在外层不会自行创建过滤快照时先隔离后代。静态命中路径不递归检查。最终新增 1 个 SQLite 守卫，验证后代过滤每次取新值，同次 Count/Data 共用值。三类职责用例在缓存修正前均失败，最终全部通过；补充 SQLite 初稿 using 位置及既有 With Recursive 输出期望错误保留日志，不算产品 RED。

因最终修复改变首次终结路径，按 CIA 重建独立最终候选并额外诊断一次；唯一实际修改前基线不重跑，两个候选全部保留，未按快慢筛选。最终 1 条件首次终结 Mean 为 21.3082 μs（Error 59.71078 μs），比前态更慢且误差很大；不将它隐去或解释为已改善。重复终结分配稳定为 800 B；本轮没有足够证据承诺首次终结耗时或端到端 CPU 收益，不继续重复短测。

## Round117 追加说明：冻结来源与保留查询

`SqlQueryPlanReuseBenchmarks.FirstTerminalRetainingQuery` 覆盖首次终结后由调用方保留查询的路径；既有 `FirstTerminal`、`RepeatedTerminal` 继续保留。条件数 1/8，真实修改前基线和候选各编译、运行一次；不执行数据库，输出对象不等于保留堆测量。

首次终结每次少 8 B，重复终结仍为 800 B；耗时样本有升有降且误差大。六组完整 Mean/Error、环境和限制见 `ai_docs/tasks/20261107-sqlquery-command-aot-design/round117.md`；原始 CSV、二进制哈希和失败收据见 `output/round117-results.json`。此处不声明 CPU 或容量提升。


## Round118：共享 CTE 定义

SqlCteDefinitionBenchmarks 对内建不可变片段分别测创建和重复渲染，Parameters=1/8；Setup 核对完整 SQL/参数。实际修改前与候选各一次成功短测，重复渲染分配 3096→2192 B、14680→11696 B；创建维持 32 B。没有为短测误差反复测量，创建 Mean 较慢也保留。

完整四行 Mean/Error、命令及范围限制见任务 round118.md 和 SqlQueryPerformance.md 第 118 轮，原始 CSV/二进制身份见 output/round118-results.json。自定义片段一次冻结的创建成本、保留堆、CPU 与生产容量尚无本轮测量。基准构建前态/候选均显式设置 -p:ImplicitUsings=enable，绕过既有无关资源基准缺少 System 导入的问题，未修改该文件。


## Round119：冻结定义实际驻留堆

新增命令 `--query-retention-probe --output <json-path>`（SqlQueryRetentionProbe）。固定 256 实例、simple / cte-union、1/8 条件，分别长期保留模板、显式 Freeze 计划或默认首次终结后的 Query。每个 cell 独立栈帧，预热后完整 GC，再测 GC 前后存活堆；WeakReference 分别追踪原草稿和 Builder 服务，GC.KeepAlive 固定被测根。

首次 capture 分配包含语义校验、参数快照和 WeakReference 记账，只比较相同 harness 的前后差异，不能当作单次 GetPlan 微基准。驻留字节包含记录对象，属于本地诊断，不是生产容量或阈值。默认 Query 合理保留一个隔离结构供 Clone/动态过滤使用，显式计划的生命周期委托也可能延长所属 Query 生命周期。

修改前/最终候选的二进制、源码哈希、原始 JSON 与所有中间诊断均保留于 output/round119-*。首次诊断发现上个 cell 栈根可能跨越基线，正式测量使用 NoInlining 独立栈帧；不重写历史数值。最终详细数据见 ai_docs/tasks/20261107-sqlquery-command-aot-design/round119.md。


## Round123：四 Provider 完整生成 SQL 分配

SqlGeneratedMutationBenchmarks 的 Provider 参数为 SQLite/MySQL/PostgreSQL/SqlServer；每个 Provider 独立比较相同模型、Getter、静态 Binder 下逐次渲染 SQL 与复用完整 SQL 的命令构造，Setup 核对完整语句、静态绑定和常量身份。Oracle 没有加入本探针，不能扩展解释为五 Provider 性能均已测量。

复现：

```powershell
dotnet run --project framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Round123Perf --no-restore -p:ImplicitUsings=enable -- --generated-mutation-probe --output output/round123-mutation-allocation-confirmed-data.json
```

最终原始样本 output/round123-mutation-allocation-confirmed-data.json，收据 output/round123-allocation-confirmed.json，源码与 Harness 身份 output/round123-results.json。每格 2048 预热、5×10000 次，.NET 8.0.30/Windows x64；12 组操作的生成 SQL 路径少分配 1824～3080 B/次。完整数值见任务 round123.md。

这是同候选分支比较，不是历史版本 before/after，也不包含数据库 IO、方法 CPU、真实并发、保留堆或容量。固定 Provider 分支增加生成代码/静态 SQL 体积，无每请求形状缓存。初次及 allocation-final 早于 Oracle 自定义方言兼容修正，最终采用 confirmed 候选；没有重建固定性能基线或自设门槛。
