# SQL 性能验证工具

在 Windows 上从仓库根目录运行 `pwsh -NoProfile -File eng/ci/tests/Invoke-SqlBenchmarkSmoke.Tests.ps1`，可编译 CPU 采样工具并运行方法归因、源码快照和构建进程树测试。各项报告保存在这次冒烟运行的目录下，默认位于 `artifacts/benchmarks/sql-smoke/run-<批次标识>`。

新增性能测量的报告、日志和探针输出统一使用 `artifacts/benchmarks/<任务或套件>/<运行批次>/`；临时源码、构建和中间文件使用 `artifacts/validation/`。BenchmarkDotNet 命令必须显式传入 `--artifacts`，诊断探针必须指定对应的输出参数，不在 `ai_docs/tasks/` 中创建运行产物。正式发布验证沿用 `artifacts/benchmarks/release-candidate/`。

2026-11-25当前候选验收见[证据](../../artifacts/ai-tasks/r02-rc/verification.json)：干净构建378.406MiB/16.737秒通过；正式C1为2402.49 req/s，低于2500，P99=4ms且零失败，首格FAIL后停止余下档位。175个运行文件在执行前后身份一致，应用/数据库CPU、内存和IO观测及清理通过。当前没有新的方法级CPU栈；不能将历史CPU报告或不同候选容量结果合成当前PASS。

本轮新增完整运行清单和可复用入口，工具职责检查59项通过。既有[完整五格证据](../../artifacts/ai-tasks/r02-final/verification.json)属于旧候选：容量4/5通过、C1=2405.39req/s未达标，构建356.262MiB/18.433秒通过；仅作历史记录。

本阶段新增 `SqlMixedStageDiagnostic-v2`：四阶段提供0.1ms固定桶的P50/P95/P99（100ms上限，溢出分位数为null，保留真实最大值），记录热路径零额外分配、固定约32KiB。仅显式诊断模式扩展已有100ms数据库采样，按application_name累计互斥的Lock/WALSync/WALWrite/其他IO/无等待事件/其他等待会话观测。次数不是等待时长，无等待事件也不能认定为CPU执行；v1历史报告继续可重放，正式容量门拒绝两个诊断版本。

一次1C2G诊断完成236460次测量、零失败，读执行P99=0.7ms、写执行P99=21.4ms；614次活动会话观测中WALSync=412。服务端UPDATE均值35.808微秒、客户端写执行均值2.186ms，WAL同步累计43.449秒；这是优先核实持久化等待的线索，不是逐请求提交归因。诊断入口/PDB重编译，其余173个运行文件逐字节复用r02-rc；不是原正式容量候选完整身份，诊断吞吐1970.49req/s不用于达标或回退判断。

原诊断编排因应用io.stat设备在测量结束前约6秒出现而FAIL，原始收据保留；阶段/等待报告校验通过。汇总器修正为只检查实际包围区间内IO一致性，区间内变化仍拒绝。原样本重放数据库资源PASS，应用NOT_VERIFIED；没有重跑压测。职责测试118项及短时真实PostgreSQL集成3项通过，证据见 `artifacts/ai-tasks/r02-tail/verification.json`。本阶段不改变下文正式C1的FAIL，也没有新正式构建或方法CPU采样。

## 临时卷 WAL 同步诊断

`Invoke-SqlWalSyncProbe.ps1` 只使用本机已有 PostgreSQL 16 镜像，在无网络、只读根文件系统的 1CPU/512MiB 容器上运行一次 `pg_test_fsync`。工具文件位于本次独占 named 磁盘卷的 `/var/lib/postgresql/data/fsync.out`，不启动数据库，不访问既有卷，不修改 fsync/synchronous_commit/wal_sync_method。镜像先解析为不可变 ID；收据保留实际 cgroup、镜像和工具摘要、文件系统、原始输出及清理退出码。默认每项1秒、命令超时180秒，无自动重试。临时卷与容器结束即删除，清理失败不能报告完成。

```powershell
pwsh -NoProfile -File eng/perf/Invoke-SqlWalSyncProbe.ps1 `
    -ResultsDirectory artifacts/benchmarks/<task>/<batch>/wal-sync
pwsh -NoProfile -File eng/perf/tests/SqlWalSyncOutput.Tests.ps1 `
    -ResultsDirectory artifacts/validation/<task>/<batch>/parser-tests
pwsh -NoProfile -File eng/perf/tests/SqlWalSyncProbe.Tests.ps1 `
    -ResultsDirectory artifacts/validation/<task>/<batch>/probe-tests
```

`Get-SqlWalSyncSummary.ps1` 中的 `ConvertFrom-SqlWalSyncOutput` 可离线解析保留的 UTF-8 原始日志。`SqlWalSyncDiagnostic-v1` 只提取一/二次8KiB写入组中的五种同步方法及终端非同步写入；后续块大小和跨文件描述符实验保留原文。n/a/n/a*对应UNSUPPORTED和null数值，不伪造零延迟；微秒整数舍入为0仍合法。缺组、重复方法、坏数字、截断输出会被拒绝。文件同步操作率不能送入正式业务容量门禁，工具不自动挑选或应用“最佳”同步配置。

2026-11-25一次实测：PG16.14、WSL2共享Docker宿主、独立local named卷、实际cpu.max=100000 100000、memory.max=536870912、swap=0。单块fdatasync/fsync均值1549/2520微秒，双块1343/1989微秒，fsync_writethrough不支持。每项仅1秒、无重复统计，顺序执行的方法没有随机化，不能凭均值排名改配置；非同步写入的0微秒为整数舍入。当前同步路径存在毫秒量级开销，与此前WALSync/写入尾延迟线索一致，但不是逐请求归因、业务P99、当前方法CPU或物理断电持久性证明。1个容器/1个卷清理退出码0，独立库存核对均不存在，证据 `artifacts/ai-tasks/r02-wal/verification.json`。原正式C1和应用IO未验证状态不变；本轮没有重新运行容量或构建峰值。

## 同候选本地容量验收

先生成新干净构建收据，再将其作为唯一运行输出来源。入口读取已批准的 `r02-local-budget.json`，默认依次运行并发1/8/16/32/64，首个预算失败即停止；`-Concurrency 1` 可限定为单格。结果目录必须不存在，不自动重试或拉取镜像。

```powershell
pwsh -NoProfile -File eng/perf/Invoke-SqlCleanBuildPeak.ps1 `
    -ResultsDirectory artifacts/benchmarks/<task>/<batch>/build `
    -RestoreConfigFile <explicit-NuGet.Config> -TimeoutSeconds 900
pwsh -NoProfile -File eng/perf/Invoke-SqlLocalCapacity.ps1 `
    -BuildReceiptPath <new-build-run>/result.json `
    -ResultsDirectory artifacts/benchmarks/<task>/<batch>/capacity
