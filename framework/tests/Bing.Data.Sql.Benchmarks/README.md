# SQL 元数据与执行资源测量

## 缓存条件的生成查询对照（2026-11-26）

`SqlCachedPredicateBenchmarks` 使用由真实源生成器编译的调用点，分别比较内联 Lambda 和 `static readonly Expression`：单参数固定值进入完整查询模板，双参数显式值使用生成桥接；`WhereIf` 的关闭和开启分支各自配对。每次调用创建新 Builder，服务对象复用。测试断言完整 SQL、执行计划或 Builder 中的参数及模板热形状的 SQL 复用，避免把延迟模板尚未回放到原 Builder 的状态当作参数丢失。

Windows 10 x64、.NET 8.0.30、Release、`DOTNET_TieredCompilation=0`；每条路径预热 5,000 次，交替测量 5 组、每组 50,000 次。当前线程分配中位数如下（B/次，四舍五入）：

| 路径 | WhereIf 关闭：内联 → 缓存 | WhereIf 开启：内联 → 缓存 |
| --- | ---: | ---: |
| 固定值模板 | 7,296 → 6,720 | 8,776 → 8,200 |
| 显式值桥接 | 18,064 → 17,488 | 29,601 → 29,024 |

该探针使用同一进程配对，每项约少分配 576 B/次；静态字段的首次初始化在预热阶段，不包含在结果中。显式值与固定值是不同调用路径，不把两行的差异解释为缓存条件收益。记录的 CPU 是包含 GC 等运行时线程的进程 CPU，不是方法级 CPU；经过时间是墙钟时间。样本没有数据库 I/O 或并发负载，不代表容量验收或生产性能。原始样本为 `artifacts/benchmarks/r03-predperf/b1/probe.json`，测试与身份见 `artifacts/ai-tasks/r03-predperf/verification.json`。

```powershell
dotnet <隔离构建输出>/Bing.Data.Sql.Benchmarks.dll --cached-predicate-probe --output artifacts/benchmarks/r03-predperf/b1/probe.json
dotnet <隔离构建输出>/Bing.Data.Sql.Benchmarks.dll --filter '*SqlCachedPredicateBenchmarks*' --artifacts artifacts/benchmarks/r03-predperf/b1/formal
```

## 未引用名称解析分配对照（2026-11-25）

`SqlPlanningHotPathBenchmarks.ParseNameSegments` 补充单段、两段、三段名称的独立场景；保留原来的 `SqlItem.Parse`、首次 `GetPlan`、Builder 和别名克隆场景。`NameItem.ResolveBySplit` 直接定位句点并保存名称段，移除 `Split` 数组和 `ToList` 集合，不增加缓存或公共入口。引用名称仍走原解析路径，空段、空白读取和四段及以上的既有行为由直接测试覆盖。

Windows x64/.NET 8.0.30/Release，4096 次预热、5×5000 次当前线程分配；同一 harness 源码在修改前和修改后各运行一次：

| 场景 | 修改前 B/次 | 修改后 B/次 |
| --- | ---: | ---: |
| NameItem 单段 | 168 | 40 |
| NameItem 两段 | 264 | 120 |
| NameItem 三段 | 320 | 160 |
| SqlItem.Parse 两段 | 320 | 176 |
| 首次 GetPlan（输入 Builder 已准备） | 13384 | 13384 |
| Builder Clone | 3208 | 3208 |
| 空/自连接别名 Clone | 280 / 792 | 280 / 792 |

各场景五次分配值一致。已准备 Builder 的计划和克隆不重新执行名称解析，因此本次没有降低这两类操作的分配。短时计时原样保留，仅作诊断，不据此声称方法 CPU 或 C1 容量提升。两侧依赖程序集重新编译，完整文件摘要保留，不是仅替换一个 DLL 的实验。

本次新增三类直接名称场景，旧五场景报告没有这些输入，因此为扩展后的同一 harness 保存一次真实前态；没有覆盖 `r02-alias` 的历史基线。原始前后 JSON、比较、红绿 TRX、构建日志、源码及二进制身份见 `artifacts/ai-tasks/r02-name/verification.json`。临时构建使用 `artifacts/validation/r02-name/b1/`，原始测量使用 `artifacts/benchmarks/r02-name/b1/`，归档核验后清理。

## 别名克隆分配对照（2026-11-25）

`SqlPlanningHotPathBenchmarks` 新增 `CloneEmptyAliases` 和 `CloneSelfJoinAliases`；后一场景保留同实体两个自连接别名及一个原始来源别名。原有 `GetPlan`、`CloneBuilder`、`ParseName` 场景仍在Setup断言完整SQL。

移除 `EntityAliasRegister` 私有构造器对Clone已独立创建的HashSet的第二次复制，不共享原注册器状态。Windows x64/.NET8.0.30/Release，4096次预热、5×5000次当前线程分配：空别名344→280 B/op，自连接别名968→792 B/op，普通Builder Clone3272→3208 B/op，首次GetPlan13448→13384 B/op，名称解析320 B/op不变。各5次分配一致；耗时仅作诊断，无数据库IO或并发，不代表C1吞吐预算已经通过。前后依赖程序集会重编译，具体身份与报告见 `artifacts/ai-tasks/r02-alias/verification.json`，不是固定同一宿主仅替换一个DLL的试验。

```powershell
dotnet <隔离构建输出>/Bing.Data.Sql.Benchmarks.dll --planning-hot-path-probe --output artifacts/benchmarks/<任务>/<批次>/planning.json
```

原始前后JSON、比较、源码及程序集哈希统一归档于 `artifacts/ai-tasks/r02-alias/`；本次临时构建和Benchmark目录归档核验后清理。该小范围优化不触发无条件反复运行完整容量矩阵。

## Right Join 保留侧过滤的测量边界（2026-11-24）

本次复用 `SqlFilterPlacementPlanner` 的来源扫描、谓词和参数路径，移除保留侧的提前拒绝，并继续检查后续连接。没有修改映射缓存、格式化热路径或 Builder 克隆，没有新增请求容器、缓存和公共 API。旧版本在该输入上抛出异常，因此相同成功查询的 before/after 为 NOT_APPLICABLE；本次以完整 SQL、冻结 Count/Page 和 SQLite 结果集回归验收，不新增伪造的历史基准，也不宣称 CPU、分配或吞吐提升。现有基准场景不受此新增成功路径影响，未重新运行重型性能矩阵。

## Returning 静态只读投影复用（2026-11-24）

`SqlReturningProjectionBenchmarks` 新增 `BuildGeneratedCachedProjection` 和 `BuildGeneratedCachedComputedProjection`。Models 项目使用同一个 `static readonly Expression` 字段，生成器识别声明处的直接 Lambda；Setup 与探针逐项校验完整 SQL。正式方法发现为 15 项，本次只运行固定分配探针，不运行方法 CPU 或生产容量作业。

