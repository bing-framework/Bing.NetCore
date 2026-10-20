<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-CACHE-PERF-STREAM-20261004-002
AI_EXECUTION_FINISHED_AT: 2026-10-06T10:37:23.6172561Z

AI_EXECUTION_STARTED_AT: 2026-10-06T04:17:49.225Z

# 实施执行报告

## 执行结论

公共流式契约、FreeRedis/CSRedis/EasyCaching 的批量与扫描路径、双节点分片路由和基准拓扑程序已实现。单节点及双分片的适用测试通过，解决方案构建通过。性能正式对照表明批量读、扫描及分配预算未达标；100,000 项与三主三从 Cluster 矩阵尚无运行证据，任务属于部分完成。

## 实施与验证

- `Bing.Caching` 新增 `CacheEntry<T>`、批量读取及前缀扫描异步流契约，保留 List API 的顺序、重复键、缺失与 null/default 语义；netstandard2.0 添加异步枚举兼容依赖。
- FreeRedis/CSRedis 按配置批次惰性消费输入，按槽位或分片节点分组 MGET、按原索引回填，扫描逐批输出；Cluster 按 `MaxConcurrency` 有界派发。取消后停止下一批派发，已发送命令不承诺撤销。
- EasyCaching Redis/Hybrid 批量读取、Hybrid 本地回填及流式输出已接入；重复物理键在批量查询前去重，按输入位置重建结果。
- 两个 Redis Provider 的流式契约测试覆盖重复键、缺失、null、消费中取消；CSRedis 双独立节点验证默认和自定义 `NodeRule` 分片读取顺序及实际双节点数据。EasyCaching Redis 集成测试覆盖 Hybrid、租约和总线回归。
- `Bing.Caching.Tests` net6.0/net8.0 各 105/105 通过；`Bing.EasyCaching.Tests` net6.0/net8.0 各 25/25 通过。
- FreeRedis 集成测试 net6.0/net8.0 各 79 通过、5 跳过；CSRedis net6.0/net8.0 各 77 通过、4 跳过。跳过项主要为 Cluster/专用故障门控。
- 四个受影响生产项目 netstandard2.0 构建、`Bing.All.sln --no-restore` 构建、`git diff --check` 通过；解决方案有既存 NuGet 可达性/TFM/分析器警告，0 error。
- 基准程序 baseline/current 均构建通过；单节点 FreeRedis/CSRedis 的 1,000/10,000 项各 2 轮预热、10 轮样本，以及当前版双分片和 EasyCaching Redis/Hybrid 单节点的 1,000 项重复样本已运行。详见 `performance.md`。

## 未完成与 TODO

1. 性能预算未达标：单节点两 Provider 的批量读、扫描 P95 与分配量均超过计划上限。后续应先做热点分配归因，再保持安全键格式复测。
   CSRedis 扫描已消除一次重复物理键生成，1,000 项诊断样本平均分配从 7.07 MB 降到 5.86 MB，仍约比旧版高 373%；第二个字符串 MGET 实验无收益并已撤回。按 No Progress Guard 停止重复同类试验，FIX-007 保持 OPEN_ACTIONABLE。
2. 100,000 项完整重复矩阵、三主三从 Cluster 集成及基准尚未运行；当前环境只有单节点和两个独立分片节点。
3. 三方严格对照缺少可重建的上一阶段中间版代码；上一阶段 v2 JSON 与本阶段探针方法不同，仅作历史参考。
4. 网络往返数和 Provider 内部在途并发没有公开可观测接口，报告显式记为 `-1`，后续需客户端侧探针才能补齐。EasyCaching 分片/Cluster 基准仍需专用拓扑配置。

## Round 4 增量证据（2026-10-06）

- FreeRedis 特殊逻辑键流式测试补充圆括号，并对每个结果断言逻辑键、值和 `Hit`；同时保留 Unicode、花括号、星号、问号、反斜线及相似命名空间哨兵隔离断言。
- `Bing.Caching.FreeRedis.Tests.Integration` 的 `CacheSpecialKeyTest` 精准 net8.0 集成测试：3/3 通过，0 失败，0 跳过。
- `production-symbol-tests.md` 已补充 FreeRedis 特殊键流式路径及 CSRedis `ScanPhysicalPrefix`/批量校验路径的生产符号到测试方法映射。
- 本轮只补测试证据和追踪映射，没有新增性能候选；FIX-007 的批量/扫描 P95 与分配预算仍保持未达标，Cluster 外部环境、100,000 项矩阵和网络/内部并发观测仍为 TODO 或外部阻塞。

## Change Impact Analysis

- ChangedFiles: Bing.Caching 公共接口/类型、FreeRedis、CSRedis、EasyCaching、对应测试、benchmark 与任务文档。
- ChangedProjects: Bing.Caching、Bing.Caching.FreeRedis、Bing.Caching.CSRedis、Bing.EasyCaching、三个缓存测试项目、两个 Redis 集成测试项目。
- ChangedPublicContracts: `CacheEntry<T>`、三个 `ICache` 异步流成员。
- ChangedRuntimePaths: 批量读取、前缀扫描、Hybrid 批量回填、取消路径与 Cluster/客户端分片路由。
- ChangedTFMs: 生产目标保留 netstandard2.0；测试继续 net6.0/net8.0。
- ChangedBuildPackaging: Bing.Caching 增加 `Microsoft.Bcl.AsyncInterfaces`。
- ChangedBenchmarkHarness: baseline/current、预热、采样、单节点/分片/Cluster 连接、节点监控与不可观测指标状态。
- AffectedDependents: 所有 `ICache` 实现和调用方；RiskLevel: HIGH。

## 状态

- Implementation: FUNCTIONAL_PASS_WITH_PERFORMANCE_TODO。
- Tests: PASS_WITH_CLUSTER_EXTERNAL_GAP。
- Performance: BUDGET_FAILED_AT_1K_AND_10K；Cluster/100K NOT_VERIFIED。
- Goal: STOPPED_AT_REVIEW_BOUNDARY；Round 3 已验证新的 CSRedis 扫描候选，但 FIX-007 仍为 OPEN_ACTIONABLE，需独立 Review 后再决定是否进入下一轮。
- Release: 未批准；未执行 git commit、git push 或发布。

## Review-Fix Round 1（2026-10-06）

本轮处理 `review.md` 中仍开放的 `FIX-007` 性能问题下，一个与既有序列化、物理键解析和容器分组试验不同的结构候选：把逻辑前缀下推到 Redis `SCAN MATCH`。`CacheKeyFormat.GetPrefixPattern` 生成带命名空间、哈希标签通配位和转义逻辑前缀的物理模式；FreeRedis、CSRedis、EasyCaching 的前缀读取、前缀删除和流式扫描使用该模式，客户端仍执行完整物理键和逻辑前缀校验。`*`、`?`、括号、反斜线和 Unicode 前缀由模式转义处理，命名空间与其他应用的哨兵键保持隔离。

### 本轮完成

- Redis 前缀扫描模式已在三类 Provider 接入，保留单节点、Cluster/分片路由、取消和拓扑完整性检查。
- 增加前缀模式精确单元测试，覆盖 Redis glob 字符、Unicode 和固定 32 位哈希标签通配位。
- FreeRedis、CSRedis 特殊键集成测试各 3/3 通过；EasyCaching Redis 特殊键测试 1/1 通过。
- 受影响生产项目 netstandard2.0 构建通过；CacheBench 当前版构建通过；Core 键格式测试 18/18 通过；`git diff --check` 无错误。
- 追加 1,000 项密集匹配方向性探针。P95 变化为 FreeRedis batch/scan +4.03%/+6.91%，CSRedis +3.40%/+0.90%；分配量变化均在 -0.05% 到 -0.55% 范围。该样本没有不匹配键，不能作为稀疏扫描收益或正式预算通过证据。

### 本轮未完成

- `FIX-007` 的正式批量读、扫描和分配预算仍未达标；本轮候选没有足够证据关闭该 finding，`review.md` 保持原状态。
- 稀疏命名空间（目标键与大量无关键混合）的前缀扫描基准尚未补齐；当前探针只能作为密集场景方向性记录。
- 100,000 项完整重复矩阵、EasyCaching 多拓扑正式性能矩阵和缺少外部环境的故障/拓扑证据仍按原 TODO 保留。

### Review-Fix 状态

- `FIX-007`: OPEN_ACTIONABLE，未达到性能预算；本轮产生了新的安全/扫描结构改进，但没有宣称性能门禁通过。
- Round result: PARTIAL。
- Release status: NOT_APPROVED；未执行 commit、push 或发布。

## Review-Fix Round 2（2026-10-06）

本轮处理 `FIX-007` 的可执行证据缺口，新增稀疏命名空间基准能力，而不是重复上一轮的生产键格式或容器分组试验。CacheBench 支持 `--scan-noise-count`：每轮先写入不匹配的 `noise:` 键，再计时目标 `item:` 键的批读和前缀扫描；噪声准备不计入 `write` 阶段，checkpoint 和 workload identity 会记录噪声数量及前缀。baseline/current 共用参数，避免用密集数据推断服务端 `MATCH` 收益。

### 本轮完成

- 基准参数、checkpoint、workload identity 和 README 已支持稀疏扫描数据分布；默认 `0` 保持既有基准行为。
- baseline/current CacheBench 均编译通过；`--scan-noise-count=9000` 的 FreeRedis、CSRedis baseline/current 四份原始报告均生成并记录 `noiseCount=9000`。
- EasyCaching Redis/Hybrid current 稀疏报告也生成，目标扫描均只返回 1,000 项。
- 稀疏首轮证据已写入 `performance.md`：FreeRedis scan P95 为 62.2321 → 23.9105 ms，CSRedis 为 21.4575 → 29.3627 ms；两者分配量仍分别上升 44.47% 和 53.44%。
- `git diff --check` 通过；仅存在既有 CRLF 转换提示。

### 本轮未完成