pwsh -NoProfile -File eng/perf/tests/SqlLocalCapacity.Tests.ps1 `
    -ResultsDirectory artifacts/validation/<task>/<batch>/tests
```

新入口强制验证完整运行清单，预算门的 `-ExpectedRuntimeManifestSha256` 对旧调用方保持可选；未传参数的旧重放只证明原入口DLL合同。容量入口在开始时固定输入收据、源码/运行清单及预算摘要，前后重新核验输出文件集合及全部文件内容。调用者负责提供本次明确选定的收据；该工具不独立认证外部发布身份或预算审批。

正式运行保持 `--mixed-capacity-probe`，不加入诊断包装或采样器。仅使用本地已有镜像，在内部网络创建应用1CPU/2GiB与数据库1CPU/512MiB临时容器、swap0及named磁盘卷，结束清理；任何清理失败记为INVALID。宿主构建快照用于构建到容量的交接，由执行阶段在归档证据后显式清理，容量入口不隐式删除调用者输出。本次已清理3313个临时文件约166.52MiB，原始报告和一个逐条摘要验证的压缩候选包保留在统一证据目录；原收据中的历史绝对路径不因解压自动更新。

本次应用/DB的CPU为44.534%/32.227%（121秒括号区间，各1CPU），memory.current采样峰值约104.64/148.91MiB。进程CPU53390ms、分配8133826136字节包含harness与全部线程，不是逐方法或逐请求分配归因；IO设备计数不是物理等待。下一步优先核对同候选读/写延迟及持久化等待，不以继续添加缓存替代原因定位。

## 规划热路径最小分配验证

2026-11-25完成一次新候选确认，见[证据入口](../../artifacts/ai-tasks/r02-opt/verification.json)。无空白名称解析跳过正则和别名分词，public.samples由520降至320B/op（38.46%），不增加缓存。GetPlan首次终结13448B/op、Clone3272B/op保持不变；GetPlan探针包含新查询描述，输入Builder已准备，三个操作都没有数据库IO。

~~~powershell
dotnet <candidate-directory>/Bing.Data.Sql.Benchmarks.dll --planning-hot-path-probe --output artifacts/benchmarks/<suite>/<batch>/planning.json
~~~

输出目录由调用方预先建立，文件拒绝覆盖。每项预热4096次、5组各5000次；同线程分配是诊断口径，时间不是容量预算或统计显著性证明。BenchmarkDotNet同类入口为SqlPlanningHotPathBenchmarks，运行时仍须显式设置--artifacts。

同一干净构建的C1固定窗口为2247.41req/s、P99=4ms、零失败，吞吐未达2500；构建406.930MiB/16.122秒通过。应用/数据库CPU约49.96%/31.24%，仍不能确认吞吐缺口的唯一原因。新旧容量各一次且候选不同，不由此判断吞吐回退；下一步先做有限的阶段耗时诊断，再选计划或克隆优化。此次未重跑新候选完整五格或CPU采样，旧五格/方法报告仍只代表原候选。

connectionAcquisition现在同时记录provider、driver、实际AssemblyVersion、reasonCode和occupancyIsQueueDuration。当前Npgsql6.0.11.0报告NOT_INSTRUMENTED / DRIVER_QUEUE_TIMING_UNAVAILABLE；未知版本报告UNKNOWN_CAPABILITY。这只是能力报告，不是纯排队埋点。6.0.11等待连接发生在驱动ConnectorPool内部Channel，现有EventSource未提供该时段；后续需驱动级事件或明确批准的补丁，覆盖成功/取消/超时及重试段。整个OpenAsync总耗时不能替代。

新代码尚未形成远端候选，公开GitHub API本次403限流；没有新候选外部CI或下载重放结果。授权形成候选后，使用既有sql-aot.yml的native-summary下载全部sql-aot-*及sql-regression工件，再按实际仓库、run、SHA、attempt和Job结论调用verify.mjs --ci-summary重放；旧R01不能代替。

## 显式请求阶段诊断

可选 `-CpuCollectorDirectory <SqlCpuTrace-output>` 在同一次诊断中采集托管 CPU 栈。采集器通过已有 `/results/cpu-tool` 挂载提供，候选 `/app` 保持只读；不能用 `docker cp` 写入只读根文件系统。未传该参数时保持原来的直接阶段调用。

```powershell
pwsh -NoProfile -File eng/perf/Invoke-SqlMixedStageDiagnostic.ps1 `
    -PublishDirectory <benchmark-directory> -CpuCollectorDirectory <collector-directory> `
    -ResultsDirectory artifacts/benchmarks/r02-stage-cpu/new-run
```

CPU 模式的阶段报告在 `app/cpu/run-*/probe.json`，同目录保存原始 `cpu.nettrace`、采集收据和 `report/`。两个混合入口均强制要求唯一测量窗口，缺失标记时不能退回整段进程。入口核对候选、采集器及 trace SHA256、PID 和窗口；组件/方法百分比只代表该窗口的托管采样。`probe.json` 的 `processCpuMilliseconds`、`allocatedBytes` 和 GC 差值是进程汇总，未采集按类型/调用栈分配事件。采集器与应用共享应用容器的 1CPU/2GiB 限制，容器资源统计包含采集器；诊断吞吐不能替代无采样正式容量。

本次 C1 同窗观测见 [证据](../../artifacts/ai-tasks/r02-stage-cpu/verification.json)：313141次零失败，诊断2609.5req/s、P99=2ms；进程CPU74300ms、累计分配9132400904字节（含harness/all threads，约29163.86B/次）。托管23124样本，规划/执行/驱动最近owner18.29%/21.77%/15.28%，0丢事件、288未解析。比例不是精确CPU时间；Linux中的Interop+Kernel32标签也不证明Windows原生执行，平台符号未独立解析。89项检查通过，原正式C1=2247.41req/s的FAIL保留。首次docker cp启动失败收据保留，改用结果挂载后仅执行一次负载，临时资源均清理。

固定本地拓扑的可复用入口如下，使用本地已有 Linux .NET 镜像，不拉取或改变 Docker 配置：

```powershell
pwsh -NoProfile -File eng/perf/Invoke-SqlMixedStageDiagnostic.ps1 `
    -PublishDirectory <benchmark-directory> `
    -ResultsDirectory artifacts/benchmarks/r02-stage-linux/new-run
pwsh -NoProfile -File eng/perf/tests/SqlMixedStageDiagnostic.Tests.ps1 `
    -ResultsDirectory artifacts/validation/r02-stage-linux/tests
