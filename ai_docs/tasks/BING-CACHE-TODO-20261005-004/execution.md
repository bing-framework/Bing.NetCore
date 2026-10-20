<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-CACHE-TODO-20261005-004
AI_EXECUTION_FINISHED_AT: 2026-10-05T02:00:48Z

# 执行报告

最终独立审查：PASS_WITH_ISSUES；IMPLEMENTATION_STATUS=PASS、TEST_STATUS=PASS；PERFORMANCE_EVIDENCE_STATUS=COMPLETE_WITH_BOUNDARIES_BUDGET_FAILED；RELEASE_STATUS=BLOCKED；OPEN_ACTIONABLE=0。详见 review.md。本轮执行已停止，剩余性能预算、外部证据与延后能力保留在 TODO.md。

用户授权继续实现上一轮 TODO。已读取 GOAL_RULES、execute-plan、根目录 AGENTS，保护所有原有未提交修改。原计划与历史证据保留，本任务针对最新审查清单推进。Review 修复轮开始时间为 2026-10-05T00:15:03.737Z。

## Change Impact Analysis

ChangedProjects: Bing.Caching、FreeRedis、CSRedis、EasyCaching 及其测试；ChangedPublicContracts: 仅必要可选资源配置；ChangedRuntimePaths: Hybrid回填、回源超时/清理、普通写删与租约、批量反序列化；ChangedProviders: 三个 Redis 适配与 Easy 内存；ChangedTFMs: netstandard2.0 / net6.0 / net8.0；ChangedBenchmarkHarness: 按实际记录；AffectedDependents: admin 与缓存消费者；RiskLevel: HIGH。

## 进度

T01-T08 核心实现已完成（T07 按字节自适应拆分保留明确偏差）；T09 正式性能证据采集结束，预算未通过；T10 独立复审与最终门禁报告已收口。性能报告、序列化与扫描取消专项委派给 luna_worker，架构与集成由主代理负责。

2026-10-05 第一轮：
- T01：值/PTTL 同槽原子读取、本地回填扣除耗时、缺失值移除本地层、Hybrid 注册禁用随机 TTL 延长；500ms/1500ms 精准实机测试通过。
- T02：共享回源取消与任务异常观察、清理错误附加到原异常 Data；清理等待有界；协调时间参数验证上界。公共 helper 精准 6/6、Easy 原异常/释放失败精准 1/1。
- T03：FreeRedis、CSRedis、Easy Redis/Hybrid 显式 Set/Remove/批量写删撤销同槽租约；实机暂停工厂后写删的 6 条路径及超时晚结果测试通过。
- T04：仅无全局设置的标量 JSON 共用数组解析；对象/引用/自定义转换器逐值解码。CSRedis 同步批读和单读使用同一个 JSON 解码入口。Easy Redis/Hybrid 首批产出后第二批坏 JSON 精准通过。
- T05：实机证明底层 Subscribe onConnect 不覆盖自动重连，因此跟踪底层已注册 subscriber 的 ConnectionFailed/Restored Subscription 事件，清空本地 Provider；不另建连接/总线，DI Dispose 退订事件。无补发的断线更新/删除精准通过。
- T06：Easy Cluster Redis/Hybrid 10k 功能对照 net8.0 各通过；100k 两用例均通过（约 2m27s），两次规模均为 EXPECTED_SUCCESS，session 观察窗口 600s。不以其耗时代替性能基准。真实 MONITOR 验证 Free/CS/Easy Redis/Hybrid 首批产出后取消不再派发 SCAN，4 条路径均通过。
- T07：共享 CacheResourceOptions 的准入预算默认 8 在途/128 等待；单原始响应批次预算 8MiB，响应收到后、解码前校验，无法限制底层客户端已接收的原始缓冲。公共边界测试 3/3。
- T08：命中读 P95 表已更正并记录累计工作集口径，旧原始 JSON 未改。

环境：本轮新建专用 redis:7.4 容器 bing-cache-todo004-data(:52800)、bus(:52801)、cluster(:7000-7005，三主三从)；现有 :6379 Redis 未访问。Cluster 建立后 16384 槽完整。
任务登记首次在受保护 .agents/runtime 写入失败，使用批准的最小脚本升级登记成功。Windows 映射文件不允许原地覆盖时采用同目录 UTF-8 临时文件替换。无源码丢失。

## Git 安全

不自动 commit、push、PR 或发布。

## Review 修复记录 — Round 1

独立 review.md 记录第一轮候选的 NEEDS_FIX；fixScope=recommended。当前修复与该审查分离，未由 Executor 修改审查结论。复审将检查下面的最终候选。

