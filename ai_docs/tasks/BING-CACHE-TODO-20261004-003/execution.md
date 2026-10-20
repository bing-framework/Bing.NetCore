<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-CACHE-TODO-20261004-003
AI_EXECUTION_FINISHED_AT: 2026-10-04T13:49:35Z

# 缓存 TODO 续做执行记录

状态：PARTIAL。EasyCaching Cluster 与客户端分片、单节点及双分片大规模一致性已有运行证据；性能预算和部分故障场景仍未完成。未提交、推送或发布。

## 已完成

- EasyCaching `GetAllStreamAsync(IEnumerable<CacheKey>)` 和对应异步 List 路径改为惰性投影；同步接口的原校验时机保持不变。
- `CacheKeyFormat.GetPhysicalKeys` 在批内复用 SHA256 实例与命名空间前缀，输出与单键格式逐项相同；FreeRedis、CSRedis、EasyCaching 批量读取接入。
- FreeRedis 单键读写、删除和批量写入在路由和值操作间复用同一物理键，避免重复哈希。
- 两个 Redis Provider 补首批产出后取消、第二批反序列化异常、大输入 List/Stream 顺序与缺失语义测试。EasyCaching 补惰性输入、批次取消及 Redis/Hybrid 真实批量一致性测试。
- 隔离单节点、双独立分片和三主三从 Cluster 均成功建立。Cluster 跨槽读、流式读取、扫描、清理以及遗漏/故障主节点显式报错测试通过。

## 验证

- `Bing.Caching.Tests` net6.0/net8.0 各 106/106 通过。
- FreeRedis 集成测试 net6.0/net8.0 各 76 通过、11 项专项门控跳过；CSRedis 各 74 通过、10 项专项门控跳过。新增 `BatchContractTest` 两个项目各 9/9 通过；双分片各 6/6 通过。
- EasyCaching net6.0/net8.0 各 31/31 通过，包含 Redis/Hybrid 真实运行测试及多节点配置拒绝测试。
- 三主三从 Cluster 的两个 `ThreeMasters_CrossSlotReadScanAndClear` 测试通过；两个 `MissingScanMaster_FailsInsteadOfReturningPartialResults` 测试在停掉 7000 主节点后通过。FreeRedis `OmittedOnlineMaster_RejectsScanAndClearBeforePartialSuccess` 通过。
- 四个受影响生产项目的 netstandard2.0 构建通过；`git diff --check` 通过。
- 独立审查指出的 Hybrid 回填测试已改为仅向分布式 Provider 预置数据，读前确认本地层缺失，读后分别验证 List 与 Stream 回填；EasyCaching net6.0/net8.0 各 31/31 通过。
- 副本晋升测试改为从 `CLUSTER NODES` 动态选择三主节点并等待 16384 槽完整；停止测试容器 7000 后 FreeRedis/CSRedis 的 `PromotedReplica_UpdatedTopologyReadsAndClearsAllSlots` 均通过。
- 上轮 EasyCaching Redis/Hybrid 对非 `SingleNode` 配置明确拒绝；此限制已在本轮缩小为仅拒绝客户端分片。上轮注册边界测试和 netstandard2.0 构建通过。
- `Bing.All.sln --no-restore` 构建 0 错误，既有依赖/分析器警告 129 项。

## 续做进展（2026-10-04）

- EasyCaching 新增专用客户端分片 Redis/Hybrid 注册及 `ShardedRedisCacheManager`：按物理键哈希标签路由，批量读保留顺序、重复键和缺失位置；扫描覆盖所有独立节点；操作检查节点 `run_id` 与固定顺序，拓扑变化显式失败。
- Hybrid 分片使用每分片独立 Redis 总线与主题。EasyCaching 1.9.2 的单个总线实例只保存一个订阅处理器；共用总线会使前面分片的失效处理器被覆盖。租约回源提交成功后补写本地层。
- 隔离双节点下 `ShardedRedis_CrossNodeBatchStreamScanClearAndRetriever_Work`、`ShardedHybrid_CrossInstanceBatchStreamInvalidationAndClearConverge` 等专项用例通过；加入总线失败用例后 EasyCaching 全量 net6.0/net8.0 各 41 通过、2 项 Cluster 门控跳过。
- Cluster 异步批量读写/删除取消时等待已派发命令结束并保留原异常；三个取消期间派发测试通过。
- 本轮 `Bing.EasyCaching` netstandard2.0 构建 0 警告、0 错误；`Bing.All.sln --no-restore` 构建 0 错误、120 个既有依赖/NuGet 警告；`git diff --check` 通过。
- FreeRedis 与 CSRedis 均新增 Redis `MONITOR` 命令级取消用例，使用唯一标记命令作顺序屏障，确认消费首批后取消时只派发一批 `MGET`；两个项目在隔离单节点的 net6.0/net8.0 精准测试均通过。
- EasyCaching 双分片 Hybrid 回源发布失败用例在隔离双节点通过：异常传播、Redis 已提交、本地层未写入。此用例不覆盖批量发布失败。
- EasyCaching 分片批量读取在返回前复验所有节点的 `run_id`，节点在读取过程中被替换时显式失败；单键/批量读取前和批量读取期间替换的三个精准单元测试在 net6.0/net8.0 通过。
- 独立审查发现单键操作原先只有命令前身份校验；现已在同步/异步单键读写、删除、回源路径成功完成后复验路由节点 `run_id`，新增同步/异步读取、同步写入、异步删除期间替换测试。
- Hybrid 总线增加单次操作的发布结果跟踪。单节点及分片注册将跟踪器交给 Hybrid Provider，适配层在单键与批量写入/删除后检查 EasyCaching 内部吞掉的发布异常；真实 Hybrid Provider 加故障总线的两个批量测试通过。
- Hybrid 管理器构造器收窄为程序集内部使用，公开注册入口统一创建 Hybrid Provider 和同一个总线跟踪器；传入未包装总线显式失败。新增构造边界测试，并更新两个程序集内手工构造的实机测试调用。
- 双独立 Redis 节点下 EasyCaching 全量 net6.0/net8.0 各 51 项通过、2 项 Cluster 门控跳过；生产 netstandard2.0 构建通过，`Bing.All.sln --no-restore` 构建 0 错误、120 项既有依赖/NuGet 警告，`git diff --check` 通过。

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX；Fix Scope：recommended；Review 文件：`ai_docs/tasks/BING-CACHE-TODO-20261004-003/review.md`。

#### FIX-007