- `FIX-007` 仍为 OPEN_ACTIONABLE；旧版与当前版的键格式和命令路由差异使稀疏表不能直接宣称正式预算通过，CSRedis 仍有 P95/分配回退。
- 稀疏基准尚未扩展到 10,000/100,000 目标项、不同噪声比例和 Cluster/分片正式矩阵。
- 生产路径尚未获得新的分配根因；下一候选必须针对 CSRedis 的有界批处理/扫描响应成本提出独立结构假设，不能重复已停止的 JSON、SHA、返回类型和容器分组实验。
- 外部多节点故障、网络往返和内部并发观测 TODO 仍保留。

### Review-Fix 状态

- `FIX-007`: OPEN_ACTIONABLE；本轮完成可审计稀疏基准能力和新证据，未达到性能门禁。
- Round result: PARTIAL。
- Release status: NOT_APPROVED；未执行 commit、push 或发布。

## Review-Fix Round 3（2026-10-06）

本轮继续处理 `FIX-007` 中 CSRedis 的稀疏扫描回退，验证了两个相互独立的候选。第一项移除了 `ReadRawBatch` 已完成后的重复 `CacheBatchBudget` 遍历，保留直接单键读取的预算校验；第二项把每个扫描节点的 `ROLE` 校验从每个 `SCAN` 页面调整为节点扫描前后各一次，并复用扫描连接。同步前缀读取另走已校验物理键路径，避免物理键解码后再次哈希编码；异步前缀扫描同步复用连接，命名空间、哈希校验、取消和拓扑完整性语义不变。

### 本轮完成

- `CSRedisCacheManager.cs` 与 `CSRedisCacheManager.Stream.cs` 完成批量预算校验去重、扫描连接复用和同步物理键前缀读取路径。
- CSRedis 生产项目 netstandard2.0 构建通过，0 警告/错误；`BatchContractTest` 与 `CacheSpecialKeyTest` 合计 17 通过、1 个 100k 用例跳过。
- CSRedis 集成项目 net8.0 全量 89 通过、19 跳过、0 失败；跳过项为 Cluster/分片及专用故障环境门控。
- 同口径稀疏基准生成 `bench/sparse-current-csredis-budget-1k.json`、`bench/sparse-current-csredis-scanrole-1k.json` 和 `bench/sparse-current-csredis-physical-1k.json`。最后一项相对上一候选的扫描 P95 为 21.4987 ms，较旧版 21.4575 ms 约 +0.19%；扫描命令由 42 降为 24。
- 同一最终样本中，批读 P95 为 6.0531 ms（较旧版 5.7709 ms 约 +4.89%），扫描分配均值为 1,933,611 B（较旧版 1,295,552 B 约 +49.26%），批读分配均值为 1,613,373 B（较旧版 899,261 B 约 +79.41%）。扫描延迟预算方向改善，但分配预算仍失败。

### 本轮未完成

- `FIX-007` 仍为 OPEN_ACTIONABLE；1k/3 样本候选诊断不能关闭正式 1k/10k 分配预算，命中读和 FreeRedis/EasyCaching 的正式预算也未因本轮改变。
- 10,000/100,000 项最终候选复测、三主三从 Cluster 正式性能矩阵、EasyCaching 多拓扑正式矩阵仍未完成。
- 扫描前后 `ROLE` 校验减少了命令重复，但仍保留节点状态变化显式失败语义；该路径尚未在实际拓扑迁移期间以多节点环境复测。
- `review.md` 保持独立审查结论，未在执行阶段修改；未执行 commit、push 或发布。

### Review-Fix 状态

- `FIX-007`: OPEN_ACTIONABLE；扫描命令重复和同步前缀物理键回写已改善，分配预算仍不达标。
- Round result: PARTIAL。
- Release status: NOT_APPROVED；等待独立 Review。

## Review-Fix Round 5（2026-10-06）

本轮处理 EasyCaching 客户端分片批量写入/删除路径中的独立结构问题：此前每个分片批次按顺序发送，`MaxConcurrency` 没有用于跨分片派发。`ShardedRedisCacheManager.SetAllAsync` 与 `RemoveAllAsync` 现在使用有界在途任务列表，达到 `MaxConcurrency` 后等待一个已完成批次；已派发的批次在失败路径中继续被观察，取消只停止新批次派发。每个批次派发前替换容器，避免异步操作与后续填充共享可变集合。

### 本轮完成

- EasyCaching 客户端分片异步 `SetAllAsync` 和 `RemoveAllAsync` 支持按 `MaxConcurrency` 有界并发，保留分片身份校验、批大小、命名空间和部分成功语义。
- `ShardedBatchConcurrencyUnitTest` 在 net8.0、net6.0 各 6/6 通过，覆盖并发上限、取消停止新派发、已派发批次等待和首个异常传播。
- `Bing.EasyCaching.Tests` 全量 net8.0、net6.0 各 52 通过、52 跳过、0 失败；跳过项仍由 Redis/Cluster 环境门控。
- `Bing.EasyCaching` netstandard2.0 生产构建通过，0 警告、0 错误；生产符号映射已补充对应测试方法。
- 本轮没有运行正式前后性能基准；候选改变的是分片写入/删除的调度结构，未获得可与旧版严格对齐的两分片历史基线，因此不宣称性能预算通过。

### 本轮未完成

- `FIX-007` 仍为 OPEN_ACTIONABLE：FreeRedis/CSRedis 的正式批量读、扫描 P95 与分配预算仍失败；No Progress Guard 阻止重复相同 JSON、键解析、MGET 类型或容器微调实验。
- 100,000 项完整性能矩阵、三主三从 Cluster 正式多规模基准、EasyCaching 分片/Cluster 正式矩阵仍未形成可发布证据。
- 网络往返数、连接池压力和 Provider 内部在途并发仍缺少公开可观测钩子；当前只能用 Redis 命令计数和单元测试约束部分行为。
- 严格旧版/当前版/优化版三方基线，以及后续协调变更后的跨进程 ProcessProbe 证据仍需专用环境或可重建版本。

### Review-Fix 状态

- `FIX-007`: OPEN_ACTIONABLE；本轮新增 EasyCaching 分片调度能力并完成契约验证，但未关闭核心 Provider 性能 finding。
- Round result: PARTIAL。
- Release status: NOT_APPROVED；未执行 commit、push 或发布。

## Review-Fix Round 6（2026-10-06）

本轮处理“Provider 内部在途并发不可观测”的一个可执行子问题，没有重新运行已触发 No Progress Guard 的 FreeRedis/CSRedis 性能微优化。`CacheCommandProbe` 现在按 `CacheCommandKind` 记录最大重叠数，并通过 CacheBench 输出 `commandMaximumParallelism`；该指标只表示已接入探针的逻辑客户端调用重叠，不推断网络往返、连接池排队或 Redis 服务端并发。

### 本轮完成

- `CacheCommandProbe.GetMaximum` 和 `GetMaximumBreakdown` 增加按 `Unknown`、`SingleRead`、`BatchRead`、`Scan`、`SingleWrite`、`Delete` 分类的最大并发统计。
- CacheBench 的阶段汇总和 `rawSamples` 增加 `commandMaximumParallelism`，并覆盖批量读、扫描、写入、删除、命中和回源阶段。
- `CacheCommandProbeTest` 新增同类重叠、不同类别隔离、失败恢复和未使用类别为零的测试；net8.0/net6.0 精准测试各 6/6 通过。
- `Bing.Caching.Tests` 项目级回归在 net8.0/net6.0 各 164/164 通过；Bing.Caching netstandard2.0 构建 0 警告、0 错误。
- CacheBench Release Rebuild 通过 0 错误（保留既有 NU1801/NU1603 警告）；10 项 EasyCaching Release 冒烟生成 `bench/probe-concurrency-easyredis-10-release.json`，确认新字段写入并能记录 `SingleWrite` 最大重叠数。
- 复核 `BING-CACHE-TODO-20261006-034`：当前工作树的 FreeRedis、CSRedis、EasyCaching Redis ProcessProbe 五类跨进程协调场景均通过；其后 035–039 及本轮没有修改租约/回源协调路径，旧的“协调后证据过期” TODO 已关闭。

### 本轮未完成

- `networkRequests`、连接池等待和 Redis 客户端内部排队仍不可从现有 FreeRedis/CSRedis/EasyCaching API 可靠获得，继续标记为 `-1` 或明确不可观测；本轮没有用逻辑命令数替代它们。
- `FIX-007` 的 FreeRedis/CSRedis 批量读、扫描 P95 和分配预算仍开放；本轮诊断能力不改变性能结果，也不绕过 No Progress Guard。
- 100,000 项正式性能矩阵、三主三从 Cluster 正式矩阵和 EasyCaching 多拓扑正式矩阵仍按外部/延后 TODO 保留；单节点 100,000 项 List/Stream 一致性及协调 ProcessProbe 证据已有独立通过记录，但不能替代性能或集群证据。

### Review-Fix 状态

- 新增的逻辑并发观测子项已完成；`FIX-007` 仍为 OPEN_ACTIONABLE，但没有重复相同性能候选。
- Round result: PARTIAL。
- Release status: NOT_APPROVED；未执行 commit、push 或发布。

## Review-Fix Round 7（2026-10-06）

本轮补强流式取消的 Redis 命令级回归证据，范围限定在 FreeRedis 集成测试，不修改生产代码或性能热路径。新增测试在消费第一批后取消，再通过独立 `PING` 排空 `MONITOR`，断言当前命名空间只执行一条 `MGET`，从而验证取消不会继续派发下一批命令，而不是只观察输入枚举停止。

### Change Impact Analysis

- ChangedFiles：`framework/tests/Bing.Caching.FreeRedis.Tests.Integration/BatchContractTest.cs`。
- ChangedProjects：FreeRedis 集成测试项目及其测试依赖。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：无生产运行路径变化；新增测试只观察已有流式取消行为。
- ChangedProviders：FreeRedis 测试覆盖。
- ChangedTFMs：net6.0、net8.0 测试目标。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：本执行记录和生产符号映射。
- AffectedDependents：FreeRedis 流式批量契约测试。
- RiskLevel：LOW。

### 本轮完成

