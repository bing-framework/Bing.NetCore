# SQL 性能验证工具

在 Windows 上从仓库根目录运行 `pwsh -NoProfile -File eng/ci/tests/Invoke-SqlBenchmarkSmoke.Tests.ps1`，可编译 CPU 采样工具并运行方法归因、源码快照和构建进程树测试。各项报告保存在这次冒烟运行的目录下，默认位于 `output/sql-benchmark-smoke`。

## PostgreSQL 并发读验证

从仓库根目录构建 SQL 基准。使用独立的 `bing_sql_aot_test` 库，并在当前终端设置 `RUN_POSTGRESQL_INTEGRATION_TESTS=true` 和 `ConnectionStrings__PostgreSqlConnection`。库可以由现有 [PostgreSQL Native fixture](../sql-aot/README.md) 提供。账户需要能在专用库的 `public` 架构创建测试表。每次运行在事务中创建并填充独立的主键表，完成后删除该表。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release
$benchmark = 'output/release/net8.0/Bing.Data.Sql.Benchmarks.dll'
$receipts = 'output/sql-capacity/run-' + [Guid]::NewGuid().ToString('N')
foreach ($concurrency in 1,16,64,256) {
    foreach ($path in 'runtime','template') {
        dotnet $benchmark --capacity-probe $path 10000 8192 $concurrency "$receipts/$path-$concurrency.json"
        if ($LASTEXITCODE -ne 0) { throw 'Capacity probe failed. Check the log and the report.' }
    }
}
```

两个路径使用相同的行数、请求序列和查询条件。一半请求按主键读一行，另一半按主键范围读最多八行。每次创建独立的服务作用域、Query 和连接租约，并核对完整结果。`runtime` 使用 Builder 的 `WhereIf`。`template` 使用同一份提前构造的 `SqlQueryShapeTemplate`，两种形状都在预热中执行，启用可选片段时复用下界参数。模板数据为手动声明，Source Generator、其他 SQL 形状和数据映射有各自的验证入口。

探针按固定并发发起请求，每个名额完成一次后领取下一次。闭环客户端的应用层入站队列 `maxQueuedRequests` 为 0。`concurrency` 是并发上限，`measure.peakInFlight` 是客户端已进入执行的请求峰值。它包含可能等待连接的请求。报告同时记录池配置，例如在连接串加入 `Maximum Pool Size=32` 可统一比较池配置。每档记录成功吞吐、成功请求 P50/P95/P99、失败数、客户端进程累计 CPU、托管分配和 GC 次数。延迟包括作用域创建到查询、结果校验和释放。预热、建表、报告写入和分位排序在测量窗口之外。退出码 0 表示预热与测量的查询均成功，1 表示有请求失败并保留收据，2 表示参数、夹具或报告写入错误。每个输出路径仅创建一次。

另跑一次带 `--observe-server` 的诊断。它从独立的非池化管理连接查询 `pg_stat_activity`，按本次探针的 `application_name` 过滤，只采集测量窗口内的活跃会话。

```powershell
dotnet $benchmark --capacity-probe runtime 10000 8192 64 "$receipts/runtime-observed.json" --observe-server
```

开启后，`serverActivity` 记录每次采样之间的最短间隔（10 毫秒）、有效样本数、采到的活跃会话峰值及其中等待 PostgreSQL `Lock` 的会话峰值。测量太短而没有完整样本时，两项峰值为 `null`。关闭时 `serverActivity` 为 `null`。要求测试库启用 `track_activities`。这项诊断会额外查询数据库，并计入客户端进程的 CPU 和分配。比较两条路径的吞吐和延迟时，可用上面的默认命令。

当前 Npgsql 6.0.11 通过 `EventCounters` 提供忙闲连接数。代表性容量验收仍需独立采集连接池等待，并取得业务请求组合、数据规模、目标并发、延迟与错误阈值、数据库资源和 CPU 采样。

```powershell
dotnet run --project eng/perf/tests/SqlCapacityLoad.Tests/SqlCapacityLoad.Tests.csproj -c Release
pwsh -NoProfile -File eng/perf/tests/SqlCapacityProbe.Tests.ps1 -BenchmarkAssembly $benchmark -ResultsDirectory output/sql-capacity-tests -RunPostgreSql
```

去掉 `-RunPostgreSql` 可只跑门控及库名校验。基础并发结果和服务端采样记录分别见 [Round155](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round155.md) 和 [Round157](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round157.md)。

## 校验 PostgreSQL 容量收据

v2 收据记录探针、SQL 核心、PostgreSQL Provider 和数据库驱动等七个程序集的 SHA256。探针在请求计时结束后读取程序集。验收时指定事先选定的候选构建目录，脚本逐个比对摘要，再检查成功请求每秒下限、成功请求 P99 毫秒数上限和失败率上限。失败率采用 0 到 1 的比例，例如 0.01 表示 1%。数字参数使用小数点。

```powershell
$candidateBuild = 'output/release/net8.0'
pwsh -NoProfile -File eng/perf/Invoke-SqlCapacityGate.ps1 `
    -ReceiptPath $receipt -ExpectedBuildDirectory $candidateBuild `
    -MinRequestsPerSecond $minRequestsPerSecond -MaxP99Milliseconds $maxP99Milliseconds `
    -MaxFailureRate $maxFailureRate
```