- 严重程度：HIGH；处理要求：MUST_FIX；执行状态：PARTIAL，性能预算仍未达标。
- 结构性归因：1k 批读中逐项 JSON 解码平均分配约 2.66-2.71 MB；固定大小 JSON 数组单次解析在诊断探针中约 0.11-0.14 MB，原始数据见 `BING-CACHE-PERF-STREAM-20261004-002/bench/diagnostic-components-*.json`。
- 修改文件：`CSRedisCacheManager.Stream.cs`、`FreeRedisCacheManager.Stream.cs`、FreeRedis `BatchContractTest.cs`、性能探针和报告。CSRedis 每批解析一次；FreeRedis 仅对标准 JsonConvert 委托启用，自定义序列化逐项回退。保留 `Hit` 与 `null/default` 语义。
- 验证：两个 Provider 的批量契约 net6.0/net8.0 分别为 FreeRedis 11/11、CSRedis 10/10；完整单节点集成回归 net6.0/net8.0 分别为 FreeRedis 80 通过/11 门控跳过、CSRedis 77 通过/10 门控跳过；双分片精准测试 net8.0 各 6/6；两个生产项目 netstandard2.0 构建通过。EasyCaching 真实双分片重启测试 net6.0/net8.0 各 1/1；`Bing.All.sln --no-restore` 0 错误、128 个既有警告；`git diff --check` 通过。
- 同环境固定旧版/当前版 1k/10k、2 轮预热和 10 轮采样见 `BING-CACHE-PERF-STREAM-20261004-002/performance.md` 及 `bench/fix007-*.json`。批读分配回退 FreeRedis 为 +69.6%/+73.2%，CSRedis 为 +165.7%/+182.5%；扫描分配回退为 +97.0%/+99.1% 和 +175.1%/+186.6%。扫描及 CSRedis 10k 批读也有 P95 超预算项。因此不能将 FIX-007 标为完成。
- 下一步：保留已验证的批量解析收益；需要新的保持物理键隔离与拓扑语义的结构性方案，再复测既定预算。本轮已按 No Progress Guard 停止同类微调。

### Round 1 汇总

- MUST_FIX：1；已完成：0；PARTIAL：1；BLOCKED：0；FAILED：0。
- 当前执行状态：PARTIAL，独立 `review.md` 保持原状态，下一步由 Reviewer 基于新证据重新审查。

## Goal 续做记录（2026-10-04）

- Change Impact Analysis：新增或修改 `Bing.EasyCaching.Tests`、FreeRedis/CSRedis 的 `BatchContractTest` 及本报告；公共契约、生产运行路径、TFM 和基准探针均未变化。风险为中：故障测试会对显式隔离的 Redis 总线执行 `CLIENT KILL` 或短时 `CLIENT PAUSE`，分别由专用环境变量门控。
- FreeRedis/CSRedis 新增各 10k 项的前缀 List/Stream 对照，含 Unicode、花括号、星号、问号、反斜线和相似命名空间哨兵键；逐项校验逻辑键与值。每个测试使用自己的命名空间。两个项目 net6.0/net8.0 精准测试均各 1/1 通过。
- 新增独立数据 Redis 与总线 Redis 的 EasyCaching Hybrid 故障测试。`HybridBus_SubscriptionDisconnect_ReconnectsAndInvalidatesPeer` 主动断开两个订阅连接，确认重订阅后跨实例失效恢复；`HybridBus_PublishDuringPause_ReportsFailureAndRecovers` 在总线暂停期间证明数据层已提交、发布异常传播，恢复后下一次发布再次使对端本地层失效。net6.0/net8.0 各 2/2 通过。运行要求 `RUN_INTEGRATION_TESTS=true`、不同的数据/总线端点，并分别显式设置 `BING_TEST_ALLOW_REDIS_CLIENT_KILL=true` 或 `BING_TEST_ALLOW_REDIS_PAUSE=true`。
- 尝试重建任务专用三主三从 Cluster，槽位完整覆盖 16384；但 Docker Desktop 桥接网络向宿主机客户端公布不可达的 `172.19.x.x`，FreeRedis 跨槽测试在路由阶段重试。把公告 IP 改成 `127.0.0.1` 又会破坏容器间集群连接；host 网络探针也无法从宿主机访问。该次测试已终止，全部临时 Cluster 容器和网络已清理；不能将其记为本轮 Cluster 路径通过。
- `FIX-007` 性能预算仍未通过；本轮没有变动生产热路径，因此复用上轮固定基线与候选性能证据，不重复高成本采样。
- EasyCaching 客户端分片补充真实在途节点重启测试：以专用 Docker 容器端口核对为前置，在 `CLIENT PAUSE` 保持读取未完成时重启目标节点，确认在途读取未返回成功、旧管理器拒绝身份变化、新管理器恢复读写。`ShardedRedis_NodeRestart*` 两项测试在 net6.0/net8.0 各 2/2 通过；要求显式设置 `BING_TEST_RESTART_CONTAINER` 为 `bing-cache-inflight-` 开头的临时容器名，及原有集成/重启门控。两个任务专用 Redis 容器已清理。
- EasyCaching 客户端双分片新增显式门控的 10k/100k List/Stream 一致性测试：跨分片交错顺序、重复键、首/中/尾缺失位及 `Hit` 均逐项核对。`BING_TEST_EASYCACHING_SHARDED_LARGE_SCALE=10000|100000` 分别选择规模；两种规模在 net6.0/net8.0 均通过，测试专用双节点已清理。这是正确性证据，不替代性能预算采样。
- FreeRedis 与 CSRedis 各新增单节点 100k 专用测试，比较批量 List/Stream 的输入顺序、重复与缺失位、命中状态，以及前缀 List/Stream 的值和逻辑键集合；包含特殊字符与相似命名空间哨兵。`BING_TEST_LARGE_CACHE_DATASET=true` 门控，默认 100k，可用 `BING_TEST_CACHE_LARGE_COUNT` 调整。两个 Provider 的 100k 测试在 net6.0/net8.0 各 1/1 通过；net8.0 还分别通过 1k 冒烟测试。各轮临时容器已清理。
- EasyCaching 双分片新增第二批流式读取中途节点重启测试：第一批已产出并确认命中，第二批在 Redis 暂停处理时保持在途；容器重启后第二批以异常结束，旧管理器随后拒绝节点身份变化。该测试在 net6.0/net8.0 各 1/1 通过，使用与单键在途重启相同的专用容器门控；临时容器已清理。
- EasyCaching 同步 `SetAll` 新增真实节点重启边界测试：`batchSize=1` 时通过受控字典在第一批完成后暂停枚举，并确认首批物理键存在；第二批发出期间重启目标容器，验证方法没有返回成功且旧管理器拒绝变化后的节点。net6.0/net8.0 各 1/1 通过；专用容器已清理。
- EasyCaching 异步 `SetAllAsync` 同样新增首批成功后节点重启测试，net6.0/net8.0 各 1/1 通过；专用容器已清理。
- 在可从宿主机访问全部公告节点的隔离三主三从 Cluster 中，CSRedis 扫描去重改按 `CLUSTER MYID` 识别同一主节点的不同 endpoint 别名；同步和流式前缀扫描精准测试通过。FreeRedis 10k 测试揭示单键组 `MGET` 在缺失键时返回零长度数组，Cluster 单键组改用原生异步 `GET` 保留缺失位置，多键组仍使用同槽 `MGET`。
- FreeRedis/CSRedis 的跨槽 10k List/Stream 与前缀扫描一致性测试在 net6.0/net8.0 通过；两个 Provider 的 100k 同类测试在 net8.0 通过，耗时分别为 3 分 29 秒和 2 分 40 秒。测试覆盖重复键、缺失键、特殊字符与相似命名空间哨兵，隔离 Cluster 的六个容器和网络在验证后清理。
- 增加 Redis Cluster 迁移/导入标记、槽位缺口、重复归属和主节点断连的拓扑契约测试；`Bing.Caching.Tests` net6.0/net8.0 各 7/7 通过。FreeRedis/CSRedis 新增独立 `RUN_CACHE_CLUSTER_TRANSITION_TESTS` 门控，在专用三主三从 Cluster 构造旧管理器后触发副本 `TAKEOVER`；两个 Provider 的旧实例均显式拒绝扫描和清理，CSRedis 新实例确认完整数据仍可读取。两项目 net6.0/net8.0 精准测试各 1/1 通过，并经连续多次拓扑切换；专用容器和网络已清理。