- 新增 `BatchContractTest.GetAllStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet`，使用共享 `RedisCommandMonitor` 观察真实命令。
- FreeRedis 集成测试项目 net6.0/net8.0 均编译通过，精准筛选各发现 1 个测试并按集成门控跳过；当前环境未设置 `RUN_INTEGRATION_TESTS` 和 Redis 连接，因此没有将跳过计为运行通过。
- `git diff --check` 通过；仅保留工作树既有 CRLF/LF 转换提示。
- CSRedis、EasyCaching 对应的命令级取消测试已存在并保持不变；本轮没有重复修改同一 Provider 热路径。

### 本轮未完成

- FreeRedis 命令级测试需要在明确隔离 Redis 上运行，当前属于 `BLOCKED_EXTERNAL` 证据缺口，不以门控跳过替代通过。
- `FIX-007` 的 FreeRedis/CSRedis 批量读、扫描 P95 和分配预算仍为 OPEN_ACTIONABLE；本轮没有新的性能根因或候选，不重复同类基准实验。
- 100,000 项正式性能矩阵、三主三从 Cluster 正式性能矩阵、EasyCaching 多拓扑正式矩阵和真实网络/客户端内部队列指标仍按原分类保留。

### Review-Fix 状态

- 新增的 FreeRedis 取消派发测试能力已完成，运行证据等待隔离环境。
- `FIX-007`：OPEN_ACTIONABLE；性能预算仍未达标。
- Round result：PARTIAL。
- Release status：NOT_APPROVED；未执行 commit、push 或发布。

## Review-Fix Round 8（2026-10-06）

本轮处理 EasyCaching 进程内协调状态的实例边界缺口。原实现使用全局静态 gate 字典和静态 `AsyncLocal`，只按物理键区分管理器；两个不同底层 Provider 即使连接到不同 Redis 端点，也会因为同名物理键互相等待，并可能把对方的回源识别为同键递归。本轮将协调状态放入按底层 `IEasyCachingProvider` 引用索引的 `ConditionalWeakTable`：同一 Provider 的多个适配器实例继续共享进程内合并，不同 Provider 的 gate、递归上下文彼此隔离。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs`、`framework/tests/Bing.EasyCaching.Tests/CacheManagerCoordinationIsolationTest.cs`、`production-symbol-tests.md`。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Memory/Redis/Hybrid 适配器共享的回源 gate 与递归检测。
- ChangedProviders：EasyCaching 内存、Redis、Hybrid。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：`MemoryCacheManager`、`RedisCacheManager`、`HybridCacheManager` 及显式 DI 注册入口。
- RiskLevel：MEDIUM。

### 本轮完成

- 使用 Provider 作用域协调状态替代跨所有 EasyCaching 实例的静态 gate/递归状态；`ConditionalWeakTable` 不持有底层 Provider 的强引用，避免把 Provider 生命周期延长。
- 保留同一 Provider 的跨管理器同键合并语义；不同 Provider 即使命名空间、ProviderName 和逻辑键都相同，也不会共享 gate 或递归上下文。
- 新增 `CacheManagerCoordinationIsolationTest`：不同内存 Provider 的同键回源可并行完成；跨 Provider 的嵌套同键回源不会误报递归。
- `Bing.EasyCaching.Tests`：net8.0 `54` 通过、`52` 跳过、`0` 失败；net6.0 `54` 通过、`52` 跳过、`0` 失败。跳过项由 Redis/Cluster/分片/总线环境门控产生。
- `Bing.EasyCaching` netstandard2.0 Release 构建通过，0 警告、0 错误；`git diff --check` 通过，仅保留工作树既有 CRLF/LF 转换提示。

### 本轮未完成

- `FIX-007` 的 FreeRedis/CSRedis 正式批量读、扫描 P95 与分配预算仍未达标；本轮没有重复 JSON、键格式、MGET 类型或容器微优化实验。
- FreeRedis 命令级取消、Cluster/分片/Hybrid 跨实例矩阵和真实网络/客户端内部排队指标仍等待外部环境或专用观测能力。

### Review-Fix 状态

- 新增 EasyCaching Provider 作用域协调能力已完成并有双 TFM 单元回归。
- `FIX-007` 仍为 `OPEN_ACTIONABLE`，但本轮修改的是独立的协调状态根因，未宣称性能预算通过。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 9（2026-10-06）

本轮处理 FreeRedis 批量读取路径中的独立重复扫描：`ValidateRawBatch` 在 Redis MGET 返回后已经检查响应数量和总负载，随后 `DeserializeValues<T>` 又对同一 `rawValues` 重新执行一次 `CacheBatchBudget.Check`。所有解码调用点均来自该已校验入口；移除解码层重复检查，保留入口拒绝语义，减少批量读和前缀扫描每批的一次完整负载遍历。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching.FreeRedis/Bing/Caching/FreeRedis/FreeRedisCacheManager.Stream.cs`、本执行记录和生产符号映射。
- ChangedProjects：`Bing.Caching.FreeRedis` 及其 FreeRedis 集成测试项目。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：FreeRedis `GetAll*`、`GetByPrefix*`、Cluster/分片批量解码路径；MGET 长度校验和资源上限仍在读取入口执行。
- ChangedProviders：FreeRedis。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：本执行记录和生产符号映射。
- AffectedDependents：FreeRedis 批量 List/Stream API 及前缀扫描 API。
- RiskLevel：MEDIUM。

### 本轮完成

- 从 `DeserializeValues<T>` 移除重复的 `CacheBatchBudget.Check`；通过注释固定其输入必须来自 `ValidateRawBatch` 已校验的读取入口。
- 保留 `ValidateRawBatch` 的 MGET 返回数量检查、批负载上限检查和 Cluster 单键路径的同一校验，未改变超限拒绝语义。
- `Bing.Caching.FreeRedis` netstandard2.0 Release 构建通过，0 警告、0 错误。
- `CacheResourceBoundaryTest` 与 `CacheBatchAdaptiveTest` 精准回归合计 8/8 通过。
- FreeRedis 集成测试 net8.0、net6.0 精准筛选均编译通过，各 17 个用例因未设置 `RUN_INTEGRATION_TESTS`/Redis 连接而跳过；未将跳过计为通过。
- `git diff --check` 通过，仅保留工作树既有 CRLF/LF 转换提示。

### 本轮未完成

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：本轮没有重新建立旧版/当前版正式 P95 和分配基线，因此不宣称批量读、扫描或分配预算通过。
- FreeRedis 入口超限拒绝尚缺隔离 Redis 的运行证据；现有通用预算测试覆盖核心预算函数，Provider 集成回归仍受外部环境门控。
- CSRedis/FreeRedis 10,000/100,000 项最终候选复测、三主三从 Cluster 正式矩阵、EasyCaching 多拓扑矩阵、网络往返与客户端内部队列指标继续按原 TODO 保留。

### Review-Fix 状态

- 新增 FreeRedis 解码热路径重复预算扫描修复已完成并通过构建/局部回归；这是独立结构改动，未重复此前已停止的 JSON、键格式、MGET 类型或容器实验。
- `FIX-007`：`OPEN_ACTIONABLE`；正式性能预算和外部拓扑证据仍未关闭。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 10（2026-10-06）

本轮处理共享 `CacheJsonBatch` 的容量计算双遍历。字符串 JSON 片段原先在容量计算阶段扫描空白和 `null` token，写入阶段又重复扫描；UTF-8 字节片段原先先执行 `GetCharCount`，随后再执行 `GetChars`。本轮改为安全容量上界，替换判断和实际解码只在写入阶段执行，保留所有输出与 null/default 规则。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching/Bing/Caching/CacheJsonBatch.cs`、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.Caching` 及 FreeRedis/CSRedis 受影响构建和测试项目。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：FreeRedis/CSRedis 默认标量批量读取、前缀扫描和 List/Stream JSON 组装。
- ChangedProviders：FreeRedis、CSRedis；EasyCaching 仅间接受共享库影响。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：本执行记录、性能报告和生产符号映射。
- AffectedDependents：`CacheJsonBatchTest`、两个 Redis Provider 的标量/序列化契约。
- RiskLevel：MEDIUM。

### 本轮完成

- 字符串片段容量改为 `max(value.Length, replacement.Length)` 上界，移除容量阶段的空白和 `null` token 重复扫描。
- UTF-8 字节片段容量改用 `Encoding.UTF8.GetMaxCharCount` 上界，移除容量阶段的 `GetCharCount` 全量解码；实际 `GetChars` 仍只执行一次。
- `CacheJsonBatchTest` net8.0、net6.0 各 9/9 通过，覆盖 null、空白、null token、Unicode、空输入、大值和长替换 token。
- `Bing.Caching.Tests` 项目级 net8.0、net6.0 各 164/164 通过。
- `Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` netstandard2.0 Release 构建均通过，0 警告、0 错误。
- FreeRedis/CSRedis 标量和序列化集成测试项目均编译通过；各 2 个用例因未启用 Redis 门控跳过，未将跳过计为通过。
- `git diff --check` 通过；未运行重型正式性能矩阵。

### 本轮未完成

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：当前环境没有可用隔离 Redis，尚未获得本候选同口径 1,000/10,000 项 P95 与分配 JSON；既有正式预算失败结论仍有效。
- 三主三从 Cluster 正式矩阵、客户端分片/Hybrid 多拓扑矩阵、网络往返和客户端内部队列指标继续按外部/延后 TODO 保留。

### Review-Fix 状态

- `CacheJsonBatch` 双遍历候选已完成并通过公共契约、Provider 构建和项目级回归；未改变 JSON 输出语义。
- `FIX-007`：`OPEN_ACTIONABLE`；性能门禁尚未验证或关闭。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 11（2026-10-06）