`SqlReturningProjectionProbe-v5`：.NET 8.0.30/Windows x64、`DOTNET_TieredCompilation=0`，每场景 2,048 次预热、7×5,000 次采样。先用未修改的生成器编译并保存 Models DLL，最终在同一个宿主中仅替换前后 Models DLL；实际宿主逐文件哈希已核对，只有该文件不同。

| 场景 | 修改前 B/次（中位数） | 修改后 B/次（中位数） | 减少 |
| --- | ---: | ---: | ---: |
| generated-cached-columns | 8,344 | 6,440 | 1,904 B，22.82% |
| generated-cached-arithmetic | 8,608 | 6,440 | 2,168 B，25.19% |
| 原有 13 场景 | 各自原值 | 各自原值 | 0 B |

上述前后比较都使用复用字段；收益来自字段调用也能采用生成布局和 SQL，不再回退到运行时解析。相同最终宿主的内联生成场景分别为 7,784.064 和 7,864.064 B/次，字段复用还避免了逐次 Expression 构造。Models 程序集从 80,896 B 增为 81,920 B，生成布局和 SQL 常量增加了 1,024 B 的代码体积。字段在预热前初始化一次；初始化分配与可能的裁剪警告仍存在，不代表进程零分配或所有 Lambda 零反射。测量包含 Builder、参数快照和命令构造，不含数据库 I/O、并发、保留堆、方法 CPU 或容量验收。

原始 `probe-before.json`、`probe-after.json` 和 `comparison.json` 保留在 `artifacts/benchmarks/returning-cache/b1/`。初始未固定分层编译配置的 `initial-before.json` 仅作诊断，不参与结论。宿主身份、命令、前后 Models 和源代码见 [verification.json](../../../artifacts/ai-tasks/returning-cache/verification.json)。可重建输出限定 `artifacts/validation/returning-cache/b1/`，归档后清理。

```powershell
dotnet <隔离构建输出>/Bing.Data.Sql.Benchmarks.dll --returning-projection-probe --output artifacts/benchmarks/returning-cache/b1/probe-after.json
dotnet <隔离构建输出>/Bing.Data.Sql.Benchmarks.dll --filter '*SqlReturningProjectionBenchmarks*' --artifacts artifacts/benchmarks/returning-cache/b1/formal
```

## Returning 固定物理名的预生成（2026-11-24）

`SqlReturningProjectionBenchmarks` 新增 `BuildGeneratedQuotedProjection` 和 `BuildGeneratedQuotedComputedProjection`，物理列名含空格或内部反引号；Setup 校验完整 SQL。正式 13 个方法的发现通过，本次没有运行正式 CPU 或吞吐基准。

`SqlReturningProjectionProbe-v4` 包含原 11 场景和上述 2 场景；.NET 8.0.30/Windows x64、`DOTNET_TieredCompilation=0`、2,048 预热、7×5,000 次，包含原 API 的调用点 Expression 构造。修改前先编译并保存 Models DLL；最终配对固定同一编译宿主及全部其他依赖，只替换前后 Models DLL。原始结果为 `artifacts/benchmarks/returning-names/b1/paired-before.json`、`paired-after.json`。

| 场景 | 修改前 B/次（中位数） | 修改后 B/次（中位数） | 少分配 |
| --- | ---: | ---: | ---: |
| generated-quoted-columns | 9,432.064 | 7,784.064 | 1,648 B，17.47% |
| generated-quoted-arithmetic | 10,768.192 | 8,080.128 | 2,688.064 B，24.96% |
| 其余原 11 场景 | 各自原值 | 各自原值 | 0 B |

两项减少来自提前复用 SQL 文本及静态列布局。Models 程序集从 77,824 B 增为 79,872 B，保留了生成常量带来的代码体积代价。无数据库 IO、并发请求、保留堆、方法 CPU、干净构建峰值或生产容量结论；原始耗时有升有降，不据此宣称统一加速。

最初 `before.json`/`after.json` 的宿主重新编译后哈希不同，因此原始结果保留作诊断，最终使用固定宿主的配对。`paired-before.json` 使用保存的真实修改前 Models DLL；`paired-after.json` 复用已用完全相同最终宿主测量的 `after.json`，没有重复挑选候选样本。全部身份、两组比较、日志及重放命令见 [verification.json](../../../artifacts/ai-tasks/returning-names/verification.json)。隔离构建根为 `artifacts/validation/returning-names/b1/`，可重建输出在证据归档后清理。

```powershell
dotnet <隔离构建输出>/Bing.Data.Sql.Benchmarks.dll --returning-projection-probe --output artifacts/benchmarks/returning-names/b1/paired-after.json
dotnet <隔离构建输出>/Bing.Data.Sql.Benchmarks.dll --filter '*SqlReturningProjectionBenchmarks*' --artifacts artifacts/benchmarks/returning-names/b1/formal
```

## Returning 一元运算与既有场景分配

2026-11-24 增加 `SqlReturningArithmeticBenchmarks` 三个正式场景：`CreateBinaryComputed`、`CreateUnaryArithmetic`、`AppendMappedArithmetic`。Setup 对二元、一元及 SQL Server 物理映射后投影断言完整 SQL；输入片段和复用缓冲区在测量前建立，构造与渲染分开测量。三项发现通过，本轮没有执行 FormalHost 计时作业。

复用 `SqlReturningProjectionProbe-v3` 的 11 个端到端构建场景，两个进程使用同一编译 harness 和依赖，仅替换真实修改前/后 SQL 程序集；.NET 8.0.30/Windows x64，`DOTNET_TieredCompilation=0`，每场景预热 2,048，7×5,000 次。11 项托管分配中位数全部相同（B/次）：cached anonymous 8,344，cached DTO 8,232，runtime inline anonymous 9,744.096，runtime inline DTO 9,544，generated anonymous 7,784.064，generated DTO 7,696，cached computed 8,608，runtime inline computed 10,088.096，generated computed 7,864.064，cached remapped computed 8,672，generated remapped computed 9,592.096。该对照验证新算术校验没有增加既有场景分配，不能用它声称新增负号已降低 CPU 或提高生产吞吐。新增负号正式基准仍供目标主机测量。

原始配对样本保留在 `artifacts/benchmarks/returning-unary/b1/before.json`、`after.json`，完整身份及日志见 [verification.json](../../../artifacts/ai-tasks/returning-unary/verification.json)。构建输出限定 `artifacts/validation/returning-unary/b1/`；运行示例（从仓库根目录，程序集使用隔离输出）：

```powershell
dotnet <隔离构建输出>/Bing.Data.Sql.Benchmarks.dll --returning-projection-probe --output artifacts/benchmarks/returning-unary/b1/after.json
dotnet <隔离构建输出>/Bing.Data.Sql.Benchmarks.dll --filter '*SqlReturningArithmeticBenchmarks*' --artifacts artifacts/benchmarks/returning-unary/b1/formal
```

本轮没有运行方法级 CPU、完整干净构建进程峰值或生产容量验证；这些需要在对应目标环境单独验收。

## 不可变列值与克隆