脚本输出一份 JSON 判定，包含输入收据的 SHA256、候选程序集摘要、阈值、实际指标及全部超标项。等于阈值时通过。预热有失败或测量没有成功请求时判为失败。退出码 0 表示 `PASS`，1 表示有效收据的指标 `FAIL`，2 表示阈值或收据 `INVALID`。阈值由调用方指定，判定适用于输入收据对应的负载。正式验收需要代表性环境的业务负载和独立的池等待、数据库资源及 CPU 证据。

v2 通过构建校验时输出 `identityScope=sql-assemblies`。复核 v1 历史收据时，使用 `-ExpectedBenchmarkSha256 <原基准入口摘要>`，输出 `identityScope=benchmark-only`。收据版本与所选参数不符或所需程序集摘要缺失、错配时，脚本返回 `INVALID`。

可以用 `-ExpectedWorkloadPath <文件>` 将判定绑定到本次预期的固定读负载。配置文件由调用方在验收前确定。以下示例对应 67 行、2,048 次请求、8 路并发、最大连接池 2 且开启服务端采样的诊断收据。

```json
{
  "path": "runtime",
  "rows": 67,
  "requests": 2048,
  "concurrency": 8,
  "pooling": true,
  "maxPoolSize": 2,
  "observedServer": true
}
```

将以上 JSON 保存为 `expected-workload.json`，并在校验命令末尾追加 `-ExpectedWorkloadPath expected-workload.json`。七项都必须填写，数字与布尔值按 JSON 类型提供。负载错配返回退出码 2，`workloadMismatches` 给出错配的字段。配置仅支持固定读探针的七项字段。`observedServer` 记录是否启用诊断，采样结果见原始收据。省略该参数时沿用单份收据的阈值判定。

职责测试无需数据库。Release 基准冒烟也执行此测试。

```powershell
pwsh -NoProfile -File eng/perf/tests/SqlCapacityGate.Tests.ps1
```

## 构建进程树资源测量

在 Windows PowerShell 7 中，从仓库根目录运行：

```powershell
pwsh -NoProfile -File eng/perf/Invoke-SqlCleanBuildPeak.ps1
```

可通过 `-ResultsDirectory <目录>` 指定报告父目录，`-RestoreConfigFile <NuGet.Config>` 指定离线源配置，`-TimeoutSeconds <秒>` 指定整棵进程树的观察窗口（默认 900 秒）。工具不下载 SDK，也不改动源仓库的构建产物；SDK 由现有 `global.json` 决定。默认源配置可能访问网络；离线配置必须能从宿主已有 NuGet 缓存恢复全部依赖。

每次在新建的 `run-<GUID>` 内复制根构建配置、EditorConfig、asset、framework/src 和 SQL 基准工程。复制时排除 bin、obj、.codex-build、工程同名生成 XML 文档等历史产物及目录链接，保留 UTF-8 字节，记录逐文件 SHA256。该副本从无中间产物的状态构建 `Bing.Data.Sql.Benchmarks.csproj` 的完整项目引用图，Release、单 MSBuild 节点、关闭编译服务器及节点复用；包含 restore，复用宿主包缓存。它不构建整个 Bing.All.slnx，也不清空包缓存。

`BuildProcessTree.cs` 在恢复根进程前把它加入 Windows Job，子孙继承同一 Job；等待 Job 中全部进程退出，根进程提前退出不会提前结束测量。超时终止整棵进程树并保留失败报告。标准输出与错误输出写入同一个 build.log，不经过 shell。

每份 result.json 包含命令、SDK/OS、源码 manifest、工具与入口程序集摘要、退出状态，以及：

| 字段 | 含义 |
| --- | --- |
| `PeakJobCommittedBytes` | 整棵进程树在同一时刻的提交内存峰值；包括已退出的短命子孙造成的峰值。 |
| `PeakProcessCommittedBytes` | Job 内单个进程出现过的最大提交内存峰值，不能与整树峰值相加。 |
| `TotalProcesses` | 整个 Job 累计加入的进程数，包括根进程及已退出子孙。 |
| `UserTimeTicks` / `KernelTimeTicks` | Job 累计用户/内核 CPU 时间，单位 100 ns；不是方法级归因。 |
| `ElapsedMilliseconds` | 从创建构建进程到整棵树结束的墙钟时间，不包含复制源码与编译测量器。 |