本轮处理共享 `CacheKeyFormat` 热路径中的独立重复 UTF-8 扫描。物理键格式化和哈希输入构造原先先调用 `Encoding.UTF8.GetByteCount` 计算精确容量，再调用 `GetBytes` 实际编码；同一逻辑键因此被遍历两次。本轮改用 `GetMaxByteCount` 作为安全容量上界，并让 `GetBytes` 负责唯一一次实际编码，保持物理键字节和哈希输入完全兼容。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching/Bing/Caching/CacheKeyFormat.cs`、`framework/tests/Bing.Caching.Tests/CacheConfigurationAndKeyFormatTest.cs`、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.Caching`、`Bing.Caching.Tests`；FreeRedis/CSRedis 及 EasyCaching 通过共享键组件间接受影响。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：物理键生成、哈希输入构造，以及依赖物理键的 Cluster/客户端分片路由。
- ChangedProviders：FreeRedis、CSRedis、EasyCaching 共享键格式路径。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：本执行记录、性能报告和生产符号映射。
- AffectedDependents：`CacheConfigurationAndKeyFormatTest`、Redis Provider 构建和集成测试项目。
- RiskLevel：MEDIUM。

### 本轮完成

- `HashContext.ComputeHash` 和 `FormatPhysicalKey` 移除 `GetByteCount` 精确容量扫描，使用 `GetMaxByteCount` 上界池化缓冲并保留实际 `GetBytes` 输出。
- 键格式兼容回归加入 ASCII、中文、BMP 字符和代理项字符；原始物理键字节与 V2 参考格式保持一致。
- `CacheConfigurationAndKeyFormatTest`：net8.0、net6.0 各 18/18 通过；`Bing.Caching.Tests` 项目级回归各 165/165 通过。
- `Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` netstandard2.0 Release 构建通过，0 警告、0 错误。

### 本轮未完成

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：本轮没有可用隔离 Redis，未生成该候选同口径 1,000/10,000 项正式 P95、分配和命令 JSON；既有预算失败结论继续有效。
- 三主三从 Cluster 正式性能矩阵、100,000 项正式采样、客户端内部网络/排队指标和 EasyCaching 多拓扑正式矩阵继续按外部/延后 TODO 保留。

### Review-Fix 状态

- `CacheKeyFormat` 重复 UTF-8 sizing pass 已完成并通过双 TFM 公共契约回归及三个受影响生产项目构建；未改变公开签名和键格式语义。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮只完成新的结构性候选，未宣称性能预算通过。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 12（2026-10-06）

本轮处理模式清理路径的独立 CPU/安全缺口。FreeRedis、CSRedis 和 EasyCaching 原先为每次模式清理编译正则表达式，并对扫描到的每个逻辑键执行正则匹配；缓存模式实际只支持 `*`、`?` 和反斜线转义。现在模式只解析一次为内部 glob token，扫描期间使用迭代匹配器；公共 `CreateRegex` 保留，避免破坏已有调用方。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching/Bing/Caching/CachePattern.cs`、FreeRedis/CSRedis/EasyCaching 模式清理路径、`framework/tests/Bing.Caching.Tests/CachePatternTest.cs`、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` 及缓存公共测试。
- ChangedPublicContracts：无；`CreateRegex` 和 `IsMatch` 签名保持不变。
- ChangedRuntimePaths：三个 Provider 的 `RemoveByPattern`/`RemoveByPatternAsync` 扫描过滤，以及公共 `CachePattern.IsMatch`。
- ChangedProviders：FreeRedis、CSRedis、EasyCaching。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；本轮不重跑 Redis 批量性能矩阵。
- ChangedDocsOnly：本执行记录、性能报告和生产符号映射。
- AffectedDependents：模式清理集成测试、EasyCaching mock 清理测试和缓存公共模式测试。
- RiskLevel：MEDIUM。

### 本轮完成

- 新增一次解析的内部 glob matcher，支持字面量、`*`、`?`、转义字符和尾部反斜线；保留仅通配模式拒绝语义。
- FreeRedis、CSRedis、EasyCaching 的同步/异步模式清理改用 matcher，消除每个扫描操作的正则编译和潜在回溯路径。
- `CachePatternTest` net8.0、net6.0 各 18/18 通过，增加 Unicode、转义和与公开正则表示的兼容性对照。
- `Bing.Caching.Tests` 最终全量 net8.0、net6.0 各 170/170 通过；模式精准测试各 18/18 通过。
- Bing.Caching、FreeRedis、CSRedis、EasyCaching netstandard2.0 Release 构建均通过，0 警告、0 错误；EasyCaching 模式测试 2/2 通过。

### 本轮未完成

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：模式匹配优化没有生成批量读/扫描正式 P95 与分配 JSON，不能宣称性能预算通过。
- FreeRedis/CSRedis 模式集成测试因未启用 Redis 门控各跳过 3 项；三主三从 Cluster、100,000 项正式性能矩阵、客户端内部网络/排队指标和 EasyCaching 多拓扑矩阵仍未验证。

### Review-Fix 状态

- 模式清理已从正则逐键匹配改为一次解析的 glob matcher，公共语义回归通过；`CreateRegex` 兼容 API 保留。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮是新的模式清理根因，未重复既有批量/键格式/JSON 实验。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 13（2026-10-06）

本轮继续处理模式清理的扫描成本，但范围与 Round 12 的匹配算法不同。前缀读取和删除已有服务端 `MATCH` 下推，模式删除此前仍使用当前命名空间通配模式后再客户端过滤。本轮从已解析 glob 中提取第一个通配符前的字面逻辑前缀，生成带命名空间、哈希标签通配位和 Redis glob 转义的扫描模式；客户端仍使用完整 matcher 复核，模式以通配符开头时不下推字面前缀。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching/Bing/Caching/CachePattern.cs`、`framework/src/Bing.Caching/Bing/Caching/CacheKeyFormat.cs`、FreeRedis/CSRedis/EasyCaching 模式清理路径、`framework/tests/Bing.Caching.Tests/CachePatternTest.cs`、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` 及缓存公共测试。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：三个 Provider 的同步/异步 `RemoveByPattern` 扫描模式生成；命名空间校验、逻辑匹配、取消和拓扑校验保持原路径。
- ChangedProviders：FreeRedis、CSRedis、EasyCaching Redis/Cluster 扫描路径；非 Redis EasyCaching Provider 继续使用原前缀枚举回退。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；当前无 Redis，不生成正式性能 JSON。
- ChangedDocsOnly：本执行记录、性能报告和生产符号映射。
- AffectedDependents：模式清理集成测试、`CachePatternTest` 和三个 Provider 构建。
- RiskLevel：MEDIUM。

### 本轮完成

- `CachePattern.Matcher` 记录第一个通配符前的字面逻辑前缀；无安全前缀时返回空字符串。
- `CacheKeyFormat.GetPatternScanPattern` 集中生成命名空间限定的 Redis `MATCH` 模式，字面前缀经过 URI 和 Redis glob 双重转义。
- FreeRedis、CSRedis、EasyCaching 同步/异步模式删除把该模式传给扫描层，仍保留完整逻辑 matcher，避免服务端过滤成为隔离边界。
- 增加模式前缀、Unicode、转义通配符和通配符起始模式测试；`CachePatternTest` net8.0/net6.0 各 22/22 通过。
- `Bing.Caching.Tests` 最终全量 net8.0/net6.0 各 174/174 通过；四个生产项目 netstandard2.0 Release 构建均为 0 警告、0 错误；EasyCaching 模式测试双 TFM 各 2/2 通过。

### 本轮未完成

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：没有 Redis 环境，尚未证明模式下推对实际扫描 P95、分配和命令数的收益，也没有替代批量读/扫描正式预算结论。
- FreeRedis/CSRedis 模式集成测试各跳过 3 项；Cluster 节点拓扑、客户端分片、100,000 项正式性能矩阵和客户端内部网络/排队指标继续未验证。

### Review-Fix 状态

- 模式删除已具备安全的字面前缀 `MATCH` 下推，完整逻辑匹配和命名空间隔离保持；本轮没有放宽安全过滤。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮新增模式清理扫描根因，未重复 Round 12 matcher 算法或既有批量微调。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 14（2026-10-06）

本轮继续处理模式清理的独立结构成本：当模式解析结果全部为字面量时，匹配语义等价于单个逻辑键删除，继续执行命名空间扫描没有必要。`CachePattern.Matcher` 新增内部 `IsLiteral` 标记；FreeRedis、CSRedis 和 EasyCaching 的同步/异步 `RemoveByPattern` 在该分支直接调用正常单键删除路径，保留物理键生成、租约撤销、Hybrid 失效通知、取消和客户端分片路由。含通配符的模式继续使用前缀下推和客户端 matcher。

### Change Impact Analysis