2026-11-24 新增 `SqlColumnCloneBenchmarks`：1/32/128 列的 `CloneColumnCollection` 与 `CloneSqliteBuilder`。Setup 断言完整列 SQL 和完整 Builder SQL、聚合与别名保留，以及修改克隆后源对象不变。

固定分配探针使用同一编译 harness 及其余依赖，仅替换真实修改前/后的 `Bing.Data.Sql.dll`；.NET 8.0.30 / Windows x64，`DOTNET_TieredCompilation=0`，每格预热 2,048 次，采样 5×2,000 次。中位数如下（B/次，四舍五入；每个样本另含固定计时器分配 40 B）：

| 操作 | 1 列 before→after | 32 列 before→after | 128 列 before→after |
| --- | --- | --- | --- |
| ColumnCollection.Clone | 224→88 | 2,456→336 | 9,368→1,104 |
| SqliteBuilder.Clone | 3,896→3,768 | 6,128→4,016 | 13,040→4,784 |

128 列时分别减少约 88.2% 和 63.3% 托管分配。`ColumnItem` 不可变后可共享值，每次克隆仅复制列容器；它没有改变其他 Clause 的可变草稿合同。分配探针入口：

```powershell
dotnet <隔离构建输出>/Bing.Data.Sql.Benchmarks.dll --column-clone-probe --output artifacts/benchmarks/clause-values/b1/after.json
```

先创建输出父目录，构建产物必须放在 `artifacts/validation/`，不可使用默认 `dotnet run` 在源码目录产生输出。正式 BenchmarkDotNet 运行应显式传 `--filter '*SqlColumnCloneBenchmarks*' --artifacts artifacts/benchmarks/<任务>/<批次>`。本次执行了两个正式方法的发现和固定分配探针，没有执行计时基准、方法 CPU 或生产容量测量。原始样本：`artifacts/benchmarks/clause-values/b1/before.json`、`after.json`；源码、程序集身份与验证记录见 [验收证据](../../../artifacts/ai-tasks/clause-values/verification.json)。

## 动态过滤局部渲染工作区

2026-11-24 在 `SqlQueryTemplateIrBenchmarks` 增加 `FreezeFilteredSnapshot` 和 `FreezeFilteredPlan`，分别覆盖直接冻结动态过滤和 SqlQuery 隔离草稿后的完整计划创建。过滤器与业务条件共享一次参数快照；Setup 断言完整 SQL、参数名称和值。正式基准共七个方法，`SqlQueryTemplateIrAllocation-v2` 覆盖 1/8 条件共十四格。

配对使用同一份修改前编译的 harness（SHA256=812a423a3e452e9c02e0a6869ba65c1649607864f0fea3c880c6137864596983）及全部依赖，仅替换真实的 `Bing.Data.Sql.dll`；.NET 8.0.30/Windows x64，`DOTNET_TieredCompilation=0`，2,048 次预热，每格 5×2,000 次采样。托管分配中位数如下（B/次）：

| 操作 | 1 条件：修改前 → 后 | 8 条件：修改前 → 后 |
| --- | ---: | ---: |
| CreateTemplate | 11,016 → 10,800 | 17,960 → 17,744 |
| RenderScopedTemplate | 4,240 → 4,240 | 12,400 → 12,400 |
| CreatePlan | 192 → 192 | 192 → 192 |
| FreezeFilteredSnapshot | 18,608.496 → 13,992.248 | 26,192.744 → 21,128.496 |
| FreezeFilteredPlan | 23,224.496 → 18,840.496 | 31,816.744 → 26,984.744 |
| DeriveCount | 520 → 520 | 1,080 → 1,080 |
| DerivePage | 2,552 → 2,552 | 3,168 → 3,168 |

动态过滤快照减少约 24.81%/19.33%，完整计划减少约 18.88%/15.19%。默认路径只复制需要修改的 Where/Join/参数/排序工作区；自定义 SQL 尾部、原生排序及未被冻结结构覆盖的分页路径，仍将同次过滤状态提升为独立规划副本。过滤器的冻结集合直接读取，消除每次重复的 `ToList`。没有增加公共 API、请求值缓存或参数池。

原始配对数据为 `artifacts/benchmarks/filter-render/b1/before.json` 与 `after.json`；中间诊断数据单独保留，不参与最终表。正式基准发现七个方法通过，本次只执行托管分配探针，未执行计时 ShortRun、方法 CPU 或生产并发容量测量。[TDD、程序集身份与原始证据](../../../artifacts/ai-tasks/filter-render/verification.json)

## 冻结参数派生复用与组合只读视图

2026-11-24 沿用 `SqlQueryTemplateIrBenchmarks` 的五个正式场景和原有 Setup，测量 Count/Page 不再深拷贝未变业务参数的效果。组合只读视图按访问延迟创建，内部渲染/Clone 直接访问私有列表；已有 `MySqlBuilderCloneBenchmarks` 覆盖 Builder 克隆，`SqlBuilderCteAndParameterTokenBenchmarks.ConfigureCteSnapshotAndRender` 覆盖 CTE 快照及渲染；组合正确性由直接单元和 SQLite 用例校验，本次不新增重复基准。此次更新测量说明，未修改 harness 源码。

同一份修改前编译的 harness（SHA256=b5fbb0808170cdf30d8c04a27c37bdf9375229490e8ba872cf853bc9ac587ac2），分别加载修改前、后的运行库；.NET 8.0.30/Windows x64，2,048 预热，每格 5×2,000，托管分配中位数如下（B/次）：

| 操作 | 1 条件：修改前 → 后 | 8 条件：修改前 → 后 |
| --- | ---: | ---: |
| CreateTemplate | 11,192 → 11,144 | 18,136 → 18,088 |
| RenderScopedTemplate | 4,240 → 4,240 | 12,400 → 12,400 |
| CreatePlan | 192 → 192 | 192 → 192 |
| DeriveCount | 1,136 → 520 | 2,704 → 1,080 |
| DerivePage | 3,400 → 2,616 | 5,144 → 3,232 |

Count 少约 54.2%/60.1%，Page 少约 23.1%/37.2%。公开参数与执行绑定仍隔离复制，第一次输入和作用域转换仍需冻结；优化只共享框架内部已经冻结的参数，没有引入参数池、缓存或反射。构建和五方法发现通过；这里只执行分配探针，没有运行计时 Benchmark 或生产并发容量测量。

原始样本位于 `artifacts/benchmarks/query-state/b1/before.json` 和 `after-same-harness.json`；新编译 harness 的辅助样本另存 `after.json`，不混入上述配对表。源码、实际程序集、命令和留档副本见 [验收记录](../../../artifacts/ai-tasks/query-state/verification.json)。

## 查询模板保留 IR

`SqlQueryTemplateIrBenchmarks` 覆盖 1/8 个条件下的 CreateTemplate、RenderScopedTemplate、CreatePlan、DeriveCount、DerivePage。Setup 核对全部 SQL、参数名称和值；正式构建及五方法发现通过。分配探针使用相同 harness，仅更换本次功能 RED 时冻结的旧运行库与最终运行库。