| Finding | 修复 | 验证 |
| --- | --- | --- |
| FIX-001 MUST_FIX | 普通分片 BuildShards 复用外层 Resources 对象，所有分片共享准入与响应预算 | ShardedRedis_SharesFactoryAdmissionAndPayloadBudgetAcrossNodes：net6/net8 各通过；显式确保前两个键路由到不同节点，实际工厂最大并发 1，64-byte 值超 32-byte 预算拒绝 |
| FIX-002 MUST_FIX | 三 Provider 均先进入同键 gate、二次读取，然后获取准入许可；命中/异常/取消释放路径保留 | SameKeyMergeWithZeroQueue_AndLeaseLossCancelsFactory、RedisAndHybrid_SameKeyMergeWithZeroQueueAndLeaseLossCancelsFactory；真实 1/0 预算下两个同键请求只回源一次；各 Provider 的不同键准入专项最大并发 1 |
| FIX-003 SHOULD_FIX | Free/Easy 续租失败取消工厂 lifetime CTS，调用方 Token 仍单独用于异常身份；同步委托保留无法强制中止边界 | 删除租约后等待续租周期，未完成工厂的令牌取消，API 3s 内以 TimeoutException 返回，晚结果不写回；三 Provider 共享专项通过 |
| FIX-004 SHOULD_FIX | Hybrid 普通/批量成功写后用 GET/PTTL 规则热填 L1；tracker 固定实例 ID 前缀过滤自身消息，不存无界消息列表；peer 失效保留 | HybridCluster_CrossSlotBatchStreamRemoveAndClear_UpdatesDistributedAndLocalLayers 的原写后 L1 断言保留并通过；单节点/分片 Hybrid 与 bus 发布失败/重连回归通过 |

附带直接回归：协调时间正的 sub-ms 值向上取整；Free sync 到期使用统一 TimeoutException；Free/CS 独立 JSON 引用上下文、self reference、nullable/root-null/missing 的 List/Stream 对照均通过。

## 受影响阶段验证

| 项目 | net8.0 | net6.0 | 证据 |
| --- | --- | --- | --- |
| Bing.Caching.Tests | 130 pass | 130 pass | 公共配置、键格式、factory 生命周期、准入、字节预算、命令探针；同名 TRX 被 net6 覆盖，net8 控制台结果已记录，不把一个 TRX 伪称两份 |
| FreeRedis.Tests.Integration | 94 pass / 13 skip | 94 pass / 13 skip | test-results/FreeRedis-final、test-results/free；专用数据、双分片、普通 Cluster，重启/迁移/大规模门控未自动开启 |
| CSRedis.Tests.Integration | 91 pass / 12 skip | 91 pass / 12 skip | test-results/CSRedis-final、test-results/cs；同环境 |
| Bing.EasyCaching.Tests | 67 pass / 18 skip + 分片资源专项 1 pass | 67 pass / 18 skip + 分片资源专项 1 pass | test-results/easy 的 net8-final、net6；新分片资源测试在全项目后新增，仅精准验证，其他 scope 不变 |

`dotnet build Bing.All.sln --no-restore`：通过，141 warnings / 0 errors；包含两个 admin 消费者。缓存生产项目均产出 netstandard2.0。EasyCaching/benchmark 缓存 restore 仍显示 NU1801 和 NU1603（外部 NuGet 索引不可用及既有近似包版本），不把 warnings 当成功修复，也未修改包版本。

阶段回归期间，旧 Easy mock 仍拦截已替换的 Provider GetAll/Set，而生产改用原生 IDatabase MGET/Lua，造成等待 issued 信号的测试挂起。更新测试至实际调用边界、保留一次派发及取消断言，晚失败任务观察；旧“收到精确 caller token”断言改为在工厂运行中验证关联 lifetime token 取消及返回 caller token 的异常身份。总线暂停/断连专项共享节点，Easy 测试改串行，避免其他 Hybrid 断言被故障测试清空 L1。先前失败/中止 TRX 保留，不计入通过；hang dump 不作为交付必需数据。

## 性能工作

新结构假设：不可变命名空间/hash 输入前缀缓存，直接生成 16-byte hash hex 标签，保持 v2 物理键逐字节不变。精确参考算法对照测试通过。诊断只运行一次（每 Provider，1k/10k，1 warmup/3 samples）；正式确认只运行一次（2 warmups/10 samples），不重复旧 SHA 实例/返回类型微调，不改变预算。

新增内部 CacheCommandProbe 及直接重叠/失败测试，记录接入批读/Free-CS 扫描原生调用派发数和在途峰值；Easy SCAN 在 StackExchange.KeysAsync 内部，未接入其底层每页派发，报告 scope 明确。网络请求和客户端内部排队未知仍为 -1。新增阶段 10ms 工作集采样加首尾样本，明确为采样峰值，不能当 OS 精确峰值。预算对照关闭额外测量，资源矩阵单独开启。命令/工作集 smoke 成功，1k CSRedis 批读原生 Calls=2、最大在途=1。