```

入口只运行 1C2G/C1，固定使用已批准的 100K、90:10、30/120 秒和池 32。应用 1CPU/2GiB、数据库 1CPU/512MiB，验证实际 cgroup、swap=0 和持久化三项 on。临时内部网络不发布端口，数据库数据使用独占 named 磁盘卷；结束删除本次容器、网络和卷。资源采样脚本 `Sample-SqlContainerResources.sh` 每秒记录原始 cgroup，既有汇总工具按测量窗口括号计算 CPU/内存/IO。结果目录必须全新，入口不自动重试；失败收据和日志保留。

`environment.json` 记录镜像、运行时、负载和程序集身份、清理结果；`app/stages.json` 记录阶段数据及数据库/连接池观察；两个 `*-resources.json` 保存容器资源汇总。该入口的 `DIAGNOSTIC_COMPLETE` 仅表示诊断执行完成，`capacityBudgetEvaluated=false`，不判定正式吞吐预算或生产容量。正式容量仍须独立运行原入口与门禁。

前置阶段诊断见 [原证据](../../artifacts/ai-tasks/r02-stage-linux/verification.json)：334597次测量、零失败，组装/冻结/读取执行/写入执行均值分别20.771/16.331/214.396/1280.965μs，执行占四阶段ticks的90.05%。当时13项入口测试、环境9项、资源7项、容量门禁12项通过；该诊断未拆分连接打开。

历史连接拆分见 [证据入口](../../artifacts/ai-tasks/r02-open/verification.json)：相同批准拓扑和负载，259286次测量零失败，Open平均0.679372μs，占执行ticks约0.162009%；读取执行250.172495μs、写入执行1941.915355μs。Open在本次C1热池负载中累计占比很小，下一项优先细分命令IO、写入等待及读取物化，不外推高并发池竞争。新增包装改变了诊断harness，2160.708req/s与前置诊断/正式路径不作提升或回退比较；此前正式C1的FAIL保留。26项C#职责、18项报告合同、12项容量门禁及3项真实PG集成场景通过，临时Docker资源删除并独立核验。

历史服务端观测见 [证据入口](../../artifacts/ai-tasks/r02-command/verification.json)：100K/90:10/30秒预热/120秒测量、C1/池32，293919次测量零失败。服务端读/写平均17.772/28.222μs，客户端执行236.080/1557.834μs；集群WAL同步累计37.293秒，仅提示进一步核实持久化等待，不解释为精确的逐请求归因。本次诊断2449.317req/s、P99=3ms，不代替正式容量预算或旧候选的FAIL。初次编排在请求完成后因应用io.stat全为空而拒绝资源汇总；已补职责测试修复，将IO标记NO_DEVICE_COUNTERS，CPU/内存仍按原窗口汇总，并重放原始样本，不重跑负载、不改写原FAIL收据。31项C#、32项诊断合同、12项门禁、10项资源职责、3项真实PG集成共88项通过。

服务端诊断同时要求 PostgreSQL 16 预加载 `pg_stat_statements`，并打开 `track_io_timing`、`track_wal_io_timing`；上述容器入口会显式配置并读回，不修改宿主数据库。探针仅在诊断模式安装扩展，按唯一测试表和 SELECT/UPDATE 过滤统计，在预热后及测量 drain 后各取一次快照。`measurement.diagnosticDatabaseCommands.read/write` 分别记录 calls、rows、执行毫秒、共享块、块 IO 计时及 WAL 生成量，calls/rows 必须与客户端请求一致；`wal` 是整个临时集群的异步 WAL 写入/同步差值。两类统计的 reset 改变、计数倒退或非有限值会拒绝诊断。空调用均值为 null。

语句执行时间不含客户端读取/物化和隐式提交；WAL 同步时间是集群累计值，统计存在刷新滞后，也可能包含后台活动。不能将客户端执行减去语句时间直接命名为“映射耗时”，不能用 WAL 均值替代逐请求提交延迟，更不能把这些墙钟时间解释为 CPU 时间。诊断增加数据库观测开销，不能与旧候选直接比较容量。普通容量模式不安装扩展、不读取这些统计，字段为 null。

新增 --mixed-stage-probe <workload.json> <profile> <concurrency> <new-report.json>，参数和数据库/cgroup门控与正式混合入口相同；只用于定位耗时，不用于预算验收。输出schema为SqlMixedStageDiagnostic-v1，正式容量门禁拒绝该格式。建议先使用1C2G/并发1执行一次已固定窗口的诊断，不因失败反复重跑。

measurement.diagnosticStages包含四项：scope-and-compose、read-freeze-plan、read-execute-and-materialize、write-execute，分别报告完成阶段的次数、原始Stopwatch ticks、均值及最大毫秒数。diagnosticStopwatchFrequency保留换算频率；预热不计入，测量及已发出请求收尾后生成快照。累加器固定12个long槽位，不保留逐请求列表。未完成或异常退出的阶段没有样本，结合负载失败数与阶段计数解读；不将阶段均值相加当作端到端P99。

诊断路径显式创建内部SqlQuery计划后交给同一根执行器，额外查询描述成本包含在冻结阶段；它不等同正式终结路径的无干扰测量。执行阶段仍包含连接获取、驱动/数据库执行和结果物化；校验及资源所有者释放不在阶段总计内。普通入口仍使用原ToListAsync终结流程，diagnosticStages与diagnosticConnectionOpen均为null。

新增 measurement.diagnosticConnectionOpen，记录测量及 drain 期间所有 Open/OpenAsync 尝试的次数、失败数、总 ticks、最大 ticks 及均值/最大毫秒数；预热使用原生连接，不进入统计。只在诊断模式经现有连接工厂包装 DbConnection，命令、参数和 Reader 保留 Provider 原始实现。四个 long 累加槽位按次运行创建，不保存逐请求记录，也不新增生产计时接口。该包装仅用于当前无事务混合探针，不能作为事务 Scope/Uow 的通用连接代理。

连接打开耗时包含连接池获取、必要的建连/认证和驱动开销，不等于纯连接池排队。零失败且打开次数与请求数一致时，可用两个执行阶段总 ticks 减去打开总 ticks 得到执行余量；余量仍包含参数准备、命令 IO、读取/物化及命令清理，不等于数据库服务端时间或 CPU 时间。新增打开统计是执行阶段的子集，不能与原四阶段再次相加。容器入口校验计数、失败和总 ticks 嵌套关系，拒绝缺少该字段的旧诊断报告。

短时真实集成同时覆盖普通并发8及诊断并发1，每项2000行、预热1秒/测量3秒、池2；验证阶段计数对应读写请求、排除预热和测试表清理。该测试仅验证接线，不能解释此前Linux1C2G的吞吐缺口，也不是新候选容量/外部CI通过。

## PostgreSQL 并发读验证

### R02 已固定的探索负载与真实资源环境

`r02-workload.json` 保存用户于 2026-11-25 确认的探索负载：100,000 行、读取/主键更新 90:10、预热 30 秒、正式测量 120 秒、并发 1/8/16/32/64、池大小 32，独立且固定的非生产 PostgreSQL。负载文件中的历史预算仍为null；用户已另行批准本地回归预算，见r02-local-budget.json，正式门禁显式接收批准值。历史探索结果不能据此自动升级为PASS。

在具有足够空闲资源的 Linux Docker 宿主逐档执行。脚本先检查引擎 CPU/总内存，再创建具有 CPU quota、内存硬限制、禁用 swap 的容器，并读取 cgroup v2 的 `cpu.max`、`memory.max` 和 `memory.swap.max` 验证实际限制。镜像必须已在本地存在且提供 `sh`、`sleep` 和目标 .NET 运行时；挂载目录应为 Linux 兼容的发布输出，不能复用 Windows Native AOT 可执行文件。

```powershell
pwsh -NoProfile -File eng/perf/New-SqlCapacityEnvironment.ps1 `
    -Profile 2C4G -CheckHostOnly `
    -ResultsDirectory artifacts/benchmarks/r02/host-check/new-2c