```powershell
dotnet $benchmarkDll --query-template-ir-probe --output artifacts/benchmarks/template-ir/current/allocation.json
dotnet $benchmarkDll --filter '*SqlQueryTemplateIrBenchmarks*' --job Short --inProcess --artifacts artifacts/benchmarks/template-ir/short
```

.NET 8.0.30/Windows x64，2,048 次预热，每格 5×2,000 次采样，全部样本稳定为下列 B/次：

| 操作 | 1 条件：修改前 → 后 | 8 条件：修改前 → 后 |
| --- | ---: | ---: |
| 创建模板 | 10,424 → 11,192 | 15,888 → 18,136 |
| 渲染参数作用域 | 1,920 → 4,240 | 10,856 → 12,400 |
| 从共享模板创建计划 | 1,248 → 192 | 4,360 → 192 |
| 从模板 IR 派生 Count | 不支持 → 1,136 | 不支持 → 2,704 |
| 从模板 IR 派生 Page | 不支持 → 3,400 | 不支持 → 5,144 |

共享模板创建计划少分配约 84.6%/95.6%；模板创建和作用域重命名增加结构、位置与预留分页槽位的成本，尤其少参数场景应按实际调用组合评估。模板适合在结构和值稳定时复用，独立计划无需每次更换参数作用域。IR 与片段内部共享冻结参数，作用域渲染只深拷贝一次，公开读取继续隔离；独立复核消除的第二次参数克隆使当前作用域路径比初稿再少 320/1,384 B。

历史运行库不提供模板 IR，Count/Page 在 before 报告中明确为 unsupported，不进行语义不同的历史对比。这是托管分配探针，未运行计时 ShortRun，不用于 CPU、数据库吞吐、P99 或容量判断。初稿与最终原始样本保存在 `artifacts/benchmarks/template-ir/b1/`；源码、程序集身份及全部验证见 [模板 IR 证据](../../../artifacts/ai-tasks/template-ir/verification.json)。

## Returning 投影生成 SQL

`SqlReturningProjectionBenchmarks` 使用相同 SQLite 模型、Update/Where、参数和完整 SQL 比较十一个场景：缓存表达式、每次创建表达式的运行时调用，以及独立 Models 程序集中的真实生成式调用，各含匿名对象、DTO 初始化和列运算；另有运行时映射变化时的缓存表达式与生成调用。列运算固定为 `(Id + Id) * Id`。Setup 分别验证直接投影、计算投影和重映射投影的完整 SQL 及参数。生成场景仍计入调用时 Expression 树的构造；运行时场景放在未开启查询拦截的程序集，避免比较的两端实际走同一生成路径。

```powershell
# benchmarkDll 指向 artifacts/validation/ 下隔离构建的正式基准程序集。
dotnet $benchmarkDll --returning-projection-probe --output artifacts/benchmarks/returning-bind/current/allocation.json
dotnet $benchmarkDll --filter '*SqlReturningProjectionBenchmarks*' --job Short --inProcess --artifacts artifacts/benchmarks/returning-bind/short
```

此前列运算扩展的本机 .NET 8.0.30/Windows x64 样本，2,048 次预热，每格 7×5,000 次采样，分配中位数如下（B/次）：

| 投影 | 缓存表达式 | 运行时内联表达式 | 生成式内联表达式 |
| --- | ---: | ---: | ---: |
| 匿名对象 | 8,856 | 10,256.096 | 8,040.064 |
| DTO 初始化 | 8,744 | 10,056 | 7,760 |
| 列运算 | 8,672 | 10,088.096 | 7,864.064 |

对应内联路径少分配约 21.6% / 22.8% / 22.0%。匿名生成场景的七个样本为 7,848.064～8,040.096 B/次，缓存列运算为 8,608～8,672 B/次，保留这些波动；DTO 生成场景稳定为 7,760 B/次。这是同一候选中可选路径的比较，既有生成布局也参与结果，不能把全部差值归因于本次列运算扩展，也不是历史版本前后对照。新列运算没有等价历史基线；探针版本为 `SqlReturningProjectionProbe-v2`。

测量包含 Builder、表达式树、冻结命令与参数快照构造，不含数据库 I/O。每个生成调用点使用静态列布局、Provider SQL 常量和工厂；计算列保存一次复制的不可变片段，普通列没有新增引用字段，没有新增请求值缓存或共享可变 Builder。此前完成分配探针、正式基准构建和九方法发现，ShortRun 只提供复现命令；原始耗时不作为 CPU、吞吐、P99 或生产容量结论。[列运算证据与源码/程序集身份](../../../artifacts/ai-tasks/returning-math/verification.json) · [先前直接列证据](../../../artifacts/ai-tasks/returning-sql/verification.json)

2026-11-24 增加 `BuildCachedRemappedComputedProjection` 与 `BuildGeneratedRemappedComputedProjection`，探针升级为 `SqlReturningProjectionProbe-v3`。映射把 `row_id/display_name` 改为 `tenant_id/tenant_name`，生成器输出当前列布局的绑定代码，避免已识别计算投影再次解析 Expression。正式基准构建和十一个方法发现通过。

配对测量使用同一份修改前编译的基准程序及全部运行库，只替换由前后两个生成器产生的 Models 程序集；关闭分层 JIT（`DOTNET_TieredCompilation=0`），保留相同的 2,048 预热和 7×5,000 采样。重映射生成场景中位数从 10,192.096 降为 9,592.096 B/次，减少 600 B（约 5.9%），其余十个场景分配中位数未变；映射解析次数另由单元测试断言一次。开启分层 JIT 的辅助样本不参与这一结论。原始配对样本在 `artifacts/benchmarks/returning-bind/b1/stable-before.json` 与 `stable-after.json`，程序集身份和复现条件见 [本次证据](../../../artifacts/ai-tasks/returning-bind/verification.json)。没有据此宣称 CPU 或生产容量改善。

## 公开计划工厂与冻结复用

`SqlQueryPlanReuseBenchmarks.PublicFactoryFreeze` 对同一个稳定 Builder 调用 `SqlQueryPlan.Create(builder).Freeze()`，前后语义一致；`FrozenPlanFreeze` 只复用已冻结计划。Setup 验证完整 SQL/参数及 Freeze 的同一性，不要求旧版公开 Create 已冻结。原 FirstTerminal、RepeatedTerminal 和保留查询场景继续存在。

```powershell
# benchmarkDll 指向 artifacts/validation/ 下隔离构建的正式基准程序集。
dotnet $benchmarkDll --query-plan-freeze-probe --output artifacts/benchmarks/plan-entry/current/allocation.json
dotnet $benchmarkDll --filter '*SqlQueryPlanReuseBenchmarks*' --job Short --inProcess --artifacts artifacts/benchmarks/plan-entry/short
```