- ChangedFiles：`CachePattern.cs`、FreeRedis/CSRedis/EasyCaching 模式删除路径、`CachePatternTest.cs`、`CacheManagerTests.cs`、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` 及两个公共/Mock 测试项目。
- ChangedPublicContracts：无；`ICache`、`CreateRegex` 和模式语义保持不变。
- ChangedRuntimePaths：精确字面量模式删除；通配模式扫描、命名空间过滤、Cluster/分片和 Hybrid 协调路径未改变。
- ChangedProviders：FreeRedis、CSRedis、EasyCaching Redis/Hybrid；EasyCaching 客户端分片仍由各分片管理器执行单键路由。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBenchmarkHarness：无；当前无 Redis，不生成正式性能 JSON。
- RiskLevel：MEDIUM。

### 本轮完成

- 字面量、转义通配符和尾部反斜线模式识别为 `IsLiteral`；包含 `*`/`?` 的模式仍走原有扫描匹配。
- 三个 Provider 的同步/异步模式删除均改为单键删除；异步入口在派发前检查取消令牌。
- EasyCaching Mock 回归确认精确模式调用 `Remove`/`RemoveAsync`，且不调用键枚举；通配模式仍保持批量扫描删除。
- `CachePatternTest` net6.0/net8.0 各 `28/28` 通过；公共缓存项目级回归 net6.0/net8.0 各 `180/180` 通过。
- `Bing.EasyCaching.Tests` 全量 net6.0/net8.0 各 `54` 通过、`52` 跳过、0 失败；跳过项均为 Redis/Cluster/分片/总线外部门控。
- `Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` 的 `netstandard2.0` Release 构建均为 0 警告、0 错误；`git diff --check` 通过，仅保留既有 CRLF/LF 转换提示。

### 本轮未完成

- `FIX-007` 正式批量读/扫描 P95 与分配预算仍未达标；本轮是单模式删除结构优化，没有新的同口径性能矩阵，不能关闭该 finding。
- 当前没有 Redis 监听，FreeRedis/CSRedis 实际单键命令数、SCAN 绕过证据和 Cluster/分片模式删除集成测试仍等待外部环境。
- 三主三从 Cluster 正式性能矩阵、Hybrid 独立总线故障恢复、严格三方 baseline、网络/客户端队列观测和 100,000 项 EasyCaching 性能矩阵继续保留原状态。

### Review-Fix 状态

- 本轮精确字面量删除能力完成，并通过公共/Mock 回归和生产构建。
- `FIX-007`：`OPEN_ACTIONABLE`；性能门禁没有因本轮改变。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 15（2026-10-06）

本轮处理客户端分片模式清理中的独立路由成本。每个分片内部已经能够把精确字面量模式转换为单键删除，但 `ShardedRedisCacheManager` 外层仍把该模式广播到所有分片并启动扫描。现在分片入口先解析共享 glob matcher：字面量（包括转义的 `*`、`?` 和尾部反斜线）直接调用带分片路由和节点身份校验的 `Remove`/`RemoveAsync`；含通配符的模式仍保留逐分片扫描、取消和拓扑完整性校验。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/ShardedRedisCacheManager.cs`、`framework/tests/Bing.EasyCaching.Tests/ShardedIdentityUnitTest.cs`、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无；`IRedisCache` 签名和模式语义保持不变。
- ChangedRuntimePaths：客户端分片同步/异步 `RemoveByPattern` 精确字面量路由。
- ChangedProviders：EasyCaching 客户端分片 Redis；单节点、Cluster 和 Hybrid 管理器未改变。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；没有 Redis 环境，不生成正式性能 JSON。
- AffectedDependents：分片身份、删除命令和模式语义测试。
- RiskLevel：MEDIUM；主要风险是转义逻辑键必须与物理键哈希路由保持一致。

### 本轮完成

- 分片精确模式删除只调用命中分片的单键删除，不再向其他分片发起扫描或删除。
- 异步精确路径复用 `RemoveAsync` 的取消检查、租约清理和节点身份校验。
- 通配符模式继续逐分片执行原有扫描，不放宽命名空间过滤和拓扑失败传播。
- `ShardedIdentityUnitTest` 新增同步和异步精确模式路由测试，net8.0/net6.0 均 `9/9` 通过。
- `Bing.EasyCaching.Tests` 全量 net8.0/net6.0 均 `56 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching` netstandard2.0 Release 构建通过，0 警告、0 错误。

### 本轮未完成

- `FIX-007` 正式批量读/扫描 P95 与分配预算仍未达标；本轮没有新的同口径性能矩阵。
- 当前没有 Redis 监听，尚未获得真实分片命令数、SCAN 绕过和多节点故障期间的精确删除证据。
- Cluster 正式性能矩阵、Hybrid 总线故障恢复、严格三方 baseline、网络/客户端队列观测和 100,000 项性能矩阵继续等待外部环境或后续任务。

### Review-Fix 状态

- 客户端分片精确模式路由已完成，并通过双 TFM 单元回归和生产构建。
- `FIX-007`：`OPEN_ACTIONABLE`；性能门禁没有因本轮改变。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 16（2026-10-06）

本轮处理扫描结果资源没有上限的问题。新增可选的 `CacheResourceOptions.MaxScanKeys`，默认值为 `0`（保持原有无限制行为）；正值限制一次扫描中通过物理键和命名空间校验的候选逻辑键数，超过上限立即抛出明确异常。该预算接入 FreeRedis、CSRedis 和 EasyCaching 的同步扫描、异步扫描、前缀流式读取、模式清理和 `Clear` 路径，避免全量结果或异常大命名空间在客户端继续产生无界工作集。已经派发的批处理不承诺撤销，资源上限也不改变 Redis `SCAN` 对整个命名空间的遍历成本。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching/Bing/Caching/CacheResourceOptions.cs`、`CacheScanBudget.cs`、FreeRedis/CSRedis/EasyCaching 扫描和流式实现、`CacheResourceBoundaryTest.cs`、`CacheManagerTests.cs`、FreeRedis/CSRedis `CacheSpecialKeyTest.cs`、FreeRedis `BatchContractTest.cs`、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching`、`Bing.Caching.Tests`、`Bing.EasyCaching.Tests`、FreeRedis/CSRedis 单节点集成测试项目。
- ChangedPublicContracts：`CacheResourceOptions` 增加可选的 `maxScanKeys` 参数和只读属性 `MaxScanKeys`；默认值为 `0`，现有构造调用保持兼容。
- ChangedRuntimePaths：同步/异步前缀扫描、模式扫描、全量清理、流式前缀读取和 EasyCaching Redis/Hybrid 扫描；普通单键读写、批量读写和回源租约路径未改变。
- ChangedProviders：FreeRedis、CSRedis、EasyCaching Redis/Hybrid；客户端分片的委托扫描仍由各分片管理器执行。
- ChangedTFMs：生产 `netstandard2.0`；公共测试 `net6.0`、`net8.0`；单节点 Redis 集成验证 `net8.0`。
- ChangedBenchmarkHarness：无；本轮未运行正式基准，不生成 P95 或命令量 JSON。
- RiskLevel：MEDIUM。默认行为不变；启用正值上限后，超过预算的扫描可能在部分批次已完成后抛错。

### 本轮完成

- `CacheResourceOptions` 新增非负 `MaxScanKeys`；负值配置在构造时拒绝，`0` 保持无限制。
- 新增内部 `CacheScanBudget`，在达到上限时停止继续派发/消费并抛出 `InvalidOperationException`；异常明确提示缩小前缀或提高预算。
- FreeRedis、CSRedis 的同步扫描、异步扫描和流式前缀路径接入预算；EasyCaching 的 Redis/非 Redis 扫描、流式前缀、模式清理和 `Clear` 路径接入预算。
- 公共资源边界测试 net6.0/net8.0 各 `7/7` 通过；EasyCaching 扫描预算测试 net6.0/net8.0 各 `1/1` 通过。
- FreeRedis/CSRedis 单节点扫描预算契约各 `1/1` 通过，验证三键数据在预算为二时前两项可消费、第三次推进抛出 `MaxScanKeys`，并使用已知键批量清理。
- `Bing.Caching.Tests` 全量 net6.0/net8.0 各 `183/183` 通过；`Bing.EasyCaching.Tests` 全量 net6.0/net8.0 各 `57 passed / 52 skipped / 0 failed`。
- `Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` 的 `netstandard2.0` Release 构建均为 `0 warning / 0 error`。
- 本机 Redis 单节点 `127.0.0.1:6379` 返回 `+PONG`；FreeRedis 集成全量 net8.0 `93 passed / 20 skipped / 0 failed`，CSRedis 集成全量 net8.0 `90 passed / 19 skipped / 0 failed`。
- 修正 FreeRedis MONITOR 取消测试的过滤计数断言；`GetAllStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet` 单测及全量集成回归均通过，确认取消后没有第二条目标命名空间 MGET。

### 本轮未完成

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：本轮是扫描工作集保护，不是性能优化证明；没有正式基准数据，不能确认 P95、命令数、分配量预算。
- 已有单节点 Redis 的批量、扫描和取消命令级证据；FreeRedis/CSRedis 的 Cluster、跨槽批量、客户端分片、拓扑变化和部分成功行为继续由专用集成门控覆盖。
- 客户端分片外层 `GetByPrefixStreamAsync` 目前由每个物理分片的管理器分别应用预算；跨分片的总量预算以及 `RemoveByPrefix`/`Clear` 的全局总量保护仍需独立设计，不能把每分片上限误报为全局上限。
- 三主三从 Cluster、Hybrid 总线故障、100,000 项性能矩阵和网络/客户端队列指标仍等待外部环境。

### Review-Fix 状态

- 扫描候选键资源上限已实现，并通过双 TFM 公共/Mock 回归和生产构建。
- `FIX-007`：`OPEN_ACTIONABLE`；未生成新的正式性能 JSON，性能门禁没有改变。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 17（2026-10-06）