pwsh -NoProfile -File eng/perf/New-SqlCapacityEnvironment.ps1 `
    -Profile 2C4G -Image <local-runtime-image> -PublishDirectory <linux-compatible-publish> `
    -ResultsDirectory artifacts/benchmarks/r02/environment/new-2c
```

`environment.json` 中 READY 仅表示环境可用，并记录实际 image ID 与 container ID。应用在 `/app`，结果写入 `/results`，容器保留供调用方通过 `docker exec` 执行负载，结束后按收据中的 ID 执行 `docker rm -f <containerId>`。失败时仅清理本次创建的容器。数据库配置通过执行进程的环境变量传入，不写入收据。一次只测一档；引擎资源还必须给 Docker、数据库以外的任务及系统开销留出空间。宿主总量校验不能证明无邻居干扰或物理核独占。

2026-11-25 本机实际引擎为 4 CPU、4,108,902,400 字节内存，原三档前置校验均失败，既有证据在 `artifacts/benchmarks/r02/host-check/`。用户随后将本阶段范围降低为 1C2G，已完成真实容器创建、实际限制核对和五档混合负载；最终证据在 `artifacts/ai-tasks/r02-1c/`。没有扩大或重启 Docker，也没有以 oversubscription 产生原三档结果。单元测试另覆盖限制解析、拒绝行为及 Docker 替身下的创建参数核对。

```powershell
pwsh -NoProfile -File eng/perf/tests/SqlCapacityEnvironment.Tests.ps1 `
    -ResultsDirectory artifacts/validation/r02/environment
```

数据库 CPU、内存、磁盘与池排队需独立采样并与测量窗口对齐。现有 `--observe-server` 仅表示活跃/锁等待会话，既不代表数据库 CPU/IO，也不代表客户端连接池排队。容器的 `docker stats` 可保留原始资源样本；独立数据库仍须部署方提供对应的服务端资源证据。

已有 `Invoke-SqlCleanBuildPeak.ps1` 继续负责 Windows Job 进程树峰值采集。新增门禁复核该收据、源码清单 SHA256 和实际程序集 SHA256，再按调用方提供的已批准 committed MiB 预算判定；审批标识只是可审计引用，工具不会独立认证审批有效性。它衡量 committed bytes，不等于物理 RAM 峰值。预算未批准时保留测量结果，不运行带自定阈值的正式门禁。

```powershell
pwsh -NoProfile -File eng/perf/Invoke-SqlBuildBudgetGate.ps1 `
    -ReceiptPath <build-result.json> -ExpectedSourceManifestSha256 <approved-source-manifest-hash> `
    -ExpectedBinarySha256 <candidate-binary-hash> -BudgetApprovalId <approval-reference> `
    -MaxCommittedMiB <approved-limit> `
    -ResultPath artifacts/benchmarks/r02/build/budget-gate.json
```

门禁退出码：0 为预算内，1 为超过预算，2 为无效或不足的证据/审批输入。结果拒绝覆盖已有文件；缺少审批、失败或超时构建、错误候选都不能 PASS。合同测试通过不代表实际构建符合预算。

### R02 固定窗口混合读写探针

2026-11-25 用户同意降低本阶段硬件要求，新增 `1C2G`（1 vCPU、2 GiB）档位。环境脚本、混合探针、指标门禁和 `r02-workload.json` 均接受该档位；原三档保留供后续宿主验证。100,000 行、90:10、30 秒预热/120 秒测量、池 32 和并发阶梯保持一致，历史探索阶段预算待批准；当前本地批准结果见下。

```powershell
pwsh -NoProfile -File eng/perf/New-SqlCapacityEnvironment.ps1 `
    -Profile 1C2G -Image $localRuntimeImage -PublishDirectory $candidateDirectory `
    -ResultsDirectory artifacts/benchmarks/r02/local/app
# 按环境收据中的 containerId 执行，工作负载文件须在只读 /app 挂载中。
docker exec --env RUN_POSTGRESQL_INTEGRATION_TESTS=true `
    --env "ConnectionStrings__PostgreSqlConnection=$nonProductionConnection" `
    $containerId dotnet /app/Bing.Data.Sql.Benchmarks.dll --mixed-capacity-probe `
    /app/r02-workload.json 1C2G 8 /results/c8.json
```

资源收据必须实际匹配 `cpu.max=100000 100000`（或等效 quota/period）、`memory.max=2147483648`、`memory.swap.max=0`。结束后调用方按本次完整 containerId 删除容器。若数据库使用同一 Docker Desktop 宿主上的独立容器，应记录其限制、镜像、运行时和共享宿主条件，结果归为本地受限资源探索；它不能完成原定独立固定数据库宿主的验收。

2026-11-25 较早的探索批次已完成该本地组合的五个混合单元，预热/测量均零失败、占用计数均 OBSERVED；候选 SHA256、原始收据和环境见 [历史 1C2G 验证记录](../../artifacts/ai-tasks/r02-1c/verification.json)。当时数据库为临时 PostgreSQL 16.14、1 CPU/512 MiB，应用为 .NET 8.0.33/Linux x64；表、容器和内部网络均已清理。该历史批次无批准预算，不包含纯池排队、数据库资源或方法 CPU；当前批准后的磁盘卷联合验收见本文开头，不是新增 Native AOT 结果。

`--mixed-capacity-probe` 使用正式 DI 查询工厂和写入执行器：主键读取一行，或按主键将 value 加一；每十个请求调度一次写入。窗口截止后停止派发，等待已经发出的操作完成，报告窗口内成功完成吞吐及包含收尾时间的完成吞吐。窗口尾部不足十个请求时报告实际读写比例，不为了补比例延长窗口。成功请求延迟使用固定 1ms 桶；超过 120 秒的样本单独计数，相应尾分位数返回 null。

将 `eng/perf/r02-workload.json` 复制到 Linux 兼容发布目录后，在已配置并核对限制的容器中运行，例如：

```powershell
dotnet /app/Bing.Data.Sql.Benchmarks.dll --mixed-capacity-probe `
    /app/r02-workload.json 2C4G 16 /results/mixed-c16.json
```