### 前序轮次证据

- EasyCaching Redis/Hybrid 已支持 `RedisCluster`：批量读写和删除按槽分组，异步命令受 `MaxConcurrency` 限制；主节点扫描前后以 `CLUSTER NODES` 校验完整 16384 槽覆盖及端点清单。前序轮次对客户端分片采用 fail-fast，本轮已由专用分片入口替代。
- FreeRedis/CSRedis 增加特殊逻辑键流式扫描测试，涵盖花括号、星号、问号、反斜线、Unicode 和相似命名空间哨兵键；两个项目的精准测试均通过。
- Hybrid 增加两个独立实例的批量覆盖、删除和 Redis 总线失效测试；net6.0/net8.0 精准测试均通过。
- EasyCaching Redis/Hybrid Cluster 同步与异步跨槽批量、流式读取、前缀扫描、删除和命名空间清理测试在隔离三主三从 Cluster 中通过，net6.0/net8.0 各 2/2。EasyCaching 全量测试在隔离单节点与 Cluster 中 net6.0/net8.0 各 34/34 通过。
- 公共 `CacheClusterTopology` 增加 Redis hash slot 计算与主节点端点解析；已通过已知 CRC16 槽位和主副本拓扑单元测试。生产 netstandard2.0 构建通过。
- EasyCaching 单节点 10k 项批量 List/Stream 与前缀扫描 List/Stream 对照通过；特殊键流式扫描同时验证相似命名空间哨兵键未泄漏。
- FreeRedis/CSRedis 的 10k 项批量 List/Stream 一致性测试在 net6.0/net8.0 均通过；公共契约测试 net6.0/net8.0 各 108/108 通过；`Bing.All.sln --no-restore` 构建 0 错误、129 个既有警告；`git diff --check` 通过。

## 生产符号到测试方法