Windows x64/.NET 8.0.30，同一最终 harness，2,048 次预热、每格 5×5,000 次采样，前后均构建真实运行库。公开工厂 1/8 条件的中位数分别为 11,424→11,256 / 18,368→18,200 B/次，均减少 168 B（1.47% / 0.91%）；已冻结计划重复 Freeze 前后均为 0 B/次。五个正式基准方法均发现，当前只执行分配探针，Short 命令是后续复现入口；不推导 CPU、吞吐、P99 或驻留堆。[原始样本、命令与二进制身份](../../../artifacts/ai-tasks/plan-entry/verification.json)

## 多行多列筛选 InsertCombined

`SqlGeneratedMutationRowsBenchmarks` 覆盖 SQLite 3/12 行、Include/Exclude 共四种负载、八格路径。使用独立模型项目的真实源生成代码；对照模型仅禁用 `GeneratedMutationColumnsSql`，保留相同映射、Getter 和静态 binder。Setup 核对完整 SQL、行优先参数名称和值、实际工厂命中，以及两条路径相同的 binder；计数器不进入正式测量。

```powershell
# $benchmarkDll 指向 artifacts/validation/ 下隔离构建的入口程序集。
dotnet $benchmarkDll --generated-mutation-rows-probe --output artifacts/benchmarks/selected-rows/current/allocation.json
dotnet $benchmarkDll --filter '*SqlGeneratedMutationRowsBenchmarks*' --job Short --inProcess --artifacts artifacts/benchmarks/selected-rows/short
```

本机 .NET 8.0.30/Windows x64，2,048 次预热、每格 5×5,000 次采样，中位数如下（B/次）。两条路径来自同一候选；默认/单列形状工厂保持启用，测量的三列子集只能由本轮选列工厂接管。

| 行数 | Include：Builder → 生成 | Exclude：Builder → 生成 | 减少 |
| --- | ---: | ---: | ---: |
| 3 | 22,752 → 12,568 | 22,568 → 12,448 | 44.76% / 44.84% |
| 12 | 82,840 → 36,176 | 82,784 → 36,120 | 56.33% / 56.37% |

减少比例按 Include / Exclude 顺序列出。只测量托管命令构造分配，不含数据库 I/O，不推导 CPU、吞吐、P99 或生产容量。完整 SQL 有四 Provider 直接单元覆盖，本基准仅测 SQLite。当前执行分配探针与基准发现，Short 命令仅为复现入口，未声称计时基准已运行。[本轮命令、原始数据与源码身份](../../../artifacts/ai-tasks/selected-rows/verification.json)

## 单行多列筛选 Insert/Update

`SqlGeneratedMutationColumnsBenchmarks` 比较同一 SQLite 模型、静态 Getter/binder 和输入下的生成片段与 Builder 回退。正式夹具在独立 `Bing.Data.Sql.Benchmarks.Models` 项目编译时生成，避免把查询拦截器加到已有运行时 Lambda 基准。覆盖 Insert/Update × Include/Exclude × 普通/整行 Returning 共八种负载、十六格路径。Setup 验证完整 SQL、参数名及值、Returning/生成标记和相同静态 binder，确认多列工厂确实命中，不用预制 SQL 常量代替生成算法。

```powershell
# $benchmarkDll 指向 artifacts/validation/ 下隔离构建的入口程序集。
dotnet $benchmarkDll --generated-mutation-columns-probe --output artifacts/benchmarks/selected-columns/current/allocation.json
dotnet $benchmarkDll --filter '*SqlGeneratedMutationColumnsBenchmarks*' --job Short --inProcess --artifacts artifacts/benchmarks/selected-columns/short
```

本机 .NET 8.0.30/Windows x64，2,048 次预热、每格 5×5,000 次采样，中位数如下（B/次）。两条路径来自同一候选，通过将模型的可选列 SQL 工厂置空选择 Builder；此表不是历史版本前后测量。

| 操作 | Include：Builder → 生成 | Exclude：Builder → 生成 |
| --- | ---: | ---: |
| Insert | 11,104 → 5,928 | 10,664 → 5,808 |
| InsertReturning | 18,848 → 6,720 | 17,512 → 6,600 |
| Update | 14,104 → 8,464 | 14,048 → 8,408 |
| UpdateReturning | 20,952 → 9,528 | 20,896 → 9,472 |

八组命令构造分配减少约 40%～65%。只验证 SQLite 托管分配；其他 Provider 有完整 SQL 单元覆盖，没有在本基准测量。测量不含数据库 I/O，不推导 CPU、吞吐、P99 或生产容量。原始 JSON、日志、命令及程序集/源码身份见 [筛选列证据](../../../artifacts/ai-tasks/selected-columns/verification.json)。ShortRun 命令为复现入口，本次仅执行分配探针与基准发现，不将其写成已运行的计时基准。

新增运行的报告、日志和探针输出统一放在仓库根目录的 `artifacts/benchmarks/sql/<运行批次>/`，临时构建和源码副本放在 `artifacts/validation/`。下文命令从仓库根目录运行，BenchmarkDotNet 必须显式指定 `--artifacts`；重复测量使用新的批次目录。历史任务路径仅表示当时的留档位置，新复现使用上述统一目录。

## 复合键与并发列的批量 Delete 命令

`SqlGeneratedPairedDeleteBenchmarks` 覆盖 SQLite/MySQL/PostgreSQL/SQL Server、3/12 行和 SQL 生成开关，共 16 格。每行包含两个键和一个版本列；两条路径使用相同的静态 Getter、映射及参数绑定。Setup 比较完整 SQL、参数元数据和并发标记，生成式委托每次按实际行数展开参数槽位及原 Builder 的逐层 OR 括号；不复用预制 SQL，也不在请求中格式化列名。测量包含冻结命令构造，不含数据库 IO。

```powershell
# $benchmarkDll 指向 artifacts/validation/ 下隔离构建的基准程序集。
dotnet $benchmarkDll --filter '*SqlGeneratedPairedDeleteBenchmarks*' --job Short --inProcess --artifacts artifacts/benchmarks/paired-delete/current
```

本机 .NET 8.0.30/Windows x64 的 2,048 次预热、5×5,000 次采样，正式 corrected pair 得到下列中位数（B/次）。生成路径八格均命中；before 使用功能 RED 构建时冻结的运行库。

| Provider | 3 行：修改前 → 后 | 减少 | 12 行：修改前 → 后 | 减少 |
| --- | ---: | ---: | ---: | ---: |
| SQLite | 23,264 → 11,920 | 48.8% | 93,472 → 37,744 | 59.6% |
| MySQL | 23,136 → 11,856 | 48.8% | 93,472 → 37,744 | 59.6% |
| PostgreSQL | 23,200 → 11,864 | 48.9% | 93,536 → 37,752 | 59.6% |
| SQL Server | 23,144 → 11,864 | 48.7% | 93,480 → 37,752 | 59.6% |

动态回退对照各格增加 32 B/次，约 0.03%～0.14%，完整数值保留。只报告托管分配，不推导 CPU、数据库吞吐、P99 或生产容量。正式基准项目隔离构建和基准发现通过。