本轮收口客户端分片扫描预算的全局语义。上一轮的 `MaxScanKeys` 已在每个 `CacheManager` 内生效，但 `ShardedRedisCacheManager` 仍让每个物理分片分别创建预算，两个分片可能各自消费完整上限。现在 `CacheManager` 暴露程序集内部的共享预算入口；客户端分片在一次跨分片读取、流式读取、前缀/模式删除或 `Clear` 开始时创建一个 `CacheScanBudget`，顺序传给所有分片。公共 API 签名和默认 `0` 无限制行为保持不变。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs`、`ShardedRedisCacheManager.cs`、`ShardedScanCancellationUnitTest.cs`、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无；新增入口仅为程序集内部方法。
- ChangedRuntimePaths：客户端分片同步/异步前缀读取、流式读取、前缀删除、模式删除和 `Clear` 的候选键计数。
- ChangedProviders：EasyCaching Redis/Hybrid 客户端分片；单节点 Redis/Hybrid 管理器的本地预算路径保持。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；未运行正式性能探针。
- AffectedDependents：分片身份校验、取消后的尾部探测、部分成功和清理路径。
- RiskLevel：MEDIUM；跨分片操作达到上限时，前序分片可能已经完成部分删除或读取，仍不提供跨分片事务性。

### 本轮完成

- `GetByPrefix`、`GetByPrefixStreamAsync`、`RemoveByPrefix`、`RemoveByPattern`、`Clear` 及对应异步路径均支持共享扫描预算。
- 客户端分片一次操作只创建一个预算，预算跨所有分片累计；同步和异步扫描都在第一个超限候选处抛出 `MaxScanKeys` 异常。
- `ShardedScanCancellationUnitTest` 新增跨分片全局预算测试；net6.0/net8.0 各 `6/6` 通过。
- `Bing.EasyCaching.Tests` 完整 net6.0/net8.0 各 `59 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching` netstandard2.0 Release 构建 `0 warning / 0 error`。

### 本轮未完成

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：本轮是资源语义修复，没有正式 P50/P95/P99、分配量或命令数数据。
- EasyCaching 非 Redis Provider 的底层 `GetAllKeysByPrefixAsync` 仍可能先物化完整键列表；接口本身没有异步键流能力，需要后续适配器或上游 API 支持。
- Cluster、双独立 Redis 分片、Hybrid 总线故障、100,000 项矩阵和客户端队列/网络指标仍未完成。

### Review-Fix 状态

- 客户端分片扫描预算已从“每分片限制”提升为“每次跨分片操作全局限制”，并通过双 TFM 精准及完整单元回归。
- `FIX-007`：`OPEN_ACTIONABLE`；性能证据门禁没有因本轮改变。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 18（2026-10-06）

本轮处理 EasyCaching 非 Redis 异步扫描的键集合驻留问题。`IEasyCachingProvider.GetAllKeysByPrefixAsync` 的契约返回 `Task<IEnumerable<string>>`，具体 Provider 可能在异步方法完成前已经物化全部键。为内存 Provider 增加程序集内部键流适配入口：扫描和异步清理优先逐项消费同步键枚举并在每项检查取消；通用 `CacheManager` 仍保留原异步 Provider 回退，因此没有改变第三方 Provider 的兼容性或公共 API。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs`、`MemoryCacheManager.cs`、`CacheManagerTests.cs`、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无；新增构造入口和键流委托仅为程序集内部成员。
- ChangedRuntimePaths：内存 Provider 的前缀流式读取、异步前缀/模式删除和 `Clear` 的非 Redis 键枚举；Redis `IDatabase` 扫描路径不变。
- ChangedProviders：EasyCaching Memory；Redis/Hybrid 仍使用原生 Redis 扫描或现有异步 Provider 回退。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；本轮未运行正式性能探针。
- AffectedDependents：内存适配器注册、非 Redis Provider 的取消行为和键枚举回退。
- RiskLevel：LOW；同步内存枚举不涉及网络 I/O，逐项取消检查保留旧结果顺序。

### 本轮完成

- `MemoryCacheManager` 注册的 Provider 现在通过内部 `IAsyncEnumerable<string>` 委托逐项提供命名空间键。
- `GetByPrefixStreamAsync`、异步前缀删除、模式删除和 `Clear` 共用 `EnumerateProviderKeysAsync`；每个键在逻辑键校验前后都检查取消。
- 通用 `CacheManager` 没有键流委托时仍调用 `GetAllKeysByPrefixAsync`，保持第三方 Provider 兼容回退。
- 新增注入键流取消测试和真实 `MemoryCacheManager` 适配测试；精准测试 net8.0 `2/2` 通过。
- `Bing.EasyCaching.Tests` 完整 net6.0/net8.0 各 `61 passed / 52 skipped / 0 failed`。
- `Bing.EasyCaching` netstandard2.0 Release 构建 `0 warning / 0 error`。

### 本轮未完成

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：本轮改善的是内存 Provider 的键枚举驻留和取消边界，没有新的正式 P50/P95/P99、分配量或命令数证据。
- 未实现通用第三方非 Redis Provider 的原生异步键流；没有内部键流委托的实例仍可能由 `GetAllKeysByPrefixAsync` 先物化完整集合。
- Cluster、双独立 Redis 分片、Hybrid 总线故障、100,000 项性能矩阵和客户端队列/网络指标仍未完成。

### Review-Fix 状态

- 内存 Provider 的异步扫描键流适配已完成并通过双 TFM 项目级回归。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮没有重复同一 Redis 性能实验。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 19（2026-10-06）

本轮补齐通用非 Redis Provider 的异步键流扩展点。新增可选公共接口 `Bing.Caching.ICacheKeyStreamProvider`；`CacheManager` 检测到底层 Provider 实现该接口后，优先直接消费 `IAsyncEnumerable<string>`，并在每项检查取消。未实现该接口的 Provider 继续使用原 `GetAllKeysByPrefixAsync` 回退，现有调用方和 Provider 不需要迁移。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching/Bing/Caching/ICacheKeyStreamProvider.cs`、`CacheManager.cs`、EasyCaching README、`CacheManagerTests.cs`、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.Caching`、`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：新增可选接口 `ICacheKeyStreamProvider`；未修改 `ICache` 或 EasyCaching Provider 原有接口。
- ChangedRuntimePaths：非 Redis Provider 的前缀流式读取、异步前缀/模式删除和 `Clear` 的键枚举选择；Redis `IDatabase` 扫描路径不变。
- ChangedProviders：实现新接口的第三方非 Redis Provider；未实现者保持旧回退。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；本轮未运行正式性能探针。
- AffectedDependents：Provider 包装器、EasyCaching 扫描取消和键命名空间过滤。
- RiskLevel：LOW；接口是可选能力，所有物理键仍经过既有布局和命名空间校验。

### 本轮完成

- 新增带中文契约说明的 `ICacheKeyStreamProvider`，明确输入为物理命名空间前缀，输出为物理键流。
- `CacheManager.EnumerateProviderKeysAsync` 优先使用该接口，继续逐项检查取消并保留旧异步集合回退。
- 新增 Provider 实现接口时的优先级测试，验证旧 `GetAllKeysByPrefixAsync` 不会被调用。
- `Bing.Caching` 与 `Bing.EasyCaching` netstandard2.0 Release 构建各 `0 warning / 0 error`。
- `Bing.EasyCaching.Tests` 完整 net6.0/net8.0 各 `62 passed / 52 skipped / 0 failed`。

### 本轮未完成

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：新增的是可扩展性和键流契约，没有新的正式 P50/P95/P99、分配量或命令数证据。
- EasyCaching 1.9.2 自带的非 Redis Provider 尚未实现新接口；内存 Provider 仍由 `MemoryCacheManager` 内部适配，其他 Provider 仍回退完整键集合。
- Cluster、双独立 Redis 分片、Hybrid 总线故障、100,000 项性能矩阵和客户端队列/网络指标仍未完成。

### Review-Fix 状态

- 通用非 Redis 键流扩展点已完成，旧 Provider 回退和命名空间过滤通过双 TFM 回归。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮没有重复同一 Redis 性能实验。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 20（2026-10-06）

本轮补齐扫描预算超限的结构化错误语义。之前所有 Provider 都抛普通 `InvalidOperationException`，调用方无法稳定区分资源拒绝与其他运行时错误，也无法记录异常发生前已经接受的候选键数量。新增公开的 `CacheScanLimitExceededException`，继续继承 `InvalidOperationException` 以保持旧捕获代码兼容；`Limit` 表示配置的 `MaxScanKeys`，`Consumed` 表示拒绝当前候选前已经通过命名空间和物理键校验的候选逻辑键数量。扫描、流式读取和清理仍可能在抛错前完成部分工作，不提供跨键事务性。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching/Bing/Caching/CacheScanLimitExceededException.cs`、`CacheScanBudget.cs`、公共资源边界测试、EasyCaching/FreeRedis/CSRedis 扫描预算测试、EasyCaching README、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.Caching`、`Bing.EasyCaching`、`Bing.Caching.Tests`、FreeRedis/CSRedis 集成测试项目。
- ChangedPublicContracts：新增 `CacheScanLimitExceededException` 及只读属性 `Limit`、`Consumed`；未修改缓存接口方法签名。
- ChangedRuntimePaths：所有复用 `CacheScanBudget` 的同步/异步扫描、前缀流、模式删除和 `Clear` 超限路径；正常命中读写不变。
- ChangedProviders：FreeRedis、CSRedis、EasyCaching Redis/Hybrid、Memory 和自定义实现预算适配路径。
- ChangedTFMs：生产 `netstandard2.0`；公共和 EasyCaching 测试 `net6.0`、`net8.0`；Redis 集成测试保持环境门控。
- ChangedBenchmarkHarness：无；本轮只补结构化错误和可观测字段，没有运行正式性能探针。
- RiskLevel：LOW；异常仍是 `InvalidOperationException` 子类，默认 `MaxScanKeys=0` 的行为不变。

### 本轮完成

- 新增 `CacheScanLimitExceededException`，校验正的 `Limit` 和不超过上限的 `Consumed`，并提供中文错误信息。
- `CacheScanBudget.Consume` 达到上限时改抛结构化异常；已消费计数不再递增，调用方可以区分资源拒绝并保留部分结果语义。
- 公共资源边界、EasyCaching、FreeRedis 和 CSRedis 扫描预算测试断言异常类型、`Limit` 和 `Consumed`；旧 `InvalidOperationException` 捕获契约仍由继承关系覆盖。
- `Bing.Caching`、`Bing.EasyCaching` netstandard2.0 Release 构建均为 `0 warning / 0 error`；`Bing.Caching.Tests` net6.0/net8.0 各 `183/183` 通过，`Bing.EasyCaching.Tests` 各 `62 passed / 52 skipped / 0 failed`。FreeRedis/CSRedis 扫描预算集成用例各 `0 passed / 1 skipped / 0 failed`（未启用隔离 Redis），随后更新本轮执行记录、性能说明和生产符号映射。

### 本轮未完成与新增 TODO

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：结构化异常提升了错误观测，不提供批量读/扫描/命中读的 P50/P95/P99 或分配收益证据。
- `CacheScanLimitExceededException.Consumed` 只表示客户端已接受的候选键，不表示 Redis `SCAN` 服务端工作量；仍需命令计数、网络队列和峰值工作集探针。
- 跨分片预算超限允许前序分片部分成功；尚未提供可恢复游标、失败分片清单或跨分片补偿协议。
- 第三方非 Redis Provider 是否实现 `ICacheKeyStreamProvider` 仍由 Provider 维护，EasyCaching 内置非内存 Provider 仍可能物化完整键集合。
- 三主三从 Cluster、双独立分片、Hybrid 总线故障、100,000 项矩阵和生产级指标仍等待外部环境。

### Review-Fix 状态

- 扫描预算拒绝已具有公开、可断言的异常类型和资源计数，默认行为及旧异常捕获兼容性保持。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮没有重复同一 Redis 性能实验。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 21（2026-10-06）

本轮补齐客户端分片扫描达到 `MaxScanKeys` 后的部分成功观测。此前异常只有全局候选键计数，调用方无法知道哪些分片已经完整结束、哪个分片正在产生部分读取或删除。保留 `CacheScanLimitExceededException` 类型和旧构造器，在共享 `CacheScanBudget` 中记录当前顺序分片；超限时直接生成 `CompletedShards` 与 `FailedShard`，不收集异步流、不改变分片顺序或正常路径。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching/Bing/Caching/CacheScanLimitExceededException.cs`、`CacheScanBudget.cs`、`ShardedRedisCacheManager.cs`、资源边界和分片扫描测试、EasyCaching README、本执行记录、性能报告和生产符号映射。
- ChangedProjects：`Bing.Caching`、`Bing.EasyCaching`、`Bing.Caching.Tests`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：异常新增只读 `CompletedShards`、`FailedShard`；原缓存接口和异常构造器保持兼容。
- ChangedRuntimePaths：客户端分片的同步/异步前缀读取、模式清理、前缀清理和 `Clear` 的预算超限路径；非分片异常默认返回空分片信息。
- ChangedProviders：EasyCaching 客户端分片 Redis/Hybrid 组合；底层单节点 Provider 的扫描命令不变。
- ChangedTFMs：生产 `netstandard2.0`；公共和 EasyCaching 测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；本轮没有运行性能探针。
- AffectedDependents：需要对跨分片部分删除或部分读取做重试/告警的调用方。
- RiskLevel：MEDIUM；仅在资源上限异常中增加诊断字段，异常仍继承 `InvalidOperationException`。