| 生产符号 | 测试项目与方法 |
| --- | --- |
| `CacheKeyFormat.GetPhysicalKeys` | Bing.Caching.Tests `PhysicalKeys_BatchMatchesSingleKeyFormat` |
| FreeRedis/CSRedis `GetAllStreamAsync` | 各自集成测试 `GetAllStream_LargeInputMatchesListIncludingDuplicatesAndMissingKeys`、`GetAllStream_PropagatesSecondBatchDeserializationFailureAfterFirstBatch` |
| FreeRedis/CSRedis `GetByPrefixStreamAsync` | 各自集成测试 `GetByPrefixStream_CancellationAfterFirstEntry_StopsFurtherConsumption`、`ThreeMasters_CrossSlotReadScanAndClear` |
| EasyCaching `GetAllStreamAsync(IEnumerable<CacheKey>)` | Bing.EasyCaching.Tests `GetAllStream_CacheKeys_EnumeratesInputOnlyAsBatchesAreConsumed`、`GetAllStream_CacheKeys_CancellationAfterFirstBatch_DoesNotDispatchNextBatch` |
| EasyCaching Redis/Hybrid 批量回填 | Bing.EasyCaching.Tests `RedisBatchReadAndStream_KeepOrderDuplicatesAndMissingConsistent`、`HybridBatchReadAndStream_KeepDistributedAndLocalValuesConsistent` |
| EasyCaching Cluster 批量与扫描 | Bing.EasyCaching.Tests `RedisCluster_CrossSlotBatchStreamRemoveAndClear_PreservesNamespaceIsolation`、`HybridCluster_CrossSlotBatchStreamRemoveAndClear_UpdatesDistributedAndLocalLayers` |
| EasyCaching 客户端分片配置拒绝 | Bing.EasyCaching.Tests `ClientShardedTopology_FailsBeforeProviderRegistration` |
| EasyCaching 客户端分片路由、批量流式和扫描 | Bing.EasyCaching.Tests `ShardedRedis_CrossNodeBatchStreamScanClearAndRetriever_Work`、`ShardedHybrid_CrossInstanceBatchStreamInvalidationAndClearConverge` |
| FreeRedis/CSRedis 取消后的命令派发 | 各自集成测试 `GetAllStream_CancellationDoesNotDispatchSecondMGet` |
| EasyCaching 分片 Hybrid 回源发布失败 | Bing.EasyCaching.Tests `ShardedHybrid_BusPublishFailurePropagatesAfterCommit` |
| EasyCaching Cluster 取消期间已派发命令处理 | Bing.EasyCaching.Tests `ClusterBatch_CancellationDuringDispatchWaitsForIssuedRead`、`ClusterBatch_CancellationDuringDispatchWaitsForIssuedWrite`、`ClusterBatch_CancellationDuringDispatchWaitsForIssuedRemove` |
| Hybrid 跨实例批量失效 | Bing.EasyCaching.Tests `HybridBatchOperations_PublishCrossInstanceInvalidationAndConverge` |
| FreeRedis/CSRedis 特殊键流式扫描 | 各自集成测试 `GetByPrefixStream_ReturnsSpecialLogicalKeys_AndExcludesSimilarNamespaceSentinel` |
| FreeRedis/CSRedis 10k 项 List/Stream 一致性 | 各自集成测试 `GetAllStream_10000ItemsMatchesListIncludingDuplicatesMissingOrderAndHits` |
| FreeRedis/CSRedis 有界批量 JSON 解码 | 各自集成测试 `GetAllStream_ReturnsKeyHitAndValue_InInputOrder`、`GetAllStream_10000ItemsMatchesListIncludingDuplicatesMissingOrderAndHits`、`GetAllStream_PropagatesSecondBatchDeserializationFailureAfterFirstBatch` |
| FreeRedis 自定义序列化逐项回退 | Bing.Caching.FreeRedis.Tests.Integration `GetAllStream_CustomSerializerPreservesPerItemProtocol` |
| EasyCaching 10k 项 List/Stream 一致性 | Bing.EasyCaching.Tests `RedisLargeBatch_ListAndStreamAgreeAcrossTenThousandKeys` |
| EasyCaching 特殊键流式扫描 | Bing.EasyCaching.Tests `RedisScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace` |
| FreeRedis/CSRedis 副本晋升后扫描 | 各自集成测试 `PromotedReplica_UpdatedTopologyReadsAndClearsAllSlots` |
| FreeRedis 单键物理键复用 | Bing.Caching.FreeRedis.Tests.Integration 原有 `RedisCacheManagerTest`，以及本轮完整单节点、双分片回归 |
| EasyCaching 分片节点身份复验 | Bing.EasyCaching.Tests `Get_FailsWhenRoutedShardRunIdChangesAfterConstruction`、`GetAllAsync_FailsWhenAnyShardRunIdChangesAfterConstruction`、`GetAllAsync_FailsWhenShardRunIdChangesDuringRead` |
| EasyCaching 单键在途节点身份复验 | Bing.EasyCaching.Tests `Get_FailsWhenShardRunIdChangesDuringRead`、`GetAsync_FailsWhenShardRunIdChangesDuringRead`、`Set_FailsWhenShardRunIdChangesDuringWrite`、`RemoveAsync_FailsWhenShardRunIdChangesDuringDelete` |
| Hybrid 批量发布失败报告 | Bing.EasyCaching.Tests `HybridBatchBusFailure_SetAllPropagatesAfterLayerOperations`、`HybridBatchBusFailure_RemoveAllPropagatesAfterLayerOperations` |
| Hybrid 手工构造总线要求 | Bing.EasyCaching.Tests `HybridManager_RejectsUntrackedBus`、`HybridRetriever_BusFailureReportsCommittedValueAndClearsLocal` |
| EasyCaching 分片节点重启后身份拒绝与重建恢复 | Bing.EasyCaching.Tests `ShardedRedis_NodeRestartChangesIdentity_OldManagerRejectsAndRebuiltManagerRecovers` |
| EasyCaching 分片在途读取节点重启 | Bing.EasyCaching.Tests `ShardedRedis_NodeRestartsDuringRead_InFlightReadDoesNotReturnSuccess` |
| EasyCaching 分片流式部分成功后节点重启 | Bing.EasyCaching.Tests `ShardedRedis_NodeRestartsDuringSecondBatch_StreamReportsPartialFailure` |
| EasyCaching 分片同步批量写入期间节点重启 | Bing.EasyCaching.Tests `ShardedRedis_NodeRestartsDuringSyncSetAll_ReportsPartialFailure` |
| EasyCaching 分片异步批量写入期间节点重启 | Bing.EasyCaching.Tests `ShardedRedis_NodeRestartsDuringAsyncSetAll_ReportsPartialFailure` |
| CSRedis Cluster 节点别名去重 | Bing.Caching.CSRedis.Tests.Integration `ThreeMasters_CrossSlotReadScanAndClear`、`TenThousandItems_CrossSlotListAndStreamRemainConsistent` |
| FreeRedis Cluster 单键组缺失位置 | Bing.Caching.FreeRedis.Tests.Integration `TenThousandItems_CrossSlotListAndStreamRemainConsistent` |
| FreeRedis/CSRedis Cluster 10k/100k List/Stream 与扫描一致性 | 各自集成测试 `TenThousandItems_CrossSlotListAndStreamRemainConsistent`、`OneHundredThousandItems_CrossSlotListAndStreamRemainConsistent` |
| Redis Cluster 迁移标记、槽位缺口/重叠与断连 | Bing.Caching.Tests `GetMasterIds_AllowsStableMigrationAndImportAnnotations`、`GetMasterIds_RejectsSlotGapWithDiagnostic`、`GetMasterIds_RejectsDuplicateSlotWithDiagnostic`、`GetMasterIds_RejectsDisconnectedMasterWithDiagnostic` |
| FreeRedis 旧扫描客户端在副本晋升后显式失败 | Bing.Caching.FreeRedis.Tests.Integration `StaleScanClients_RejectScanAndClearAfterReplicaTakeover` |
| CSRedis 旧扫描客户端在副本晋升后显式失败、新拓扑可完整读取 | Bing.Caching.CSRedis.Tests.Integration `TopologyChangeAfterConstruction_RejectsOldScanAndClearWithoutPartialSuccess` |
| EasyCaching 双分片大规模批量一致性 | Bing.EasyCaching.Tests `ShardedRedis_10000ItemListAndStreamRemainConsistentAcrossShards`、`ShardedRedis_100000ItemListAndStreamRemainConsistentAcrossShards` |
| FreeRedis/CSRedis 单节点 100k 批量与前缀一致性 | 各自集成测试 `GetAllAndGetByPrefix_100000Items_ListAndStreamRemainConsistent` |
| FreeRedis/CSRedis 大规模前缀 List/Stream 一致性 | 各自集成测试 `GetByPrefix_10000Items_ListAndStreamRemainConsistentAcrossSpecialKeysAndNamespaces` |
| Hybrid 总线运行期断线、故障期发布与恢复 | Bing.EasyCaching.Tests `HybridBus_SubscriptionDisconnect_ReconnectsAndInvalidatesPeer`、`HybridBus_PublishDuringPause_ReportsFailureAndRecovers` |
| Hybrid 总线连续断线、暂停、恢复 | Bing.EasyCaching.Tests `HybridBus_SubscriptionAndPauseFailures_ReconnectThenRepublishAndInvalidatePeer` |
| 客户端分片 Hybrid 总线暂停与目标分片重启重叠 | Bing.EasyCaching.Tests `ShardedHybrid_BusPauseOverlapsShardRestart_RejectsPendingUpdateAndRecovers` |
| 客户端双分片同时重启后的旧实例拒绝与重建恢复 | Bing.EasyCaching.Tests `ShardedRedis_BothNodesRestartTogether_OldManagerFailsAndRebuiltManagerRecovers` |
| 客户端单分片连续两次重启后的逐代身份拒绝与恢复 | Bing.EasyCaching.Tests `ShardedRedis_SameNodeRestartsTwice_EachOldManagerFailsBeforeNextRebuild` |
| 客户端双分片重启与 Hybrid 总线暂停重叠后的失败与跨实例失效恢复 | Bing.EasyCaching.Tests `ShardedHybrid_BusPauseOverlapsBothShardRestarts_RejectsPendingUpdateAndRecovers` |
| FreeRedis Cluster SCAN 已执行但响应丢失后的流式错误传播 | Bing.Caching.FreeRedis.Tests.Integration `ScanResponseGate_HoldsCompletedScanUntilConnectionIsAborted`、`ClusterScan_ResponseLostAfterExecution_PropagatesFailure` |
| CSRedis Cluster SCAN 已执行但响应丢失后的流式错误传播 | Bing.Caching.CSRedis.Tests.Integration `ScanResponseGate_HoldsCompletedScanUntilConnectionIsAborted`、`ClusterScan_ResponseLostAfterExecution_PropagatesFailure` |
| EasyCaching Redis 单节点 SCAN 已执行但响应丢失后的流式错误传播 | Bing.EasyCaching.Tests `RedisScan_ResponseLostAfterExecution_PropagatesFailure` |
| EasyCaching Redis Cluster SCAN 已执行但响应丢失后的流式错误传播 | Bing.EasyCaching.Tests `RedisClusterScan_ResponseLostAfterExecution_PropagatesFailure` |
| CacheClusterTopology.GetLocalMasterId、EnsureSameView | Bing.Caching.Tests `GetLocalMasterId_ReadsMyselfMasterRegardlessOfRowOrder`、`GetLocalMasterId_RejectsMissingDuplicateSlaveAndDisconnectedMyself`、`EnsureSameView_RejectsDivergentMasterViews` |
| CacheKeyFormat.TryGetLogicalKey 规范物理键校验与分配 | Bing.Caching.Tests `TryGetLogicalKey_RequiresCanonicalHashTagAndEscaping`、`TryGetLogicalKey_RejectsSimilarNamespaceAndLeaseKeys`；Bing.Caching.FreeRedis.Tests.Integration 与 Bing.Caching.CSRedis.Tests.Integration 各自 `CacheSpecialKeyTest`；Bing.EasyCaching.Tests `RedisScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace` |
| FreeRedis/CSRedis/EasyCaching Cluster 起止扫描拓扑校验 | Bing.Caching.FreeRedis.Tests.Integration `ThreeMasters_CrossSlotReadScanAndClear`、`SlotOwnershipChangeDuringStreamingScan_IsRejectedWithStableMasterIds`；Bing.Caching.CSRedis.Tests.Integration `ThreeMasters_CrossSlotReadScanAndClear`、`SlotOwnerChangesDuringScan_RejectsCompletedStream`；Bing.EasyCaching.Tests `RedisCluster_CrossSlotBatchStreamRemoveAndClear_PreservesNamespaceIsolation`、`RedisCluster_SlotOwnershipChangeDuringPrefixStreamIsRejected` |
| EasyCaching Cluster 主节点地址别名去重与扫描清理 | Bing.EasyCaching.Tests `RedisCluster_CrossSlotBatchStreamRemoveAndClear_PreservesNamespaceIsolation`、`HybridCluster_CrossSlotBatchStreamRemoveAndClear_UpdatesDistributedAndLocalLayers` |
| EasyCaching 单节点扫描分支回归 | Bing.EasyCaching.Tests `RedisScan_HandlesSpecialKeysWithoutTouchingAnotherNamespace` |
| FreeRedis Cluster 迁移期 ASK 批量读取与扫描 | Bing.Caching.FreeRedis.Tests.Integration `MigratingSlot_BatchReadAndScanPreserveExistingValuesOrPropagateRedirectFailure` |
| CSRedis Cluster 迁移期 ASK 批量读取与扫描 | Bing.Caching.CSRedis.Tests.Integration `AskRedirect_DoesNotReportMigratedKeyAsCacheMiss` |
| FreeRedis Cluster 槽位换主 MOVED 批量读取与扫描 | Bing.Caching.FreeRedis.Tests.Integration `MovedSlot_BatchReadAndScanPreserveValueOrPropagateRedirectFailure` |
| CSRedis Cluster 槽位换主 MOVED 批量读取与扫描 | Bing.Caching.CSRedis.Tests.Integration `MovedRedirect_DoesNotReportNewOwnerValueAsCacheMiss` |
| `CacheClusterTopology.GetSlotOwnershipSnapshot` / `EnsureUnchanged` | Bing.Caching.Tests `SlotOwnershipSnapshot_DetectsSameMasterSetMigration` |
| FreeRedis 扫描期间槽位换主拒绝完整成功 | Bing.Caching.FreeRedis.Tests.Integration `SlotOwnershipChangeDuringStreamingScan_IsRejectedWithStableMasterIds` |
| CSRedis 扫描期间槽位换主拒绝完整成功 | Bing.Caching.CSRedis.Tests.Integration `SlotOwnerChangesDuringScan_RejectsCompletedStream` |
| EasyCaching 扫描期间槽位换主拒绝完整成功 | Bing.EasyCaching.Tests `RedisCluster_SlotOwnershipChangeDuringPrefixStreamIsRejected` |