正式分配对照要求 before/after 使用同一最终 harness 源码 SHA，before 仅引用修改前冻结程序集。三行及以上的完整 SQL 比较曾发现生成路径平铺 OR 与旧 Builder 逐层括号不一致，已补入生成器回归并修正；首次 after 失败及旧 harness 的 before 记录只保留为过程证据。原始报告、命令和源码/程序集摘要归档于 [paired-delete](../../../artifacts/ai-tasks/paired-delete/verification.json)。Oracle 有完整生成 SQL 单元覆盖，本基准未新增 Oracle 依赖。

## 单主键批量 Delete 命令

`SqlGeneratedBatchDeleteBenchmarks` 覆盖 SQLite/MySQL/PostgreSQL/SQL Server、3/12 行和 SQL 生成开关，共 16 格。两条路径使用相同模型、生成 Getter、参数绑定及输入；Setup 逐项核对完整 SQL、执行标记和 `SqlParam` 元数据。生成委托每次都执行与生成代码相同的 `StringBuilder` 和 IN 参数槽位展开，不复用预制 SQL 字符串。命令构造包含冻结参数快照，不含数据库 IO。

```powershell
# $benchmarkDll 指向 artifacts/validation/ 下隔离构建的基准程序集。
dotnet $benchmarkDll --filter '*SqlGeneratedBatchDeleteBenchmarks*' --job Short --inProcess --artifacts artifacts/benchmarks/batch-delete/current
```

本机 .NET 8.0.30/Windows x64 的同源码探针，以 2,048 次预热、5×5,000 次采样比较修改前冻结库与当前库。下表是登记生成式 SQL 的相同输入在前后两版的分配中位数（B/次）；旧版尚未调用批量 Delete 形状委托，新版八个生成格均命中。

| Provider | 3 行：修改前 → 后 | 减少 | 12 行：修改前 → 后 | 减少 |
| --- | ---: | ---: | ---: | ---: |
| SQLite | 8,688 → 4,944 | 43.1% | 约 19,767.44 → 13,712 | 30.6% |
| MySQL | 8,432 → 4,880 | 42.1% | 19,640 → 13,712 | 30.2% |
| PostgreSQL | 8,496 → 4,888 | 42.5% | 19,704 → 13,720 | 30.4% |
| SQL Server | 8,440 → 4,888 | 42.1% | 19,648 → 13,720 | 30.2% |

动态渲染对照保留：SQLite 的 3/12 行分别为 8,688→8,720、19,768→19,928；MySQL 为 8,432→8,592、19,640→19,672；PostgreSQL 为 8,496→8,528、19,704→19,736；SQL Server 为 8,440→8,472、19,648→19,680 B/次。未将这组小幅增量写成完全不变，也未用分配结果推导 CPU、数据库吞吐、P99 或生产容量。

首轮形状委托遗漏运行时槽位展开，已标记为 stale，不能用于性能结论。正式 corrected pair 使用同一最终基准源码 SHA；before 仅引用编辑前冻结程序集，不重建旧业务源码。原始 corrected JSON、初始无效证据及命令/源码/二进制摘要最终归档于 [batch-delete](../../../artifacts/ai-tasks/batch-delete/verification.json)，可重建的临时构建与采样输出按仓库规则清理。Oracle 有生成器完整 SQL 覆盖，本基准未纳入该 Provider。

## 实体整行 Returning 命令

`SqlGeneratedReturningBenchmarks` 比较同一 SQLite 模型、生成 Getter、静态参数绑定和并发配置下，`InsertReturning` / `UpdateReturning` / `DeleteReturning` 的动态渲染与完整 SQL 常量路径。`UseGeneratedSql` 只切换模型的 SQL 工厂；每格 Setup 校验完整 SQL、返回标记、绑定器，生成路径还校验常量引用。测量包含命令和参数快照构造，不包含数据库 IO、读取或网络，也不代表历史版本前后对比。

```powershell
# benchmarkDll 指向使用 artifacts/validation/ 隔离构建得到的程序集。
dotnet $benchmarkDll --filter '*SqlGeneratedReturningBenchmarks*' --job Short --inProcess --artifacts artifacts/benchmarks/crud-returning/current
```

原始报告统一位于 `artifacts/benchmarks/crud-returning/current/`；最终命令、源码身份和结果见 [验证清单](../../../artifacts/ai-tasks/crud-returning/verification.json)。首次 API 验证没有等价历史版本，故其 before 归类为 `NOT_APPLICABLE`；本次后续命令直通优化的同候选 before/after 见下文。这里的分支测量不替代其他 Provider 性能、方法 CPU 或生产容量验收。

本机 .NET 8.0.30/Windows x64 的六项 InProcess ShortRun（3 次预热、3 次测量）完成：Insert 12.21→7.23 KB/次，Update 13.94→9.16 KB/次，Delete 10.36→6.42 KB/次，约少 34%～41%。报告保留在 `artifacts/benchmarks/crud-returning/current/`；仅比较同候选命令构建分配，不代表数据库吞吐、方法 CPU 或生产容量。

### 生成式命令直通前后

本次比较复用上方六格中的 `UseGeneratedSql=True` 作为优化前数据；基准源码、项目文件、模型、输入、生成 Getter、静态 Binder 和 ShortRun/InProcess 配置均未变。优化后渲染路径分配保持不变，生成路径的命令构造分配如下：

| 操作 | 优化前生成路径 | 优化后生成路径 | 分配减少 |
| --- | ---: | ---: | ---: |
| Insert | 7.23 KB/次 | 4.18 KB/次 | 3.05 KB（42.2%） |
| Update | 9.16 KB/次 | 5.47 KB/次 | 3.69 KB（40.3%） |
| Delete | 6.42 KB/次 | 3.56 KB/次 | 2.86 KB（44.5%） |

优化后的渲染路径仍为 Insert 12.21、Update 13.94、Delete 10.36 KB/次。复现命令：

```powershell
dotnet $benchmarkDll --filter '*SqlGeneratedReturningBenchmarks*' --job Short --inProcess --artifacts artifacts/benchmarks/crud-direct/after
```

`$benchmarkDll` 指向 `artifacts/validation/crud-direct/perf/after/managed/bin/Bing.Data.Sql.Benchmarks/Release/net8.0/Bing.Data.Sql.Benchmarks.dll`。原始 before/after 报告分别保留在 `artifacts/benchmarks/crud-returning/current/` 和 `artifacts/benchmarks/crud-direct/after/`；命令、输入与二进制 SHA256 见后者的 `after-evidence.json`。两次都是六格本地命令构造测量，不含数据库 I/O；after 报告未识别 CPU 型号，故只报告分配变化，不解读耗时差异，也不推导方法 CPU、吞吐或生产容量。

## Returning 子句克隆与映射查找