执行进程须设置 `RUN_POSTGRESQL_INTEGRATION_TESTS=true` 和 `ConnectionStrings__PostgreSqlConnection`，连接到独立的 `bing_sql_aot_test` 非生产库；连接串不写入报告。探针核对当前 cgroup CPU/内存限制，使用独立管理连接建立 100,000 行临时业务表、预热和测量，最后删除本次唯一表。每个并发档独立运行，固定池大小 32。此入口与既有固定请求数的只读探针分别保留，不能将混合收据交给旧的只读容量门禁。

混合报告中的进程 CPU、分配和 GC 包含负载调度与诊断开销；数据库会话/锁等待及数据库累计统计不等于数据库宿主 CPU、内存或磁盘占用。数据库累计统计有刷新延迟，且可能包含同库其他连接活动，差值不能用于逐次校验本负载的更新数量。目标三档容量尚未验证，历史探索阶段预算待批准；当前本地批准结果见下。

短时真实 PostgreSQL 调用链由 [SqlMixedCapacity.Integration.Tests](tests/SqlMixedCapacity.Integration.Tests/README.md) 单独验证：Windows 客户端、临时 1 CPU/512 MiB PostgreSQL、2,000 行、1 秒预热/3 秒测量、并发 8、池 2。复用正式测量内部实现，覆盖真实读写、活动/池计数和表清理，报告格式与正式容量收据区分。2026-11-25 已取得无失败的实际数据库与测量窗口 SQL 方法栈证据；这不是已批准的 100K/30/120 负载或三档容量结果。正式入口的 cgroup 拒绝行为仍保留。

`measurement.connectionPool` 使用 Npgsql EventCounters 采集忙碌/空闲连接数。预热完成后订阅，每秒采样，测量及已发出请求收尾结束后释放，范围标为 `observationWindow=measure-through-drain`。计数为 `process-wide-all-pools`，不是按本次 `application_name` 过滤；应在独立进程运行探针。两类计数分别报告 `sampleCount`、`sampledPeak` 和 `last`，不能把各自的峰值相加作为同时连接总量，也不能把一秒采样峰值当成瞬时绝对峰值。

两个计数均收到时为 OBSERVED，只收到一种为 PARTIAL，均未收到为 NOT_OBSERVED；缺测值保留 null，真实零样本保留 0。采集器仅保存固定大小汇总，通过 LogAlways/None 订阅计数器，不订阅 Informational/Verbose 命令事件；实际开销仍计入测量。无逐请求诊断集合，也不向生产 SqlQuery 添加接口、反射或锁。

占用计数不能证明纯池排队时长。`connectionAcquisition.status` 继续为 NOT_INSTRUMENTED；整个 `OpenAsync` 耗时、数据库锁等待和忙闲计数均不能替代纯排队。后续需先确定驱动级排队事件或外部诊断方案，再在目标环境采集；本次没有升级驱动，也不以占用观测完成替代完整池等待验收。

```powershell
dotnet run --project eng/perf/tests/SqlMixedCapacity.Tests/SqlMixedCapacity.Tests.csproj `
    -c Release --artifacts-path artifacts/validation/r02/mixed/tests
```

该测试入口包含 6 项负载测试及 8 项计数职责测试（含真实 EventSource 回调替身）。另可传入实际候选目录的 `Npgsql.dll`，验证当前驱动计数合同；该检查初始化驱动的诊断源，不连接数据库。测试中的驱动加载使用反射，仅属于离线合同验证，不进入 Benchmark 测量或生产执行路径。Benchmark smoke 在构建后自动传入自己的候选驱动。

```powershell
dotnet run --project eng/perf/tests/SqlMixedCapacity.Tests/SqlMixedCapacity.Tests.csproj `
    -c Release --artifacts-path artifacts/validation/r02/mixed/tests -- $candidateNpgsqlAssembly
```

### R02 混合负载指标门禁

`Invoke-SqlMixedCapacityGate.ps1` 专门校验 `SqlMixedCapacityReport-v1`，不接受固定读收据。它核对七个实际候选程序集的 SHA256、已批准探索负载、指定的规格/并发单元、报告的 cgroup 数值、预热及测量窗口、读写计数、固定直方图分位数，再重新计算吞吐和失败率。吞吐预算使用 **120 秒窗口内成功完成数 / 120**，不会用准入吞吐或包含收尾时间的吞吐代替；P99 使用所有成功请求（包括收尾）的延迟。

预算批准后，从仓库根目录调用，数值使用小数点。下面的变量应来自实际审批，工具没有默认预算；审批引用由调用方提供，不自动认证审批有效性。

```powershell
pwsh -NoProfile -File eng/perf/Invoke-SqlMixedCapacityGate.ps1 `
    -ReceiptPath artifacts/benchmarks/r02/mixed/c16.json `
    -ExpectedBuildDirectory $candidateBuild -ExpectedWorkloadPath eng/perf/r02-workload.json `
    -ExpectedProfile 2C4G -ExpectedConcurrency 16 -BudgetApprovalId $approvalReference `
    -MinRequestsPerSecond $approvedMinRps -MaxP99Milliseconds $approvedMaxP99 `
    -MaxFailureRate $approvedMaxFailureRate |
    Out-File -LiteralPath artifacts/benchmarks/r02/mixed/c16-gate.json -Encoding utf8 -NoClobber
```

标准输出是一份 JSON；退出码 0/1/2 分别对应 PASS/FAIL/INVALID。缺少预算或审批引用、身份/负载/单元错配、伪造派生吞吐及计数不一致返回 INVALID；有效报告的预算超标、预热失败、没有成功请求或 P99 落入直方图溢出区间返回 FAIL。等于预算时通过。已批准探索配置中的预算字段继续为 null，正式预算通过显式参数单独提供。

结果固定标注 `scope=mixed-workload-metrics`、`productionCapacityVerified=false`。PASS 只代表所选单元的收据指标达标；报告中的资源限制并非第三方环境证明，门禁也不验签。部署方仍须核对环境收据、宿主和数据库独立性、完整矩阵、连接池排队、数据库资源与 CPU 原始证据。本轮职责夹具不能代替真实数据库测量。

```powershell
pwsh -NoProfile -File eng/perf/tests/SqlMixedCapacityGate.Tests.ps1 `
    -ResultsDirectory artifacts/validation/r02-mixed-gate/tests
```

### 可选扩展：原三档固定读矩阵