## 本轮进展（2026-10-04）

- 基准探针现可构造 EasyCaching Redis/Hybrid 的双节点客户端分片和三主三从 Cluster；四种拓扑各完成 100 项、1 轮预热、1 轮样本的冒烟，原始 JSON 位于 `BING-CACHE-PERF-STREAM-20261004-002/bench/smoke-current-easy*-100.json`。这些样本不用于性能预算判定。
- Cluster 冒烟发现 EasyCaching 将客户端 `127.0.0.1` 种子地址与 Redis 宣告的 `192.168.0.14` 主节点地址直接比较，误报扫描拓扑不完整。现按 `CLUSTER MYID` 识别并去重已连接主节点，扫描复用已校验列表；开始和结束仍检查全部 16384 槽位与主节点身份。
- 在隔离三主三从环境中，EasyCaching Redis/Hybrid Cluster 集成测试于 net6.0/net8.0 各 2/2 通过；单节点特殊键扫描精准测试 net8.0 1/1 通过；EasyCaching 完整测试 net6.0/net8.0 各 48 通过、14 项独立环境门控跳过；生产 netstandard2.0 构建及 `git diff --check` 通过。隔离 Redis 容器和 Cluster 网络已清理。

## 续做进展（2026-10-05）

- 重新建立独立三主三从 Redis Cluster（16384/16384 槽覆盖），FreeRedis 与 CSRedis 的 `OneHundredThousandItems_CrossSlotListAndStreamRemainConsistent` 在 net6.0 均 1/1 通过；此项现有 net8.0 证据，补齐了两种运行时。此次只验证功能一致性，不把测试耗时当作性能 P95。
- FreeRedis/CSRedis 新增独立门控 `RUN_CACHE_CLUSTER_MIGRATION_TESTS` 的真实 `ASK` 测试：将物理键从源主节点迁至导入节点，确认源节点返回 `ASK`；批量读取和前缀扫描只能返回完整正确值或显式传播重定向失败。两个项目 net6.0/net8.0 各 1/1 通过。测试后 `redis-cli --cluster check` 确认 0 个键、无开放槽位、16384 槽完整覆盖。
- 同一独立门控下新增真实 `MOVED` 测试：先构造旧缓存，再将一个空槽的所有权同步转移至另一个主节点，在新主写入值；源节点必须返回 `MOVED`，缓存批量读取及前缀扫描不得将其伪装为未命中。FreeRedis/CSRedis net6.0/net8.0 各 1/1 通过；每轮清理后再次确认 0 个键、无开放槽位及 16384 槽完整覆盖。
- 两个 Provider 的 `ClusterMigrationTest` 在 net8.0 按类连续执行均 2/2 通过。专用 Redis 容器和网络已清理；本轮无生产代码改动，未重复运行性能预算矩阵。