运行前冻结 candidate-source-identity.json。run-performance-matrix.ps1 按单节点/双分片/三主三从、4 Provider、1k/10k/100k，串行运行 2 warmups/10 samples，每 cell 预声明 EXPECTED_SUCCESS 与 600s 终止窗口；真实失败先诊断，超时为 VERIFIED_BOUNDARY，不为未完成 cell 伪造统计。历史版无等价拓扑/API 的 before 为 NOT_APPLICABLE。

## 计划偏差与边界

- 原始响应按单批字节预算拒绝，未做自动按字节重新读取拆分；重新读取会额外派发并引入混合版本。文档明确单响应/解码前边界、底层缓冲和最终 List O(N) 不受该预算限制。
- 同键 gate 等待者不计入不同键准入额度；嵌套不同键工厂仍可能耗尽额度，应避免在同一耗尽预算下相互等待，等待超时明确失败。
- 严格旧/中间/优化三方对照缺少中间源码快照，BLOCKED_EXTERNAL；原始历史 JSON 未修改。网络往返与客户端内部排队仍不可由原生命令数推断，保留观测缺口。
- 同步用户代码、异步返回 Task 前的同步阻塞不能强制中止；租约不是事务锁；Hybrid 最终一致；SCAN 非快照、早停无法确认末尾拓扑完整性。

## 最终证据与剩余 TODO

正式矩阵共 14 个 cell：7 COMPLETED、7 VERIFIED_TIMEOUT_BOUNDARY、0 FAILED。Free/CS 两个单节点 1k/10k 预算 cell 完成；Easy Redis/Hybrid 单节点、双分片及 Easy Redis Cluster 的三个规模完整；Free/CS 单节点 100k、双分片、Cluster 以及 Easy Hybrid Cluster 达到固定 600 秒整轮观察边界。该窗口覆盖整 cell 的预热与采样，不是某一次 API 的时限；超时不证明单次调用失败或不支持该规模。原始结果见 performance-matrix/run-status.json，统计汇总见 performance-matrix/summary.csv（159 行）；超时行无统计。

运行产物归档入口：[task004 运行产物](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/)，矩阵、测试结果及门控/构建日志均按原相对结构保存。

预算对照仍失败：命中请求 P95 4/4 PASS，批读 P95 1/4 PASS，扫描 P95 2/4 PASS，批读与扫描平均分配 0/8 PASS。不改变 10%/20%/30% 原预算。键编码假设已完成一次诊断和一次正式确认，不再重复同一实验；新结构假设作为后续性能 TODO，见 performance.md。

矩阵杀掉超时子进程时 finally 清理可能未执行，后续隔离实例扫描可能包含前序随机 bench 命名空间残留。预算 cell 在这些超时之前结束，二方对照保留；后续矩阵仅报告该运行条件下的绝对值，不用于 Provider 横向提升声明。harness 仅在整个 cell 成功后写最终 JSON，超时没有完整或可恢复的正式统计。两个任务自有 279,497,080-byte hang dump 已删除，原失败/中止 TRX 与 Sequence 证据保留。

新增 Free/CS 三类集成测试改用 IntegrationFact 后，仅运行对应门控专项：2 Provider × 2 TFM × 2 模式，8 份独立 TRX。RUN_INTEGRATION_TESTS=false 且配置不可用测试地址时 24 个 UnitTestResult 为 NotExecuted；开启门控在 :52800 时 24 个用例通过、0 失败。VSTest Counters.notExecuted 字段为 0，因此跳过数以逐用例 outcome=NotExecuted 及 CLI 为准，不把该字段误记成已执行。证据 test-results/gates、gate-validation.log；归档入口见 [task004 运行产物](../../../artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/)。

最终候选对照 candidate-source-identity.json：47 个生产/benchmark 源文件哈希均未变化；测试属性变化未影响生产路径，复用上面的完整 net6/net8 与解决方案构建证据。git diff --check 通过，只有两处现有 CRLF→LF 提示。后续仅文档更改不重跑正确性或性能矩阵。

测试结束后已核对名称与 redis:7.4 镜像并移除本任务创建的 data、bus、cluster 三个临时容器；其他容器保持原状态。需要复测时按计划重建隔离环境。

Round 1 Summary：Implementation 4/4 Review Finding CLOSED（100%）；Release 仍 BLOCKED。L0 diff/源码身份通过；L1 门控专项通过；L2/L3 两个 TFM 的受影响项目与非生产单节点/分片/Cluster 已验证；L4 解决方案构建通过；L5 本次完整矩阵已终止且预算 FAIL。Reused Evidence: REUSED_UNCHANGED_SCOPE 的完整项目测试/构建。OPEN_ACTIONABLE=0；BLOCKED_EXTERNAL=缺少中间源码；DEFERRED=传输/客户端队列观测、按字节自适应拆分、基准检查点；NOT_APPLICABLE=旧版流式及多拓扑 before；VERIFIED_BOUNDARY=7 个整轮超时及预算失败。No-Progress Check: CHANGED（本轮已修复与新证据）；Next Action: STOP，GOAL_STATUS=STOPPED_BLOCKED。未提交、推送或发布。