`SqlReturningClauseCloneBenchmarks` 对同一现有子句执行 `Clone`，覆盖 4、32、128 列；Setup 核对来源与克隆的完整 SQL。新旧实现使用相同基准源码、Windows x64、.NET 8.0.30、BenchmarkDotNet 0.14.0、InProcess/ShortRun（3 次预热、3 次测量）。克隆仍复制独立列表，复用不可变的 `SqlReturningColumn`；不重建每列对象和 LINQ 中间数组。

| 列数 | 修改前分配/次 | 修改后分配/次 |
| --- | ---: | ---: |
| 4 | 416 B | 128 B |
| 32 | 1,984 B | 352 B |
| 128 | 7,360 B | 1,120 B |

```powershell
dotnet $benchmarkDll --filter '*SqlReturningClauseCloneBenchmarks*' --job Short --inProcess --artifacts artifacts/benchmarks/returning-alloc/after
```

原始配对报告位于 `artifacts/benchmarks/returning-alloc/before/` 和 `after/`，命令、相关源码及程序集摘要见 [验证清单](../../../artifacts/ai-tasks/returning-alloc/verification.json)。ShortRun 的耗时只作为局部观测，不外推请求吞吐或生产容量。既有 `SqlReturningProjectionBenchmarks` 的两项匿名/DTO 构建场景另以 Dry 冒烟通过；映射查找改动由计数测试确认默认映射逐列一次字典查找、零集合扫描，自定义映射仍保留首项扫描语义，不将 Dry 数值解释为映射加速比例。

## 静态 Schema 完整生成 SQL

`SqlGeneratedMutationBenchmarks` 增加 `PostgreSQLSchema`，比较相同 `sales_2026` 模型下的渲染与常量路径，沿用六种操作和完整 SQL、静态 Binder、常量身份的 Setup 检查。已有四 Provider 分配探针的负载不变；Schema 场景通过 BenchmarkDotNet 参数选择。本次 12 格 InProcess/Dry 冒烟执行通过，只证明基准路径可运行，`performanceVerified=false`；不将 Dry 数值当作性能提升或生产容量结论。

```powershell
# $benchmarkDll 指向统一 artifacts/validation 目录中已构建的入口程序集。
dotnet $benchmarkDll --filter '*SqlGeneratedMutationBenchmarks*PostgreSQLSchema*' --job Dry --inProcess --artifacts artifacts/benchmarks/schema-sql/current
```

原始报告位于 `artifacts/benchmarks/schema-sql/current/`；构建命令、输出隔离属性及最终验证清单归档于 [schema-sql](../../../artifacts/ai-tasks/schema-sql/verification.json)。Schema 匹配检查在映射计划创建阶段完成，不新增每请求形状缓存。Oracle 本次有完整 SQL 和命令合同测试，没有纳入此基准。

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
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round122Perf/net8.0/Bing.Data.Sql.Benchmarks.dll --generated-key-insert-probe --output artifacts/benchmarks/sql/round122/key-allocation.json
~~~

.NET 8.0.30 Windows x64，预热 2048 次，每格 5×10000 次：普通写列 8056→5520 B/次；DEFAULT VALUES 3664→2440 B/次。探针先验证完整 SQL、常量引用、Returning 和静态绑定。是同候选分支比较，不覆盖固定历史 baseline，非正式 BenchmarkDotNet 门禁。原始样本及二进制身份见任务 Round122 收据。数据与包装器收据应使用不同文件名，避免互相覆盖。

## Round121 完整生成 CRUD SQL

`SqlGeneratedMutationBenchmarks` 在同一候选内比较完整生成 SQL 与逐次渲染，两个分支都使用相同生成 Getter、静态绑定器、列映射、实体值及独立预热计划。覆盖单实体 Insert/Update/Delete 的完整命令构建及参数快照，不包含数据库 IO，也不把兼容路径当作历史版本基线。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj --no-restore -c Round121Perf -p:ImplicitUsings=enable -p:UseSharedCompilation=false -m:1 -nr:false
dotnet framework/tests/Bing.Data.Sql.Benchmarks/bin/Round121Perf/net8.0/Bing.Data.Sql.Benchmarks.dll --generated-mutation-probe --output artifacts/benchmarks/sql/round121/allocation.json
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
node framework/tests/Bing.Data.Sql.Benchmarks/paired-probe.mjs --before-dir artifacts/validation/sql-paired/before --after-dir artifacts/validation/sql-paired/after --output-dir artifacts/benchmarks/sql/paired/run-01
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
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false
dotnet output/release/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryShapeBenchmarks*' --inProcess --warmupCount 3 --iterationCount 7 --invocationCount 1048576 --unrollFactor 16 --artifacts artifacts/benchmarks/sql/round17-shape-repro
dotnet output/release/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryShapePayloadBenchmarks*' --inProcess --warmupCount 2 --iterationCount 5 --invocationCount 256 --unrollFactor 16 --artifacts artifacts/benchmarks/sql/round17-payload-repro
```

本机结果和限制见任务 `performance.md`。二进制用例迭代时间不足，分配量可用于诊断，耗时不可作为性能门槛；所有结果均不能代表并发吞吐或生产容量。

## Round 18 生成比较常量

`SqlGeneratedConstantBenchmarks` 隔离测量预构造表达式的常量读取、数值逐次装箱和只读字段复用。该用例不测表达式树创建，也不测完整查询与数据库；只读字段有每个生成调用点的常驻成本。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false
dotnet output/release/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlGeneratedConstantBenchmarks*' --inProcess --warmupCount 3 --iterationCount 7 --invocationCount 1048576 --unrollFactor 16 --artifacts artifacts/benchmarks/sql/round18-constant-repro
```

本机迭代时间不足，分配结果可用于局部诊断，计时不可作为性能门槛。

## Round 19 生成查询描述

`SqlGeneratedDescriptorBenchmarks` 只模拟单列比较的固定类型数组、来源名数组和列描述逐次创建与静态复用，不包含表达式树、SQL 构造、数据库或并发，也不测静态描述的常驻内存。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false
dotnet output/release/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlGeneratedDescriptorBenchmarks*' --inProcess --warmupCount 3 --iterationCount 7 --invocationCount 1048576 --unrollFactor 16 --artifacts artifacts/benchmarks/sql/round19-descriptor-repro
```

本机迭代时间不足，分配结果仅用于局部诊断，计时和比值不作为性能门槛。详见任务 `performance.md`。

## Round 24 共享参数槽

`SqlQueryShapeBenchmarks.SharedSlot` 使用两个同名但默认值不同的片段，并在每次渲染时
显式提供当前请求值，测量已缓存形状的分配。它始终只有一个共享槽，`OptionalCount` 的两个
参数值不会改变该工作负载；不应将其视作两种不同查询。`StableShape` 对照两个或五个独立参数。
这些场景不覆盖冷形状编译、数据库执行、并发或大默认值比较。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false
dotnet output/release/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryShapeBenchmarks.SharedSlot*' --inProcess --warmupCount 2 --iterationCount 3 --invocationCount 131072 --unrollFactor 16 --artifacts artifacts/benchmarks/sql/round24-shared-repro
```