## 续做进展：扫描期间槽位迁移（2026-10-05）

- Change Impact Analysis：改动公共 `CacheClusterTopology`、FreeRedis/CSRedis/EasyCaching 的 Cluster 扫描起止校验，以及三类 Provider 的独立迁移测试；单节点和分片的键格式、批量读写及租约路径未改。风险为中：Cluster 扫描在槽位归属变化时新增显式异常，已有返回列表的清理/读取仍可能在异常前完成部分命令。
- 发现原校验仅比对在线主节点 ID：同一批主节点间迁移空槽时，ID 集合不变，扫描可能把跨拓扑结果当成完整成功。新增规范化槽位归属快照，允许节点行顺序、范围拆分和迁移标记变化，但拒绝实际槽位换主；三个 Provider 的同步、异步、流式扫描在结束时复验快照。
- 在专用三主三从 Redis Cluster 上，FreeRedis `SlotOwnershipChangeDuringStreamingScan_IsRejectedWithStableMasterIds`、CSRedis `SlotOwnerChangesDuringScan_RejectsCompletedStream`、EasyCaching `RedisCluster_SlotOwnershipChangeDuringPrefixStreamIsRejected` 均在 net6.0/net8.0 各 1/1 通过。测试在流式首项后将空槽转给现有另一主节点，随后消费必须抛出槽位归属变化异常；三者均恢复槽位并清理测试键。
- FreeRedis/CSRedis 正常 `ThreeMasters_CrossSlotReadScanAndClear` 各 1/1 通过；EasyCaching Redis/Hybrid 正常跨槽扫描清理 net8.0 各 1/1 通过。`Bing.Caching.Tests` net6.0/net8.0 各 113/113 通过；三个生产项目 netstandard2.0 构建通过，`git diff --check` 通过。
- `redis-cli --cluster check` 确认测试后 3 个主节点均 0 键、无开放槽位、16384 槽完整覆盖。此变更只影响 Cluster 扫描校验，既有单节点性能预算证据未失效；`FIX-007` 没有新的结构性性能候选，本轮不重复采样。

## 续做进展：Hybrid 连续故障恢复（2026-10-05）

- Change Impact Analysis：仅新增 `Bing.EasyCaching.Tests` 的专用集成测试及执行记录；生产符号、公共契约、Benchmark Harness 和目标框架未变化，风险低。既有性能与 Provider 功能证据可复用。
- 新增 `HybridBus_SubscriptionAndPauseFailures_ReconnectThenRepublishAndInvalidatePeer`，独立门控要求 `RUN_INTEGRATION_TESTS=true`、不同的数据/总线 Redis 端点、`BING_TEST_ALLOW_REDIS_CLIENT_KILL=true` 和 `BING_TEST_ALLOW_REDIS_PAUSE=true`。同一双实例场景先断开并恢复两条订阅连接，验证失效通知；再暂停总线使写入已提交但发布抛错，恢复后重新发布并确认对端本地层失效和最终值。
- 专用本机数据节点 `127.0.0.1:52781` 与总线节点 `127.0.0.1:52782` 上，net6.0/net8.0 精准测试各 1/1 通过；测试后数据节点 0 键、总线 `PONG`。未修改生产热路径，不重复性能采样。

## 续做进展：分片与总线重叠故障（2026-10-05）

- Change Impact Analysis：仅新增 `Bing.EasyCaching.Tests` 独立门控的集成测试及本记录；生产代码、公共 API、目标框架与基准探针未变化，风险低。使用两个本机数据 Redis 分片和第三个独立总线 Redis，测试仅重启 `bing-cache-combined-` 前缀且端口核对通过的临时容器。
- `ShardedHybrid_BusPauseOverlapsShardRestart_RejectsPendingUpdateAndRecovers` 在总线暂停后发起分片 0 更新，直接观察分布式层已提交且发布仍在途，再于暂停窗口内重启目标分片。断言在途操作不得伪成功、旧管理器拒绝变化后的节点身份；总线恢复并重建 Provider 后，使用同分片的独立恢复键证明新发布使旧对端本地层失效，避免被暂停期间排队的旧发布污染断言。
- 专用端点 `127.0.0.1:52791`、`:52792`、`:52793` 上 net6.0/net8.0 精准测试各 1/1 通过；两个数据分片结束时均 0 键，总线 `PONG`，`git diff --check` 通过。当前改动不触及性能热路径，复用既有 FIX-007 预算失败证据。
- FIX-007 原始样本进一步核算：1k 项 CSRedis 当前批读平均分配约 2.41 MB，固定旧版约 0.91 MB；已分离的物理键生成约 0.98 MB，客户端原始 MGET 约 1.27 MB。这些诊断阶段不能直接相加为严格下限，但足以排除只删除兼容 List 路径的一层结果数组就达到 30% 分配预算的假设；没有新的完整候选前不重复正式性能采样。

## 续做进展：双分片同时重启（2026-10-05）

- Change Impact Analysis：仅新增 `Bing.EasyCaching.Tests` 的门控故障测试及本报告；生产路径、公共契约、目标框架与性能探针未变。风险为中：只对显式指定且通过容器名、运行状态和回环端口校验的两个本轮临时 Redis 容器执行重启。
- `ShardedRedis_BothNodesRestartTogether_OldManagerFailsAndRebuiltManagerRecovers` 并发重启两个独立客户端分片，分别确认 `run_id` 改变；旧管理器对每个分片的单键读取和跨分片批量读取都显式报告身份变化，重建管理器可在两个分片读写。
- 本机隔离端点 `127.0.0.1:52794`、`:52795` 上，net8.0/net6.0 精准测试各 1/1 通过。测试后两个分片 `DBSIZE` 均为 0。仅测试代码改变，复用既有 FIX-007 性能预算证据，不重复性能采样。

## 续做进展：单分片连续重启（2026-10-05）

- Change Impact Analysis：仅扩展 `Bing.EasyCaching.Tests` 中独立门控的故障测试及本报告；生产代码、公共 API、目标框架和性能探针不变。仅对通过名称、运行状态及回环端口校验的任务专用 Redis 容器执行两次顺序重启。
- `ShardedRedis_SameNodeRestartsTwice_EachOldManagerFailsBeforeNextRebuild` 在每次重启后确认新的 `run_id`：第一代管理器失效，第二代重建后可读写；第二次重启使第二代管理器失效，第三代重建后可读写。两个节点端点独立，未清空整库。
- 隔离端点 `127.0.0.1:52794`、`:52795` 上，net8.0/net6.0 精准测试各 1/1 通过。两个分片在测试后 `DBSIZE` 均为 0；既有 FIX-007 性能结果未因测试代码改变而失效。

## 续做进展：双分片与总线重叠故障（2026-10-05）