提交内存口径对应 Windows 的 [JOBOBJECT_EXTENDED_LIMIT_INFORMATION](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_extended_limit_information)。结果不表示工作集、RSS、整机物理内存峰值，也不用于推导 SQL 请求容量。没有修改前对照或用户认可的阈值时，只报告测量结果，不声称性能改善或发布容量通过。

职责测试不依赖 Pester 或额外 NuGet 包：

```powershell
pwsh -NoProfile -File eng/perf/tests/SqlCleanBuildPeak.Tests.ps1
pwsh -NoProfile -File eng/perf/tests/BuildProcessTree.Tests.ps1
```

前者验证构建副本及输入身份，后者使用真实子孙进程验证 Job 统计、提前退出、参数、失败日志和超时清理。测试证据默认写入 output/round135-tests。实际构建收据与功能测试分别保存；本工具未改变 SQL 运行时、基准工作负载或现有 before/after 基线。

## 托管方法 CPU 归因

`SqlCpuTrace` 是本地固定 SQL Builder 探针的诊断工具，不属于 SQL 公共 API。复用已有 Release 基准目录中的 DiagnosticsClient/TraceEvent 程序集，不安装 dotnet-trace，不恢复新的诊断依赖。先构建现有基准（也可使用上述干净构建工具的最终输出），然后从仓库根目录运行：

```powershell
$benchmarkDirectory = (Resolve-Path 'output/release/net8.0').Path
# 如使用独立构建收据，将上面替换为该收据 binary.file 的父目录。
dotnet build eng/perf/SqlCpuTrace/SqlCpuTrace.csproj -c Release --disable-build-servers -p:UseSharedCompilation=false "-p:DiagnosticAssemblyDirectory=$benchmarkDirectory"
dotnet eng/perf/SqlCpuTrace/bin/Release/net8.0/SqlCpuTrace.dll --self-test
dotnet eng/perf/SqlCpuTrace/bin/Release/net8.0/SqlCpuTrace.dll capture "$benchmarkDirectory/Bing.Data.Sql.Benchmarks.dll" output/sql-cpu 120
```

离线时 build 可增加 `-p:RestoreConfigFile=<已有离线 NuGet.Config>` 与 `-p:NuGetAudit=false`。工具引用的程序集来自实际 benchmark 输出；本轮实际版本是 DiagnosticsClient 0.2.251802 / TraceEvent 3.1.8，不暗示其他版本已验证。

每次生成新的 run-GUID，保存 capture.json、binary-manifest.json、cpu.nettrace、probe.json、标准输出/错误，以及 report 下的 cpu.etlx、conversion.log、samples.jsonl、methods.csv、summary.json。进程在托管入口前暂停，接入目标诊断端口后恢复；固定执行 `--builder-state-probe`，无需开启系统级 WPR 采样策略。窗口 1～300 秒；超时先结束自有进程再释放会话，失败返回非零。CPU 分析不重新运行探针，原始 trace 可重放：

```powershell
dotnet eng/perf/SqlCpuTrace/bin/Release/net8.0/SqlCpuTrace.dll analyze <cpu.nettrace> <capture.json中的PID> <新的报告目录>
```

已有报告目录拒绝覆盖。`samples.jsonl` 每行保存 PID、线程、相对时间、实际枚举 kind（External=1、Managed=2）和叶到根的方法栈。方法表的 Self/Inclusive 百分比分母是该 PID 的 Managed 样本数，递归方法包含权重在同一样本内只计一次；包含百分比不能跨方法求和。缺失/未解析栈保留在报告，不通过删除它们改善覆盖率；空证据、丢事件、仅基准方法或仅未知地址不能通过 SQL 方法验证。

EventPipe 提供的是托管执行状态采样，External 不等同于等待。此入口包含启动、JIT、探针构造、对照策略和输出，不是纯稳态 Query；样本数不能换算成精确 CPU 毫秒、整机 CPU 百分比或生产容量，原生/内核未覆盖。口径依据见 [Microsoft EventPipe](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventpipe) 与 [SampleProfiler 枚举](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/SampleProfilerTraceEventParser.cs)。实际结果和边界见 [Round136](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round136.md)。

真实集成验证使用专用 fixture，不连接数据库；CapturedRun 必须是上述实际采样目录：

```powershell
dotnet build eng/perf/tests/SqlCpuTraceFixture/SqlCpuTraceFixture.csproj -c Release
pwsh -NoProfile -File eng/perf/tests/SqlCpuTrace.Integration.Tests.ps1 -CapturedRun <run-GUID目录>
```

七项汇总职责测试随 `--self-test` 运行；三项集成测试覆盖非零退出/中文日志/trace、真实超时清理、原始数据重放/错误 PID/拒绝覆盖。测试调用进程自身上限为 30 秒。工具/fixture 的构建与测试结果独立于 SQL 单元和 SQLite/Native 验证，不互相替代。