本轮前后对照、环境及限制见任务 `performance.md`；短迭代计时不作为性能结论。

## Round 31 固定尾部形状

`SuffixStableShape` 测量可选条件之后带 `ORDER BY ... LIMIT` 的缓存命中，
`SuffixChurningRoutes` 使用 512 个路由触发超过模板 256 项上限的持续未命中。
二者包含一个或四个可选条件以及独立的尾部参数，只测模板渲染，不包含数据库和并发。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false
dotnet output/release/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryShapeBenchmarks.Suffix*' --inProcess --warmupCount 2 --iterationCount 3 --invocationCount 16384 --unrollFactor 16 --artifacts artifacts/benchmarks/sql/round31-shape-suffix-final
```

分配数据及其限制见任务 `performance.md`；该短时探测不能用于判断吞吐或生产容量。

## Round 35 有序片段形状

`OrderedStableShape` 和 `OrderedChurningRoutes` 分别测量固定分组两侧包含可选条件的形状命中与路由持续未命中；
仍使用 1 或 4 个全部启用的可选条件。旧 `Suffix*` 工作负载继续用作相同语义的前后对照。
结果不包含模板构造、数据库和并发；新增 API 在旧版本没有等价工作负载。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false
dotnet output/release/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlQueryShapeBenchmarks.Suffix*' '*SqlQueryShapeBenchmarks.Ordered*' --inProcess --warmupCount 2 --iterationCount 3 --invocationCount 16384 --unrollFactor 16 --artifacts artifacts/benchmarks/sql/round35-after-results
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
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false
dotnet output/release/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlIntegerReaderBenchmarks*' '*SqlGeneratedAccessBenchmarks.GeneratedPlan*' --inProcess --warmupCount 2 --iterationCount 3 --invocationCount 1048576 --unrollFactor 16 --artifacts artifacts/benchmarks/sql/round46-reader-results
```

分配结果仅用于局部诊断，不能推导完整查询的延迟、吞吐或生产容量。映射缓存键、SQL 格式化及
Builder 克隆没有变化，不重复它们的历史基准。

## Round 48 生成参数与字典绑定

`SqlModelParameterBindingBenchmarks` 测量实际 `DefaultSqlParameterBinder` 的登记生成模型和严格/首选
字典路径，宽度为 1/8/32。修改前后使用相同的映射解析器、参数工厂、输入值和调用次数；生成模型
Getter 的值类型装箱属于工作负载本身。候选运行：

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false /p:UseSharedCompilation=false
dotnet output/release/net8.0/Bing.Data.Sql.Benchmarks.dll --filter '*SqlModelParameterBindingBenchmarks*' --inProcess --warmupCount 2 --iterationCount 3 --invocationCount 16384 --unrollFactor 16 --artifacts artifacts/benchmarks/sql/round48-after-results
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
dotnet run --project framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Round123Perf --no-restore -p:ImplicitUsings=enable -- --generated-mutation-probe --output artifacts/benchmarks/sql/round123/mutation-allocation-confirmed-data.json
```

最终原始样本 output/round123-mutation-allocation-confirmed-data.json，收据 output/round123-allocation-confirmed.json，源码与 Harness 身份 output/round123-results.json。每格 2048 预热、5×10000 次，.NET 8.0.30/Windows x64；12 组操作的生成 SQL 路径少分配 1824～3080 B/次。完整数值见任务 round123.md。

这是同候选分支比较，不是历史版本 before/after，也不包含数据库 IO、方法 CPU、真实并发、保留堆或容量。固定 Provider 分支增加生成代码/静态 SQL 体积，无每请求形状缓存。初次及 allocation-final 早于 Oracle 自定义方言兼容修正，最终采用 confirmed 候选；没有重建固定性能基线或自设门槛。

## InsertCombined：双行插入分配前后

本次用 `SqlGeneratedMutationBenchmarks.DoubleInsert` 对比 `InsertCombined` 的运行时 SQL 渲染与完整生成 SQL 路径。固定同一模型、输入、生成 Getter 和静态 Binder；每个 Provider 的 Setup 都核对完整 SQL 与生成执行标记，完整生成 SQL 另核对常量引用。两行 workload 的命令构造中位分配如下：

| Provider | 运行时渲染 SQL（before → after） | 完整生成 SQL（before → after） |
| --- | ---: | ---: |
| SQLite | 15,312 → 15,312 B/op | 12,184 → 7,152 B/op |
| MySQL | 15,184 → 15,072 B/op | 12,120 → 7,088 B/op |
| PostgreSQL | 14,936 → 14,936 B/op | 12,128 → 7,096 B/op |
| SQL Server | 14,936 → 14,936 B/op | 12,128 → 7,096 B/op |

四个完整生成 SQL 场景均减少约 5,032 B/op（基线的 41.30%～41.52%）。动态 MySQL 的中位数相差约 112 B/op；保留原始样本，不把小幅差异解释为收益。每格预热 2,048 次，随后 5 组、每组 10,000 次，指标为当前线程分配字节；环境为 .NET 8.0.30、Windows x64。计时样本只保留原始值，没有在共享宿主负载下用作 CPU 或吞吐结论。

本次测量时的构建与运行命令如下。临时探针和二进制在归档后清理；需要重测时，先从 `artifacts/ai-tasks/batch-sql/probe-source/` 与归档的构建属性恢复到统一验证目录。下列路径记录原始测量过程，不表示当前工作区仍保留前态二进制：

```powershell
dotnet build artifacts/validation/batch-sql/probe/InsertCombinedProbe.csproj -c Release -f net8.0 --artifacts-path artifacts/validation/batch-sql/probe/after -p:CustomBeforeMicrosoftCommonTargets=H:/Bing_Framework/Bing.NetCore/artifacts/validation/batch-sql/current/isolated-output.props -p:ImplicitUsings=enable -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true -p:UseSharedCompilation=false -p:Optimize=true -m:1 -nr:false
dotnet artifacts/validation/batch-sql/probe/before/bin/InsertCombinedProbe/release/InsertCombinedProbe.dll artifacts/benchmarks/batch-sql/before/double-insert-before.json H:/Bing_Framework/Bing.NetCore
dotnet artifacts/validation/batch-sql/probe/after/bin/InsertCombinedProbe/Release/net8.0/InsertCombinedProbe.dll artifacts/benchmarks/batch-sql/after/double-insert-after.json H:/Bing_Framework/Bing.NetCore
```

before / after 的八格原始样本及完整 SQL 分别保存在 `artifacts/benchmarks/batch-sql/before/double-insert-before.json` 和 `artifacts/benchmarks/batch-sql/after/double-insert-after.json`；前态源码身份来自采样前的独立 manifest，构建命令、两侧程序集与源码哈希、输出路径副作用及限制见 [归档收据](../../../artifacts/ai-tasks/batch-sql/probe-receipt.json)。此证据只覆盖运行时两行 `InsertCombined` 命令构造，不覆盖数据库 IO、源生成器启动、三行及以上输入、稳态 CPU、生产并发或容量。