- Change Impact Analysis：仅新增 `Bing.EasyCaching.Tests` 的显式门控集成测试及本报告；生产代码、公共契约、目标框架和性能探针未变。风险为中：测试只暂停本轮独立总线，并在容器名称、运行状态和本机回环端口校验后重启两个临时数据分片。
- `ShardedHybrid_BusPauseOverlapsBothShardRestarts_RejectsPendingUpdateAndRecovers` 先在两个分片建立双实例本地缓存，再暂停总线；确认分片 0 的更新已提交而发布仍在途时，并发重启两个分片。测试确认在途更新不会伪成功，旧管理器对两个分片均拒绝已变化的节点身份。总线恢复后重建两个 Hybrid Provider，使用两个与旧发布不同的恢复键验证两侧新的跨实例本地失效通知。
- 独立端点 `127.0.0.1:52796`、`:52797`、`:52798` 上，net8.0/net6.0 精准测试各 1/1 通过；测试后两个数据分片均 0 键，总线 `PONG`。仅测试代码变化，复用既有 FIX-007 性能预算证据。

## 续做进展：FreeRedis SCAN 响应在途故障（2026-10-05）

- Change Impact Analysis：仅新增 FreeRedis 集成测试、测试专用 RESP 代理及本地 Cluster 启动脚本；生产代码、公共契约、目标框架和性能探针未变。风险为中：代理只监听本机回环端口，Cluster 仅使用本轮可丢弃容器且数据端口只发布到本机回环地址。
- `RedisScanResponseGate` 对请求和响应做完整 RESP 分帧，识别首个 `SCAN`，待上游完成响应后暂存，不向客户端转发。单节点 `ScanResponseGate_HoldsCompletedScanUntilConnectionIsAborted` 证明此时调用仍等待；断开代理连接后异常传播。net8.0/net6.0 各 1/1 通过。测试驱动把 FreeRedis `ScanAsync` 放入独立任务，因为该库在返回 Task 前可能同步等待响应；生产异步路径未变。
- 六个 Redis 进程置于同一个本轮临时容器、各用独立端口与配置文件，构成三主三从 Cluster。容器内和宿主机均使用 `127.0.0.1:7000-7005`，解决 Docker Desktop 的双侧公告地址不一致；启动脚本为 `redis-cluster-local.sh`。`ClusterScan_ResponseLostAfterExecution_PropagatesFailure` 在完整 `SCAN` 响应已由代理收到、流式调用仍等待时断连，net8.0/net6.0 各 1/1 通过，未把响应丢失误记为完整扫描成功。
- `redis-cli --cluster check` 验证 16384 槽完整覆盖、三主各有一副本且测试后主节点均 0 键；`git diff --check` 通过。这是 FreeRedis 的“请求已发送、Redis 已执行、响应未交付”窗口，不证明 Redis 服务器 CPU 正在执行 SCAN 的瞬间；CSRedis 和 EasyCaching 相同窗口尚待各自验证。

## 续做进展：CSRedis 与 EasyCaching SCAN 响应在途故障（2026-10-05）

- Change Impact Analysis：只变更共享测试辅助工具、CSRedis/EasyCaching 集成测试及 EasyCaching 测试项目引用；生产代码、公共契约、目标框架和 Benchmark Harness 不变。共享测试工具迁移影响 FreeRedis，故重新运行其精准 Cluster 用例。既有性能证据可复用。
- `RedisScanResponseGate` 移入 `Bing.Test.Shared`，供三个 Provider 测试复用。CSRedis Cluster 客户端会自动发现额外节点，测试将三个扫描客户端分别固定到指定主节点，确保第一主节点的 `SCAN` 确实经过代理；测试同时等待扫描完成与代理截获，提前完成不能误判通过。
- CSRedis `ClusterScan_ResponseLostAfterExecution_PropagatesFailure` 在独立三主三从 Cluster 上 net8.0/net6.0 均通过；单节点代理自检 net8.0 通过。FreeRedis 共享工具迁移后 Cluster 精准回归 net8.0/net6.0 均通过。EasyCaching Redis 单节点 `RedisScan_ResponseLostAfterExecution_PropagatesFailure` net8.0/net6.0 均通过；Cluster 拓扑发现可能绕过单一代理，尚未获得同等故障窗口证据。
- Cluster 检查确认 16384 槽完整覆盖、三个主节点均 0 键；单节点 Redis `DBSIZE=0`。这些用例只证明完整 `SCAN` 响应由代理截获后断连时的异常传播，不证明 Redis CPU 正在执行命令瞬间的故障。

## 续做进展：EasyCaching Cluster SCAN 响应在途故障（2026-10-05）

- Change Impact Analysis：仅扩展 `Bing.EasyCaching.Tests` 的显式门控集成测试与本记录；生产代码、公共契约、目标框架、Benchmark Harness 未变化。测试只在 `RUN_INTEGRATION_TESTS=true`、`RUN_CACHE_CLUSTER_SCAN_INFLIGHT_TESTS=true`、`BING_TEST_ALLOW_CLUSTER_ANNOUNCE_CHANGE=true` 同时设置时运行。
- 专用三主三从 Cluster 中，先写入 120 个跨槽键，再将第一个主节点的 `cluster-announce-port` 临时改为本机 RESP 代理端口。确认其他主节点也公告代理端口，且新建 EasyCaching Multiplexer 发现的是代理而非原始 7000 端口；因此生产扫描路径的首个主节点 `SCAN` 必须经过代理。
- `RedisClusterScan_ResponseLostAfterExecution_PropagatesFailure` 在完整 `SCAN` 响应被暂存、流式任务仍未完成时断连，net8.0/net6.0 各 1/1 通过。`finally` 恢复公告端口并删除精确测试键；net8.0 后核对公告端口 7000、16384 槽完整、三个主节点均 0 键。该证据覆盖响应在途丢失，不覆盖 Redis CPU 正在执行命令的瞬间。

## 续做进展：Cluster 主节点视图分歧（2026-10-05）