下述 `New-SqlCapacityMatrix.ps1` 保留原三档固定读规划合同（30 个单元），与本阶段 `1C2G` 混合负载分开；原三档是可选后续扩展，不作为此次硬件范围的阻塞条件。三档规格解释为**应用宿主**的 vCPU / GiB 内存；数据库使用独立且固定的非生产实例。若应用与数据库同机，需要另列组合，不能沿用应用独占预算。规格是验证输入，不是容量结论，也不自动批准任何吞吐、P99 或错误率阈值。

| 规格 | 应用 vCPU | 内存 GiB | 固定并发阶梯 |
| --- | ---: | ---: | --- |
| 2C4G | 2 | 4 | 1、8、16、32、64 |
| 4C8G | 4 | 8 | 1、8、16、32、64 |
| 8C16G | 8 | 16 | 1、8、16、32、64 |

先在每档独立 VM 或具有真实 CPU/内存限制的容器运行同一候选。保留分配规格、实际限制、CPU 型号、运行时/OS、数据库主机规格及连接池设置。`DOTNET_PROCESSOR_COUNT`、进程并发数或 GC 堆上限不能替代整个应用宿主的资源限制。下述矩阵脚本只规划运行单元；容器限制由上面的环境脚本配置和核对。

以下命令生成 30 个固定读验证单元（3 档 × 2 条路径 × 5 个并发档）。10,000 行、8,192 请求、连接池 32 是可调整的探索示例，**不是代表性业务负载或已批准预算**：

```powershell
pwsh -NoProfile -File eng/perf/New-SqlCapacityMatrix.ps1 `
    -Rows 10000 -Requests 8192 -MaxPoolSize 32 `
    -OutputPath artifacts/benchmarks/r02/plan/matrix.json
```

文件包含每个单元的探针参数数组和 `expectedWorkload`，可供既有容量门禁使用。运行方将参数中的 `{output}` 替换为本次统一目录下的独立收据路径，并实际在连接字符串中设置相同池大小。生成器只输出 `PLANNED`，不连接数据库，不覆盖已有文件，不设置验收阈值。先做短诊断确定有效测量窗口，再固定正式运行的请求数；不要为追求通过反复修改阈值。相同请求数、数据和池配置应跨三档保持一致。

容量正式结果仍需业务读写比、持续时间、池等待、服务端资源、CPU 原始 trace 和经批准的预算。当前固定读探针没有写入负载，不能将其结果外推为混合读写容量；核数与内存翻倍也不直接推导 QPS 翻倍。

```powershell
pwsh -NoProfile -File eng/perf/tests/SqlCapacityMatrix.Tests.ps1 `
    -ResultsDirectory artifacts/validation/r02/matrix
```

从仓库根目录构建 SQL 基准。使用独立的 `bing_sql_aot_test` 库，并在当前终端设置 `RUN_POSTGRESQL_INTEGRATION_TESTS=true` 和 `ConnectionStrings__PostgreSqlConnection`。库可以由现有 [PostgreSQL Native fixture](../sql-aot/README.md) 提供。账户需要能在专用库的 `public` 架构创建测试表。每次运行在事务中创建并填充独立的主键表，完成后删除该表。

```powershell
dotnet build framework/tests/Bing.Data.Sql.Benchmarks/Bing.Data.Sql.Benchmarks.csproj -c Release
$benchmark = 'output/release/net8.0/Bing.Data.Sql.Benchmarks.dll'
$receipts = 'artifacts/benchmarks/sql-capacity/run-' + [Guid]::NewGuid().ToString('N')
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
pwsh -NoProfile -File eng/perf/tests/SqlCapacityProbe.Tests.ps1 -BenchmarkAssembly $benchmark -ResultsDirectory artifacts/benchmarks/sql-capacity-tests -RunPostgreSql
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

默认报告父目录为 `artifacts/benchmarks/sql-build`。可通过 `-ResultsDirectory <目录>` 指定报告父目录，`-RestoreConfigFile <NuGet.Config>` 指定离线源配置，`-TimeoutSeconds <秒>` 指定整棵进程树的观察窗口（默认 900 秒）。工具不下载 SDK，也不改动源仓库的构建产物；SDK 由现有 `global.json` 决定。默认源配置可能访问网络；离线配置必须能从宿主已有 NuGet 缓存恢复全部依赖。

每次把根构建配置、EditorConfig、asset、framework/src、SQL 基准工程及其 Models 工程复制到 `artifacts/validation/sql-build/run-<GUID>/source`，日志、逐文件 SHA256 manifest 和结果保存在报告父目录的同名批次中。Models 引用的 SQL 库和生成器均由 framework/src 提供。复制时排除 bin、obj、.codex-build、工程同名生成 XML 文档等历史产物及目录链接，保留 UTF-8 字节。该副本从无中间产物的状态构建 `Bing.Data.Sql.Benchmarks.csproj` 的完整项目引用图，Release、单 MSBuild 节点、关闭编译服务器及节点复用；包含 restore，复用宿主包缓存。它不构建整个 Bing.All.slnx，也不清空包缓存。归档并核对最终证据后，调用方应清理 result.json 中 source.root 所属的本次 validation 批次。

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
pwsh -NoProfile -File eng/perf/tests/SqlCleanBuildPeak.Tests.ps1 -ResultsDirectory artifacts/validation/sql-build-tests/snapshot
pwsh -NoProfile -File eng/perf/tests/BuildProcessTree.Tests.ps1 -ResultsDirectory artifacts/validation/sql-build-tests/process
```

前者验证完整工程引用、构建副本、输入身份及报告与临时源码目录分离；后者使用真实子孙进程验证 Job 统计、提前退出、参数、失败日志和超时清理。实际构建收据与功能测试分别保存；本工具未改变 SQL 运行时、基准工作负载或现有 before/after 基线。

## 托管方法 CPU 归因

### 已批准的本地回归与资源口径

`r02-local-budget.json` 保存用户于 2026-11-25 批准的 `R02-1C-20261125-01` 预算和目标环境：本机 Docker，应用 1 CPU/2 GiB、数据库独立容器 1 CPU/512 MiB、临时磁盘卷、默认持久化；二者共享物理宿主。100K 行、90:10、30 秒预热/120 秒测量、池 32，仍由 `r02-workload.json` 定义。原负载文件的历史 `PENDING_APPROVAL` 字段保持兼容合同，正式阈值由独立预算配置显式传给门禁；不据此判断本次审批缺失。预算只验收该本地拓扑，不代表生产 SLO。

Windows 构建范围是 SQL Benchmark 的完整项目引用图，包含 restore 并复用包缓存。干净构建输出直接用于五个无 EventPipe 的容量单元及单独带 EventPipe 的诊断单元，以七个程序集 SHA256 核对候选。容量和带采样诊断不能合并判定。

Linux 子进程退出后可能无法读取 `TotalProcessorTime`。采样工具将此可选元数据保留为 `processCpuSeconds=null` / `processCpuSecondsStatus=NOT_AVAILABLE`，继续完成 trace 和方法分析；不将不可用值写成0。本次原采集失败收据保持原样，另存离线重放记录。原始样本以 `samples.jsonl.gz` 无损留档，压缩前后解压SHA256一致，可重建的ETLX不留档。

混合探针的 `measurement.measure.startedAtUnixMilliseconds` / `stoppedAtUnixMilliseconds` 给出包含 drain 的 UTC 边界，每个窗口只读取两次 UTC。外部 cgroup 采样可用以下工具按该边界汇总：

```powershell
pwsh -NoProfile -File eng/perf/Get-SqlContainerResourceSummary.ps1 `
    -SamplesPath artifacts/benchmarks/r02-final/local/app/c1-db.jsonl `
    -WindowStartUnixMilliseconds <measure开始UTC毫秒> `
    -WindowStopUnixMilliseconds <measure结束UTC毫秒> `
    -ResultPath artifacts/benchmarks/r02-final/resources/c1-db.json