### 本轮完成

- `CacheScanBudget` 支持记录当前分片上下文；分片扫描在进入每个分片前设置已完成数量和当前失败索引。
- `CacheScanLimitExceededException` 新增 `CompletedShards` 和 `FailedShard`，非分片路径返回空集合和 `null`，不改变原 `Limit`、`Consumed` 和消息。
- 客户端分片所有扫描/清理路径接入该上下文，包括同步/异步前缀、模式和 `Clear`；异步流保持逐项产出，不物化分片结果。
- `ShardedScanCancellationUnitTest` 双 TFM 精准测试各 `6/6` 通过；公共资源边界测试断言非分片默认值；公共和 EasyCaching 完整回归各通过。
- `Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` netstandard2.0 构建均为 `0 warning / 0 error`；更新 README、性能说明和生产符号映射。

### 本轮未完成与新增 TODO

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：本轮只改善部分成功观测，不提供性能预算证据，也没有重复被 No Progress Guard 暂停的实验。
- `FailedShard` 不是恢复游标；失败分片可能已经删除或读取部分键，尚未提供幂等重试、游标续扫或补偿协议。
- `CompletedShards` 只适用于当前顺序分片执行；Cluster 主节点、拓扑变化和并行分片仍缺少统一的失败节点清单。
- 第三方非 Redis Provider 的键流接口采用情况、三主三从 Cluster、双独立分片/Hybrid 故障矩阵和 100,000 项性能矩阵仍等待外部环境或后续任务。
- Redis 服务端扫描成本、网络请求数和 Provider 内部排队仍未形成生产级观测指标。

### Review-Fix 状态

- 客户端分片资源拒绝现在能报告完成分片和当前失败分片，正常路径和流式内存边界保持不变。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮没有重复同一性能实验。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 22（2026-10-06）

本轮处理 EasyCaching 前缀扫描的重复解析开销。Redis/Hybrid 前缀读取和删除此前对服务端返回的每个物理键先执行完整的 URI 解码、规范性检查和哈希计算，再用逻辑键 `StartsWith` 判断前缀；FreeRedis/CSRedis 已经具备物理编码前缀快速筛选。现在由共享 `CacheKeyFormat` 在解码前比较规范编码前缀，匹配后只执行一次完整的逻辑键还原和哈希校验。模式删除、`Clear` 和无前缀扫描继续走完整解析路径，避免改变 glob 语义。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching/Bing/Caching/CacheKeyFormat.cs`、`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs`、键格式单元测试、本执行记录、性能说明和生产符号映射。
- ChangedProjects：`Bing.Caching`、`Bing.EasyCaching`、`Bing.Caching.Tests`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：无；新增的前缀感知还原入口是程序集内部方法。
- ChangedRuntimePaths：EasyCaching Redis/Hybrid 的同步前缀读取、异步前缀流、同步前缀删除和异步前缀删除；模式和 Clear 路径不变。
- ChangedProviders：EasyCaching Redis/Hybrid；FreeRedis/CSRedis 原有路径不变。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；本轮未运行正式性能探针。
- AffectedDependents：依赖 EasyCaching 前缀扫描的命名空间过滤、特殊逻辑键和预算计数调用方。
- RiskLevel：LOW；编码前缀只是早期筛选，命名空间、物理布局、规范编码和哈希校验仍由同一完整内核确认。

### 本轮完成

- `CacheKeyFormat.TryGetLogicalKeyWithPrefix` 在 URI 解码和哈希计算前拒绝不匹配前缀，同时保留规范编码、Unicode、花括号、通配符、反斜线和相似命名空间校验。
- EasyCaching `GetByPrefix`、`GetByPrefixStreamAsync`、`RemoveByPrefix` 和 `RemoveByPrefixAsync` 传递预编码前缀；模式删除和 `Clear` 仍使用通用解析，逻辑行为不变。
- 新增键格式单元测试，覆盖匹配还原、非匹配、相似命名空间和篡改哈希；net6.0/net8.0 各 `19/19` 通过。
- EasyCaching 前缀流精准回归 net6.0/net8.0 各 `10 passed / 2 skipped / 0 failed`；`Bing.Caching`、`Bing.EasyCaching` netstandard2.0 Release 构建均为 `0 warning / 0 error`。
- 受影响项目完整回归：`Bing.Caching.Tests` net6.0/net8.0 各 `184/184`；`Bing.EasyCaching.Tests` net6.0/net8.0 各 `62 passed / 52 skipped / 0 failed`。测试输出保留已知的离线 `NU1801` 和近似依赖 `NU1603` 警告。
- `git diff --check` 通过；没有修改独立 `review.md`，没有 commit、push 或发布。

### 本轮未完成与新增 TODO

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：本轮是结构性解析优化，没有新的 P50/P95/P99、分配量或 Redis 命令数正式样本。
- `CacheScanBudget` 仍只限制客户端已验证候选，不限制 Redis 服务端 SCAN 遍历、Provider 内部队列或网络工作量；需要独立的服务端/客户端观测探针。
- 跨分片失败仍没有可恢复游标、幂等重试和补偿协议；Cluster 节点失败清单也未统一到当前异常进度模型。
- 第三方非 Redis Provider 的原生异步键流采用情况、三主三从 Cluster、双独立分片/Hybrid 故障矩阵和 100,000 项性能矩阵仍等待环境或后续任务。

### Review-Fix 状态

- EasyCaching 前缀热路径现在在完整解码前完成物理编码前缀筛选，并通过双 TFM 键格式和前缀流回归。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮没有重复既有 Redis 基准实验。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 23（2026-10-06）

本轮继续收敛前缀扫描热路径：FreeRedis 和 CSRedis 的逻辑键流需要返回逻辑键，原路径会对每个 SCAN 返回的物理键先完整解码、规范校验和哈希计算，再判断逻辑前缀。现在与 EasyCaching 共用 `CacheKeyFormat.TryGetLogicalKeyWithPrefix`，先比较预编码物理前缀，匹配后只做一次完整还原；只返回物理键的路径继续使用已有 `TryMatchLogicalPrefix`。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching.FreeRedis/Bing/Caching/FreeRedis/FreeRedisCacheManager.Stream.cs`、`framework/src/Bing.Caching.CSRedis/Bing/Caching/CSRedis/CSRedisCacheManager.Stream.cs`、本执行记录、性能说明和生产符号映射。
- ChangedProjects：`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis` 及其集成测试项目。
- ChangedPublicContracts：无；只替换 Provider 内部扫描筛选入口。
- ChangedRuntimePaths：FreeRedis/CSRedis 异步前缀流中返回逻辑键的分支；同步扫描、物理键流、模式删除、预算、取消和拓扑校验不变。
- ChangedProviders：FreeRedis、CSRedis；EasyCaching 和公共键格式代码复用上一轮实现。
- ChangedTFMs：生产 `netstandard2.0`；集成测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无；本轮未运行正式性能探针。
- AffectedDependents：前缀流特殊键、命名空间隔离、扫描预算和取消行为。
- RiskLevel：LOW；快速入口仍执行完整物理布局、规范编码和哈希校验，只有不匹配前缀的键提前返回。

### 本轮完成

- FreeRedis `EnumerateMatchingKeysAsync` 和 CSRedis 同名逻辑键流现在传递预编码前缀，避免对非匹配键做 URI 解码和哈希计算。
- FreeRedis、CSRedis、公共 `Bing.Caching` 的 `netstandard2.0` Release 构建均为 `0 warning / 0 error`。
- 两个 Provider 的特殊键精准测试在 net6.0/net8.0 各 `0 passed / 4 skipped / 0 failed`；跳过原因是未启用隔离 Redis，不能替代真实扫描验证。
- 未修改独立 `review.md`，没有 commit、push 或发布。

### 本轮未完成与新增 TODO

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：本轮是结构性筛选优化，没有新的正式 P50/P95/P99、分配量或命令数样本。
- Redis 服务端 SCAN 遍历量、网络请求数和 Provider 内部排队仍没有生产级观测或硬上限。
- 跨分片失败恢复、Cluster 三主三从和 Hybrid 故障矩阵、100,000 项矩阵仍等待对应环境或后续设计。
- FreeRedis/CSRedis 这条新路径缺少 Redis 实机执行证据；现有特殊键测试门控状态必须保持公开记录。

### Review-Fix 状态