- Change Impact Analysis：修改共享 `CacheClusterTopology` 解析与 FreeRedis、CSRedis、EasyCaching 的 Cluster 扫描起止校验；单节点/客户端分片和批量键读取未变。风险为中：新增保守失败条件，视图分歧时扫描显式抛错；Cluster 扫描需解析每个主节点的视图，正式 P95 与分配影响尚未量测。
- 在专用三主三从 Cluster 中，空槽由主节点 A 迁到 B 再迁回 A 后，三个 `CLUSTER SETSLOT` 均返回 `OK`，但曾观察到 A 的 `CLUSTER NODES` 仍认为 B 持有该槽，B/C 则认为 A 持有。三个主节点的 config epoch 在第一次迁移后未改变，说明仅比较一个节点的起止槽位快照或配置纪元不足以确认全局一致。对 A 执行 `CLUSTER BUMPEPOCH` 并重新设置槽位后，`redis-cli --cluster check` 恢复一致，16384 槽完整且 0 键。
- 三个 Provider 现对每个在线主节点分别获取 `CLUSTER NODES`，从其 `myself,master` 行读取身份，并比较规范化槽位归属；不再为同一节点另发 `CLUSTER MYID`。共享解析精准测试 net6.0/net8.0 各 11/11 通过；三个 Provider 正常 Cluster 扫描与单次槽位变更回归在 net6.0/net8.0 各项目 2/2 通过；生产 netstandard2.0 构建均通过。响应在途故障回归 net8.0 三个 Provider 各 1/1 通过，EasyCaching net6.0 也通过。
- 本保证限于扫描起止校验时观察到的视图。若拓扑在校验间发生 A→B→A 且所有节点在结束前完全收敛，当前 Redis 响应不能证明这段历史；严格 ABA 检测仍待可观测的单调事件源或明确产品边界。现有固定单节点性能预算证据不因 Cluster 专用校验变更而失效，Cluster 扫描新增解析开销尚无正式对照。

## 续做进展：Cluster 扫描当前版性能探针（2026-10-05）

- Change Impact Analysis：三类 Provider 的 Cluster 扫描校验从单节点视图改为逐主节点视图比较，可能增加解析与网络开销；本轮只对该路径做当前版诊断采样，不把不同时间的旧诊断数据当作正式同环境对照。
- 在本任务隔离三主三从 Cluster 上，四类 Provider 均按 1,000 项、2 轮预热、10 轮样本依次运行。原始结果为 `cluster-view-current-freeredis-1k.json`、`cluster-view-current-csredis-1k.json`、`cluster-view-current-easyredis-1k.json`、`cluster-view-current-easyhybrid-1k.json`；各阶段保存 P50/P95/P99、分配量、Redis 命令数和原始样本。
- List 前缀扫描的 P95/平均分配量分别为 FreeRedis 390.95 ms / 13.14 MB、CSRedis 351.12 ms / 10.85 MB、EasyCaching Redis 38.35 ms / 4.70 MB、EasyCaching Hybrid 40.86 ms / 4.99 MB。流式扫描 P95 分别为 464.66、348.91、40.75、43.04 ms。不同客户端的绝对值不代表同一实现的优化幅度；本次无同步旧版 Cluster 基线，不判定 20% P95 或 30% 分配预算通过。

## 续做进展：物理键解析分配优化（2026-10-05）

- Change Impact Analysis：共享 `CacheKeyFormat.TryGetLogicalKey` 被 FreeRedis、CSRedis、EasyCaching 的扫描过滤复用；公共签名、物理键格式、目标框架和写入路径不变。风险为中：错误接收伪造键或错误拒绝合法键会影响命名空间隔离及扫描完整性。
- 解析现在先校验规范 URI 编码，再逐字符比较原有 SHA-256 hash tag，避免为每个扫描键重新生成完整物理键。合法键、hash tag 篡改、非规范百分号编码、相似命名空间及租约后缀已有直接测试。`Bing.Caching.Tests` net6.0/net8.0 各 118/118 通过；FreeRedis、CSRedis 的特殊键集成测试在两个目标框架各 3/3 通过，EasyCaching 特殊键扫描各 1/1 通过；生产 netstandard2.0 构建通过。
- 独立 Redis 7.4 `127.0.0.1:52799` 上运行 CSRedis 当前版 1k、2 轮预热、10 轮样本的分配探针，原始结果为 `keyparse-candidate-csredis-1k.json`。`keyParse` 平均分配 0.82 MB、P95 0.55 ms；完整扫描平均分配 2.71 MB、P95 13.20 ms。旧诊断文件的 `keyParse` 为约 1.51 MB，但样本时间、Redis 容器和候选实现不完全相同，不能把差值作为正式 before/after。完整扫描仍超过旧版分配预算；本次优化不关闭 `FIX-007`。

## 续做进展：物理键解析候选正式预算确认（2026-10-05）

- Change Impact Analysis：生产与探针源码未再修改；本轮只生成固定旧版和当前版的同环境性能证据，影响 `FIX-007` 的预算判定，不使既有正确性测试失效。
- 重新建立固定提交 `24c93cb1a5355797223637e2d42ac3dce968d7ef` 的 detached 基线工作树，并在本任务独立 Redis 7.4 `127.0.0.1:52800` 串行运行 CSRedis、FreeRedis 的旧版/当前版。两规模均 2 轮预热、10 轮采样，.NET 8.0.30、端点及探针一致，四份原始文件为 `keyparse-formal-{baseline,current}-{csredis,freeredis}-1k10k.json`；所有 `rawSamples` 数量已核对。Redis 测试后 `DBSIZE=0`。
- 与固定旧版相比，CSRedis 1k/10k 批读 P95 回退 +10.1%/+41.7%、扫描 +12.7%/+23.6%、批读分配 +162.3%/+182.1%、扫描分配 +116.8%/+126.4%；FreeRedis 对应批读 P95 +37.9%/+39.3%、扫描 +34.0%/-41.4%、批读分配 +68.9%/+73.1%、扫描分配 +71.4%/+73.5%。命中读 P95 四组均低于旧版。完整表和候选哈希见 `../BING-CACHE-PERF-STREAM-20261004-002/performance.md`。
- 结论：当前候选在两 Provider 的全部分配预算上失败，多个 P95 项也失败；`FIX-007` 保持开放。键解析假设已完成诊断和正式确认，不再重复同类运行。

## 剩余 TODO

1. `FIX-007` 仍开放：有界批量 JSON 解析和物理键解析优化后，同环境正式 1k/10k 对照中两 Provider 的全部分配项仍超过 30% 预算，多个 P95 项也超过预算；需要新的结构性根因与候选，不能放宽阈值或重复键解析假设。
2. Cluster 正常跨槽、主节点缺失报错、副本晋升、迁移期 `ASK/MOVED` 和扫描首项后同主节点集合内槽位换主均有实机证据；FreeRedis、CSRedis、EasyCaching 均已补 `SCAN` 执行完毕但响应仍在途时断连的实机测试。起止校验现比较全部主节点的槽位视图；完全收敛的瞬态 ABA、Redis 服务器执行命令的瞬间仍缺可验证的强保证。Cluster 新校验已做四类 Provider 的 1k 当前版采样，缺同环境旧版对照，预算仍未判定。
3. 双分片 10k/100k 正确性测试已通过；EasyCaching 双分片和 Cluster 的 Redis/Hybrid 100 项基准冒烟通过，100k 性能采样及正式多拓扑矩阵仍未运行。当前预算已失败，高成本重复矩阵暂缓。
4. 网络往返数、Provider 内部实际并发尚无可靠客户端观测钩子；中间版只留下旧 JSON 和不同方法学探针，缺可重建源码快照，严格三方同构基线当前不可验证。已完成固定旧版与最新候选的同环境正式两方对照，不用旧 v2 数字伪造第三方比例。