pwsh -NoProfile -File eng/perf/tests/SqlContainerResources.Tests.ps1 `
    -ResultsDirectory artifacts/validation/sql-container-resources/tests
```

每行 JSONL 包含 `timestampUnixMilliseconds`、累计 `cpuUsageMicroseconds`、`memoryCurrentBytes`、累计 `memoryPeakBytes`、`oomKill` 及原始 `ioStat` 字符串。汇总使用窗口两侧采样点，各距边界不超过 2500ms；CPU 按实际包围区间计算，不假定精确等于 120 秒。内存分别记录采样 `memory.current` 峰值和容器生命周期 `memory.peak`。IO 保留每设备差值，不把虚拟块设备叠加成物理盘吞吐。时间戳逆序、计数回退或边界缺失返回 `NOT_VERIFIED` / 退出 1，不补造零值。

这批 CPU、内存和 IO 只观测；限制一致、无 OOM、无意外重启是完整性检查。cgroup 包含容器内采样器开销，结果文件写入也可能计入 IO；不是物理宿主资源或纯数据库进程统计。每个单元的采样起止、精度和原始 JSONL 必须与收据一起保存。最终保留证据在 `artifacts/ai-tasks/r02-final`，可重建构建树与 ETLX 清理；原始 trace 保留以便重放。

R02 可使用调用方指定的有限时长托管工作负载，复用原采集/清理/分析流程：

```powershell
dotnet $collector capture-workload $benchmark artifacts/benchmarks/r02/cpu 120 -- `
    --capacity-probe runtime 10000 8192 16 '{output}'