- FreeRedis/CSRedis 逻辑键流已与 EasyCaching 使用一致的编码前缀早期筛选，并完成生产构建验证。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮没有重复同一 Redis 基准实验。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 24（2026-10-06）

本轮补齐扫描工作量的客户端可观测性。此前 `CacheCommandProbe` 只记录逻辑 `SCAN` 调用次数，无法区分一次扫描调用返回了多少页或多少物理候选；本轮为显式 FreeRedis/CSRedis 扫描页记录页边界和逐项计数，为 EasyCaching 隐藏页边界的 `Keys/KeysAsync` 路径记录逐项计数并保留未知页数标记。该数据用于解释扫描工作量，不把客户端返回项数误报为 Redis 服务端遍历总量。

### Change Impact Analysis

- ChangedFiles：`framework/src/Bing.Caching/Bing/Caching/CacheCommandProbe.cs`、FreeRedis/CSRedis 同步与异步扫描路径、`framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.cs`、`CacheCommandProbeTest`、CacheBench `Program.cs`/`README.md`、本执行记录、性能说明和生产符号映射。
- ChangedProjects：`Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching`、`Bing.Caching.Tests`、current/baseline CacheBench。
- ChangedPublicContracts：无；探针字段、扫描计数入口和 CacheBench 原始报告字段均为内部/基准契约。
- ChangedRuntimePaths：Redis 显式扫描循环和 EasyCaching 隐藏页扫描循环只增加计数，不改变键过滤、预算、取消、删除、拓扑校验或批处理派发。
- ChangedProviders：FreeRedis、CSRedis、EasyCaching Redis/Hybrid；内存和公共列表 API 行为不变。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`；基准按 current/baseline 构建。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：CacheBench 的 `PhaseReport`、`PhaseSummary` 和 JSON/CSV 输出新增 `scanPages`、`scanReturnedItems`；baseline 以 `-1` 表示未采集。
- AffectedDependents：性能报告消费者和解析原始报告的脚本需要接受新增字段；旧字段保持不变。
- RiskLevel：LOW；计数只在活动探针期间执行，EasyCaching 无法观察隐藏页边界时明确返回 `scanPages=-1`。

### 本轮完成

- `CacheCommandProbe` 新增扫描页和返回物理项计数；页边界未知时 `ScanPages=-1`，返回项计数仍可用，避免把逻辑 `SCAN` 调用数当成服务端工作量。
- FreeRedis/CSRedis 的同步、异步和逻辑/物理扫描循环记录每个返回页及每个物理候选；EasyCaching Redis/Hybrid 的数据库、注入键流、可选 Provider 键流和兼容回退路径记录返回项。
- CacheBench current/baseline 的每个阶段报告、聚合报告和 README 输出 `scanPages`、`scanReturnedItems`，并明确这些值不代表网络请求、队列长度或 Redis 服务端总遍历量。
- `CacheCommandProbeTest` 在 net6.0/net8.0 各 `8/8` 通过，覆盖显式页计数、逐项计数和页边界未知状态。
- `Bing.Caching`、FreeRedis、CSRedis、EasyCaching 的 `netstandard2.0` Release 构建均为 `0 warning / 0 error`；current/baseline CacheBench 构建 `0 error`，保留已知离线 `NU1801` 与近似依赖 `NU1603` 警告。
- `Bing.Caching.Tests` 完整回归在 net6.0/net8.0 各 `186/186` 通过；未运行 Redis 正式基准，未修改独立 `review.md`，没有 commit、push 或发布。

### 本轮未完成与新增 TODO

- `FIX-007` 仍为 `OPEN_ACTIONABLE`：本轮只增加观测字段，没有新增可比较的 P50/P95/P99、分配量、峰值工作集或 Redis 命令数样本。
- `scanPages`/`scanReturnedItems` 仍是客户端代理指标；Redis 服务端实际遍历、网络请求、Provider 内部排队和跨节点重试仍需要服务端 INFO/MONITOR、客户端事件或专用探针才能量化。
- EasyCaching 的第三方 `Keys/KeysAsync` 仍隐藏底层页边界，因此只能报告返回项数；需要 Provider 原生分页契约才能补齐页级指标。
- 跨分片失败恢复、可恢复游标、Cluster 三主三从、双独立分片/Hybrid 故障矩阵和 100,000 项性能矩阵仍等待对应环境或后续任务。

### Review-Fix 状态

- 扫描报告现在能区分逻辑调用、显式扫描页和客户端返回物理项，且明确未知边界和指标限制。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮没有重复既有 Redis 基准实验。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 25（2026-10-06）

本轮补齐流式消费中途取消的批次边界证据。此前部分测试只验证进入流之前取消，或只统计输入枚举数量，不能同时证明首批已经产出后没有继续枚举键、没有派发下一次批量读取。现在 EasyCaching 的可选键流和 FreeRedis/CSRedis 的真实 Redis 测试都覆盖首批消费后取消；真实 Provider 用命令监控确认目标命名空间只发送一次 MGET，未启用 Redis 时继续按门控跳过。

### Change Impact Analysis

- ChangedFiles：`framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs`、FreeRedis/CSRedis 集成 `BatchContractTest.cs`、本执行记录、生产符号映射。
- ChangedProjects：`Bing.EasyCaching.Tests`、`Bing.Caching.FreeRedis.Tests.Integration`、`Bing.Caching.CSRedis.Tests.Integration`。
- ChangedPublicContracts：无；仅增加回归测试。
- ChangedRuntimePaths：只验证已有 `GetAllStreamAsync`/`GetByPrefixStreamAsync` 取消路径，不修改生产实现。
- ChangedProviders：EasyCaching 可选键流、FreeRedis、CSRedis。
- ChangedTFMs：测试 `net6.0`、`net8.0`；Redis 集成测试继续受环境门控。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：无生产调用方；测试只依赖现有 Redis 监控辅助设施。
- RiskLevel：LOW；新增断言不会改变缓存运行时行为。

### 本轮完成

- EasyCaching `GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextRead` 使用注入键流，消费首批两项后取消，确认键流只枚举两项且 `GetAllAsync` 只调用一次；net6.0/net8.0 精准测试各 `1/1` 通过。
- EasyCaching 已有兼容 Provider 键列表路径的首批取消回归一并保留，覆盖“首批读取完成后不派发下一批”。
- FreeRedis `GetAllStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet` 使用 `RedisCommandMonitor` 统计真实 MGET；CSRedis 增加同名契约。当前未配置 `RUN_INTEGRATION_TESTS`/Redis，专项测试按门控 `0 passed / 1 skipped / 0 failed`，但项目可编译。
- 未修改生产代码、公共 API 或 benchmark；没有 commit、push 或发布。

### 本轮未完成与新增 TODO

- `FIX-007` 仍为 `OPEN_ACTIONABLE`，本轮是测试证据补齐，没有重复既有性能实验。
- FreeRedis/CSRedis 的真实 MGET 计数仍需在隔离 Redis 环境执行；门控跳过不能替代实机证据。
- `GetByPrefixStreamAsync` 在 Cluster/分片首批成功后节点故障的异常传播、已产出数量和部分成功边界仍未覆盖。
- 大数据量 List/Stream 对照、Hybrid 实机批量回填、特殊键流式前缀和 100,000 项矩阵仍等待对应环境或后续测试任务。

### Review-Fix 状态

- 三类 Provider 现在都有首批消费后取消的测试入口；EasyCaching mock 证据已通过，FreeRedis/CSRedis 实机证据保留为环境门控。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮未执行同类性能基准。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。

## Review-Fix Round 26（2026-10-06）

本轮补齐 Cluster 缺少扫描主节点时的流式故障契约。原有故障测试只覆盖 List 前缀读取和 `Clear`，流式 API 的异常传播未被直接验证，存在把局部结果误认为完整成功的证据缺口。现在 FreeRedis 与 CSRedis 的同一故障场景同时断言 `GetAllStreamAsync` 和 `GetByPrefixStreamAsync` 抛出异常；生产代码和公共契约没有改变。

### Change Impact Analysis

- ChangedFiles：FreeRedis/CSRedis 集成 `ClusterFailureTest.cs`、本执行记录、生产符号映射。
- ChangedProjects：`Bing.Caching.FreeRedis.Tests.Integration`、`Bing.Caching.CSRedis.Tests.Integration`。
- ChangedPublicContracts：无；仅增加门控集成断言。
- ChangedRuntimePaths：只验证 Cluster 扫描缺节点时已有流式路径的失败传播，不修改实现。
- ChangedProviders：FreeRedis、CSRedis Cluster。
- ChangedTFMs：测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- AffectedDependents：无生产调用方；集成测试依赖隔离三主三从 Redis Cluster。
- RiskLevel：LOW；测试仅扩大故障场景覆盖。

### 本轮完成

- FreeRedis `MissingScanMaster_FailsInsteadOfReturningPartialResults` 新增 `GetAllStreamAsync`、`GetByPrefixStreamAsync` 异常断言。
- CSRedis 同一测试新增两条流式异常断言，保持 List/Stream 都不宣称局部成功。
- 两个 Provider 的 net6.0/net8.0 精准测试均完成编译；由于未启用 Cluster 门控，结果各为 `0 passed / 1 skipped / 0 failed`。
- 未修改生产代码、公共 API 或 benchmark；没有 commit、push 或发布。

### 本轮未完成与新增 TODO

- 三主三从 Cluster 实机故障证据仍缺失，门控跳过不能证明运行时行为已通过。
- 分片节点故障后的流式异常、已产出数量和部分成功进度仍未覆盖。
- Hybrid 批量实机一致性、大数据 List/Stream 对照、特殊键流式前缀和 100,000 项矩阵仍待环境支持。
- `FIX-007` 性能预算仍为 `OPEN_ACTIONABLE`；本轮未重复同类性能实验。

### Review-Fix 状态

- Cluster 流式故障测试入口已与 List API 对齐，异常传播边界明确。
- `FIX-007`：`OPEN_ACTIONABLE`；本轮没有性能基准执行。
- Round result：`PARTIAL`。
- Release status：`NOT_APPROVED`；未执行 commit、push 或发布。