```

`--` 后逐项传递原程序参数；`{output}` 替换为本次独立运行目录的 `probe.json`。原 `capture` 命令仍运行固定 Builder 探针。自定义入口记录调用参数与程序集身份，包含启动/JIT、业务自行定义的预热和测量；它不会自动截取稳态窗口。连接串通过环境变量传入，勿放入会落盘的命令行参数。采样会影响被测进程，因此不带采样的容量测量与带采样的 CPU 诊断要分别保存。窗口上限仍为 300 秒，超时终止该入口启动的进程树；不要将长驻服务直接作为有限负载使用。

新入口提供采集能力，不证明业务负载具有代表性；托管 SQL 栈不足时仍输出 NOT_VERIFIED。目标应用应使用同一候选 SQL 程序集及完整部署依赖。Native AOT 进程的原生/内核方法归因仍不由这个托管 EventPipe 工具覆盖。

混合探针通过 `Bing-Sql-MixedCapacity` 的两个 EventSource 事件标记正式测量及 drain，不在每个请求埋点。CPU 采集器订阅该源，按同一 trace 时钟的半开区间筛选样本，summary 中的 observationWindow 为 `measure-through-drain`，并记录 StartMilliseconds / StopMilliseconds。预热、输出及表清理不进入该范围；它仍包含调度和管理采样开销，不等于纯业务方法 CPU，也不是严格 120 秒窗口。完整 trace 保留 JIT/栈解析信息，methods.csv 和 samples.jsonl 仅输出所选范围。

`capture-workload ... -- --mixed-capacity-probe ...` 强制要求完整单次窗口；缺失、重复或逆序标记保持 NOT_VERIFIED。重放混合 trace 使用 `analyze-window <nettrace> <PID> <新目录>` 强制相同要求。普通 `analyze` 自动识别已有标记；无标记的旧探针保留整个进程范围。窗口有效仍须有 SQL 托管栈且无丢事件，才通过方法采样检查，不代表生产容量验收。直接窗口测试随 `--self-test` 执行；真实 EventPipe 集成用 `eng/perf/tests/SqlCpuWindow.Integration.Tests.ps1 -Collector <collector.dll> -Fixture <SqlCpuTraceFixture.dll> -ResultsDirectory artifacts/validation/sql-cpu-window/tests`，夹具复用正式负载调度器但不访问数据库。

Benchmark smoke 现在会构建窗口夹具并运行真实 EventPipe 窗口集成，同时检查成功测试没有把预期的分析退出码 1 泄漏给同会话调用方。CPU 工具与夹具分别使用 `artifacts/validation/sql-smoke/cpu-build`、`artifacts/validation/sql-smoke/cpu-fixture`，日志、trace 和判定写入本次 smoke 结果目录。`-NoRestore` 调用方须事先在上述相同路径恢复两个工程；普通 smoke 会自动恢复。该检查不访问数据库，也不证明容量达标。

`sql-aot.yml` 的 `sql-benchmark-smoke` 工件在测试失败时也执行上传，保留 14 天。上传范围包括 JSON 收据/候选清单、日志、原始 `.nettrace`、样本 `.jsonl`、方法表 `.csv` 和子进程 `.stderr`，便于下载后核对窗口并重放分析。可从原始 trace 重建的 `.etlx` 及构建目录不上传。`node --test eng/sql-aot/verify-ci.test.mjs` 直接检查这个上传步骤的路径、失败保留和保留期；本地合同测试通过不代表外部 CI 已执行或工件已上传。

`SqlCpuTrace` 默认诊断本地固定 SQL Builder 探针，也支持上述调用方工作负载，不属于 SQL 公共 API。复用已有 Release 基准目录中的 DiagnosticsClient/TraceEvent 程序集，不安装 dotnet-trace，不恢复新的诊断依赖。先构建现有基准（也可使用上述干净构建工具的最终输出），然后从仓库根目录运行：

```powershell
$benchmarkDirectory = (Resolve-Path 'output/release/net8.0').Path
# 如使用独立构建收据，将上面替换为该收据 binary.file 的父目录。
$cpuBuild = Join-Path $PWD 'artifacts/validation/sql-cpu/build'
dotnet build eng/perf/SqlCpuTrace/SqlCpuTrace.csproj -c Release --disable-build-servers -p:UseSharedCompilation=false "-p:DiagnosticAssemblyDirectory=$benchmarkDirectory" --artifacts-path $cpuBuild
$collector = Join-Path $cpuBuild 'bin/SqlCpuTrace/release/SqlCpuTrace.dll'
dotnet $collector --self-test
dotnet $collector capture "$benchmarkDirectory/Bing.Data.Sql.Benchmarks.dll" artifacts/benchmarks/sql-cpu 120
```

离线时 build 可增加 `-p:RestoreConfigFile=<已有离线 NuGet.Config>` 与 `-p:NuGetAudit=false`。工具引用的程序集来自实际 benchmark 输出；本轮实际版本是 DiagnosticsClient 0.2.251802 / TraceEvent 3.1.8，不暗示其他版本已验证。

每次生成新的 run-GUID，保存 capture.json、binary-manifest.json、cpu.nettrace、probe.json、标准输出/错误，以及 report 下的 cpu.etlx、conversion.log、samples.jsonl、methods.csv、components.json、summary.json。进程在托管入口前暂停，接入目标诊断端口后恢复；固定执行 `--builder-state-probe`，无需开启系统级 WPR 采样策略。窗口 1～300 秒；超时先结束自有进程再释放会话，失败返回非零。CPU 分析不重新运行探针，原始 trace 可重放：

```powershell
dotnet $collector analyze <cpu.nettrace> <capture.json中的PID> <新的报告目录>
```

已有报告目录拒绝覆盖。`samples.jsonl` 每行保存 PID、线程、相对时间、实际枚举 kind（External=1、Managed=2）和叶到根的方法栈。方法表的 Self/Inclusive 百分比分母是该 PID 的 Managed 样本数，递归方法包含权重在同一样本内只计一次；包含百分比不能跨方法求和。缺失/未解析栈保留在报告，不通过删除它们改善覆盖率；空证据、丢事件、仅基准方法或仅未知地址不能通过 SQL 方法验证。

EventPipe 提供的是托管执行状态采样，External 不等同于等待。此入口包含启动、JIT、探针构造、对照策略和输出，不是纯稳态 Query；样本数不能换算成精确 CPU 毫秒、整机 CPU 百分比或生产容量，原生/内核未覆盖。口径依据见 [Microsoft EventPipe](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventpipe) 与 [SampleProfiler 枚举](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/EventPipe/SampleProfilerTraceEventParser.cs)。实际结果和边界见 [Round136](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round136.md)。

真实集成验证使用专用 fixture，不连接数据库；CapturedRun 必须是上述实际采样目录：

```powershell
$fixtureBuild = Join-Path $PWD 'artifacts/validation/sql-cpu/fixture'
dotnet build eng/perf/tests/SqlCpuTraceFixture/SqlCpuTraceFixture.csproj -c Release --artifacts-path $fixtureBuild
$fixture = Join-Path $fixtureBuild 'bin/SqlCpuTraceFixture/release/SqlCpuTraceFixture.dll'
pwsh -NoProfile -File eng/perf/tests/SqlCpuTrace.Integration.Tests.ps1 -Collector $collector -Fixture $fixture -CapturedRun <run-GUID目录> -ResultsDirectory artifacts/benchmarks/sql-cpu-tests
```

当前 25 项直接职责测试随 `--self-test` 运行，覆盖采集参数、可选进程 CPU 元数据、方法/组件计数和窗口；三项既有集成测试覆盖非零退出/中文日志/trace、真实超时清理、原始数据重放/错误 PID/拒绝覆盖。测试调用进程自身上限为 30 秒。工具/fixture 的构建与测试结果独立于 SQL 单元和 SQLite/Native 验证，不互相替代。


### 离线组件与最近调用方归因

`analyze` / `analyze-window` 在原方法表之外生成 `components.json`（`SqlCpuComponents-v1`）。报告使用已过滤的同一 PID、窗口及 Managed 样本；External/未知执行态不进入分母，缺失/未解析栈仍保留。`summary.json` 只增加报告路径和组件验证标志，原字段和 methods.csv 的统计语义不变。PASS 继续要求有效窗口、存在非 Benchmark 的 Bing.Data.Sql 业务栈且无丢事件；只有 Bing.Dapper 栈即使归类为 sql-execution，也不会放宽原门槛。

两种视图各自对每个托管样本计一次：LeafSamples 按实际叶帧分类；NearestOwnerSamples 从叶到根跳过 runtime/unresolved/other，选择最近可识别的调用方，没有则保留叶帧。两列分别合计为 ManagedSamples，不能相加。NearestOwners 按次数降序、方法名 ordinal 排序，用于寻找运行库成本对应的调用上下文，不等于方法独占时间或包含时间。无样本时固定 11 个类别均输出 0，百分比为 null。

固定类别为 driver、dapper-materialization、dapper、sql-planning、sql-mapping、sql-execution、dependency-injection、harness、runtime、unresolved、other。匹配所属类型前缀，忽略参数类型中的名称；harness 优先于框架前缀，GetPlan 采用完整方法名边界。dapper-materialization 仅识别 GetSimpleValueDeserializer 入口及其闭包，包含工厂准备成本，未覆盖所有 Reader/DTO 物化。内联、异步栈和截断可能隐藏调用方；这不是自动发现所有业务组件的规则系统。

现有采集未打开分配事件，报告明确 `allocationAttributionStatus=NOT_COLLECTED`，不能从 CPU 样本换算 B/op，也未分离读写、网络等待或原生/内核 CPU。此改动只在离线工具中运行，不向生产查询加入逐请求采样。真实窗口集成现有 5 个场景：窗口过滤/非 SQL 拒绝、组件计数与范围、确定性重放、错误 PID、拒绝覆盖；同会话退出码检查继续运行，已由 Benchmark smoke 调用。

2026-11-25 离线重放证据见 `artifacts/ai-tasks/r02-client-cpu/verification.json`。输入是 r02-final 的历史 Linux 混合读写 C8 trace，SHA256 为 55610FD7FBD1DEB5E65D9C0E62E09C7A3F0C3C98CEB519BFF548FB0433D691AE，PID 5645。33613 个托管样本的原摘要字段和方法 CSV 全部保持一致；新最近调用方分类中 sql-planning 36.4710%、sql-execution 24.6542%、driver 20.4117%。该证据来自历史候选，不证明当前 C1 根因或吞吐改善；原采集 FAIL 收据保持原样，离线报告 PASS 独立记录。原始 trace 继续保留在 r02-final，不复制或覆盖；本次只归档新报告、测试和工具身份，清理重建的 ETLX/展开样本及构建树。
