<!-- AI_REVIEW_STATUS: NEEDS_FIX -->
AI_TASK_ID: 20261024-fusioncache-integration
AI_REVIEWED_AT: 2026-10-24T01:59:04Z

# FusionCache 集成差距审查

## 结论与计数口径

七个公共操作均有真实实现，独立包、命名注册、回源准入、L2、Backplane、Fail-Safe、提前刷新、标签、日志/遥测接线均已交付。没有发现整块首版核心功能仍是占位实现。

但“FC-00～FC-08 全部完成、OPEN_ACTIONABLE=0”不能代表原方案的全部验收条件已经闭环。本次发现 **8 项应补齐的问题，以及 2 项测试加强建议**。按根因分组计数，不把每个缺少的测试方法或性能字段分别计作一个功能。

- 2 项行为/装配约束不足：元数据存活窗口、Backplane 启动就绪。
- 5 项验证/观测不足：关闭与资源生命周期、进程崩溃、锁故障、基础契约边界、诊断及性能字段。
- 1 项迁移文档遗漏：回滚前处置旧缓存空间。
- 2 项可选加强：频道隔离的直接证据、异常来源断言。

这不是“缺少 8 个缓存 API”，也不适合折算为主观完成百分比。常规读写路径已可用，离完整方案验收还差上述闭环。

| 状态维度 | 结论 |
| --- | --- |
| IMPLEMENTATION_STATUS | NEEDS_FIX：主要是配置约束与启动策略 |
| TEST_STATUS | PARTIAL：已有用例通过，不代表计划边界全部覆盖 |
| PERFORMANCE_EVIDENCE_STATUS | PARTIAL：五类负载已有结果，但记录字段不完整 |
| RESOURCE_EVIDENCE_STATUS | PARTIAL：准入已验证，关闭/连接生命周期证据不足 |
| EXTERNAL_GATE_STATUS | NOT_VERIFIED：远程 CI、生产容量未验证 |
| RELEASE_STATUS | NOT_EVALUATED：本次不是发布验收 |
| OPEN_ACTIONABLE | 10 项：默认 recommended 范围 8 项 SHOULD_FIX，另有 2 项 OPTIONAL |
| 本轮动作 | 仅审查和隔离诊断，不修业务代码，不自动进入 Fix |

## 逐阶段对照

| 阶段 | 验收结论 | 已实现 | 距离方案的差距 |
| --- | --- | --- | --- |
| FC-00 工程依赖 | PASS | net8 包、固定依赖、net8/net10 包消费、旧新 Provider 共存 | 无新增缺口；不等于已经发布 |
| FC-01 契约 | PASS | IHybridCache、provider、七操作和 CacheKey 薄扩展 | 基础边界测试归入 FC-03 补齐 |
| FC-02 注册与生命周期 | PARTIAL | 命名/显式默认、输入与身份校验、DI 所有权设计 | FIX-002、003；OPTIONAL FIX-009 |
| FC-03 单键与资源控制 | PARTIAL | 命中、合并、预算、排队、取消、异常、递归控制 | FIX-006：默认值/预取消直接覆盖不足 |
| FC-04 分布式 | PARTIAL | 真实 Redis、双进程传播、健康状态分布式锁、停启恢复 | FIX-002、004、005；OPTIONAL FIX-009、010 |
| FC-05 容错与失效 | PARTIAL | Fail-Safe、软硬超时、提前刷新独立 scope、硬失效映射 | FIX-001、003 |
| FC-06 诊断测量 | PARTIAL | 官方 OTel 接线、四个准入仪表、五类负载结果 | FIX-007 |
| FC-07 样例与迁移 | PARTIAL | 内存、Redis、多实例、容错及迁移说明 | FIX-008；启动策略需与 FIX-002 同步 |
| FC-08 收口 | PARTIAL | CI 单元清单、打包脚本、符号测试映射、执行报告 | 前述差距未关闭，原执行报告完成声明偏宽 |

## 应补齐的问题

### FIX-001：元数据保留窗口只靠文档约定，缺少配置约束

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 计划依据：[plan.md:232](plan.md#63-ttl-与序列化)，要求标签/Clear 元数据覆盖数据、L2、Fail-Safe、抖动的存活窗口。
- 问题：保留了上游默认长窗口，但允许调用方配置更短的 TagsDefaultEntryOptions，也允许每次调用传入更长 TTL，没有比较两者。
- 证据：`Internal/FusionCacheRegistrationValidator.cs:28` 只验证业务 Duration 为正；`Internal/FusionCacheEntryOptionsMapper.cs:13` 只验证本次 TTL 的正数/表示范围；`FusionCachingServiceCollectionExtensions.cs:69` 直接复制元数据选项。迁移文档目前将责任交给使用方。
- 实测：纯 L1 数据 TTL=20 秒，标签 Duration=1 秒、FailSafeMaxDuration=1 秒；Set → InvalidateTag → 等待 2 秒 → TryGet，得到 `Hit=True, Value=42`。在标记到期前没有读取条目，避免惰性清理掩盖问题。保留元数据 FailSafeMaxDuration=10 分钟的对照配置则返回 miss。
- 影响：某些被接受的长数据 TTL/短元数据配置会使已经失效的数据重新可见。默认 5 分钟业务 TTL 未在本次发现这个问题；不能据此声称所有默认场景失效异常。此探针只证实 L1 标签，不冒充 Redis/Clear 的复现结果。
- 修复目标：明确允许的 TTL/保留窗口契约，并在注册及每次 TTL 覆盖时拒绝不满足契约的配置，或采用能保持正确性的元数据策略；不要仅把文档警告当作已经保障了窗口。
- 验证方式：保留短窗口负例及长窗口正例；覆盖 L1/L2、标签/Clear、本次 TTL 覆盖和 Fail-Safe 窗口。等待可以用受控时间/有界 Eventually，不能靠提前读取清掉旧条目后宣称没有复活。

### FIX-002：生产样例与集成测试的 Backplane 启动条件不同

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 计划依据：FC-04 明确要求确定连接初始化完成条件，`plan.md:486`。
- 问题：启动验证器只解析实例，不确认订阅完成。适配器和样例没有设置 WaitForInitialBackplaneSubscribe，上游 2.9.0 默认 false，异步订阅；集成测试却使用 true。
- 证据：`FusionCachingServiceCollectionExtensions.cs:92` 的 StartAsync 直接完成；`samples/Bing.Samples.FusionCache/Program.cs:173` 未设置等待策略；`Bing.Caching.FusionCache.Probe/RedisTestHost.cs:45` 显式设 true。已核对固定版本源码 `FusionCacheOptions.cs:91`、`FusionCache.cs:890` 的后台订阅分支。
- 影响：当前双进程测试证明的是“等待订阅后的传播”，不能证明样例 Host.StartAsync 返回后就能立即接收通知。这里没有声称每次启动都会丢消息。
- 修复目标：明确订阅就绪和失败的宿主策略，并使生产推荐配置、样例与测试一致；如果选择异步就绪，说明就绪前的服务行为并提供验证。
- 验证方式：延迟/失败的 Subscribe、启动后立即预热并更新、订阅完成后的收敛；使用实际默认/推荐配置，不能只在测试宿主里悄悄增强条件。

### FIX-003：后台工厂和外部组件的关闭生命周期证据不完整

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 计划依据：`plan.md:464` 要求释放后无残留订阅；`plan.md:555` 包含 HostShutdown_ObservesBackgroundCompletion；FC-02/04 要求组件所有权明确且测试。
- 问题：已有测试证明请求 scope 结束不影响独立后台 scope，也证明 DI-owned MemoryCache 释放一次；未证明宿主关闭过程中后台工厂、Backplane 订阅、Redis/locker 连接的行为。
- 证据：`FusionCacheResilienceTest.cs:43` 等待工厂完成后才结束测试容器；`FusionCacheLifetimeTest.cs:11` 只直接追踪 MemoryCache。`FusionCacheStartupValidator.StopAsync` 无排空逻辑，迁移文档要求应用自己等待后台工作。
- 影响：不能把“工厂不能被强制终止”推导成“正常关闭的资源交接已验证”；当前接口也没有框架提供的后台工厂排空入口。
- 修复目标：至少落实应用负责排空的可运行关闭示例和受控测试，明确关闭期间的完成/取消/释放顺序及连接所有权。是否提供公共排空 API 属于后续设计，不是本审查强制扩张接口。
- 验证方式：工厂仍在执行时停止宿主；确认约定的完成或取消行为、scope 最终释放、订阅解除；区分 DI-owned 与调用方-owned 资源，不误释放共享外部连接。

### FIX-004：进程崩溃窗口没有实测

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 计划依据：`plan.md:300` 明确写出进程崩溃窗口必须实测并记录。
- 问题：Redis stop/start 测试保留了应用进程，因此不能代表应用崩溃、进程内恢复队列丢失或新进程重启读取的行为。
- 证据：`FusionCacheRedisIntegrationTest.cs:141` 只停止/启动 Redis；`RedisTestSupport.cs:116` 的 Kill 是退出超时后的清理，不是带断言的崩溃场景。
- 影响：已有故障恢复结论只适用于存活进程；尚未取得计划要求的崩溃边界证据。
- 修复目标：增加一个有限的真实子进程崩溃用例，覆盖未完成恢复操作及重启读取，记录可接受的数据窗口。
- 验证方式：故障期间产生待恢复操作，强制退出拥有队列的节点，恢复 Redis 并启动新节点，断言实际状态。无需为此实现持久 outbox，也不需要扩大到完整网络分区矩阵。

### FIX-005：可选分布式锁不可用时的策略未验证、未明确记录

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 计划依据：`plan.md:558` 除健康合并外，还要求记录锁不可用时策略。
- 问题：当前只验证健康 Redis 下两个进程回源合并；未区分拿锁超时、锁服务故障时的抛错/降级/Fail-Safe 行为。
- 证据：`FusionCacheRedisIntegrationTest.cs:102`；`RedisTestHost.cs:63` 直接装配官方 locker。现有 Redis 故障测试并未开启 distributedLock。
- 影响：应用知道“健康时减少重复回源”，但还不知道“锁故障时是否继续回源、是否返回旧值、会等待多久”。
- 修复目标：保留官方组件，明确并验证固定版本的故障策略；不要把锁故障时的业务保证推给“锁是可选的”。
- 验证方式：对锁组件定向制造获取失败/超时，记录实际工厂次数、返回/异常、释放和恢复；无需承诺严格一次或业务互斥。

### FIX-006：基础契约的少量直接验收缺口

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 计划依据：FC-03 的预取消、默认值验收以及 `plan.md:499` 的租户隔离。
- 问题：缺少默认值命中（例如 int 0）与 miss 的直接区分测试、进入调用前已取消的 token 用例，以及同一命名实例内两个租户标签的直接失效隔离用例。
- 证据：`FusionCacheManagerTest.cs:10` 直接验证字符串 null/miss；`FusionCachePolicyTest.cs:74` 是工厂启动后的取消；现有隔离测试主要更换 cache name/namespace。
- 影响：尚无证据表明这些行为已坏，但当前测试映射不能宣称它们全部验证完毕。租户隔离仍是应用构造可信业务键/标签的职责，不是自动租户识别能力。
- 修复目标：补充上述直接用例，不改变公共接口，不借用旧 ICache 测试代替新门面证据。
- 验证方式：缓存 0 后 Hit=true、缺失 Hit=false；预取消不执行工厂/写入；同一实例租户 A 标签失效不影响租户 B。

### FIX-007：性能记录缺字段，诊断验收未闭环

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 计划依据：`plan.md:510`、`plan.md:605`。
- 问题：已有五类负载，但记录缺少 **p99、CPU、进程内存、命中数、Fail-Safe 次数**；没有直接断言 wait.duration histogram、官方状态指标/trace 的事件来源与输出约束。
- 证据：`PerformanceEvidence.cs:11` 的记录结构只有 mean/p50/p95、分配、工厂数和准入峰值；`FactoryMetrics.cs:18` 仅监听 long 仪表；`samples/.../Program.cs:103` 确实已接入官方 OTel，不能据测试缺口说遥测能力不存在。
- 影响：目前只能说明回源合并、额度及这轮小负载的部分开销，不能完整回答原方案的尾延迟、CPU/内存和降级观测要求。恢复场景仅一个样本也不能当分位数分布。
- 修复目标：补齐要求的轻量测量字段与诊断断言，明确统计口径；单样本恢复耗时直接按耗时报告。补充 key/tag 敏感信息与高基数输出的配置说明/验证。
- 验证方式：仅重跑受影响小规模测量；核对实际命中/Fail-Safe/工厂次数与仪表，验证 wait.duration 和 trace。无需增加百万键压测、生产阈值或宣称性能提升比例。

### FIX-008：回滚说明遗漏切回前的旧空间处置

- 状态：OPEN_ACTIONABLE；严重程度：MEDIUM；处理要求：SHOULD_FIX。
- 计划依据：`plan.md:618` 要求回滚前失效/预热旧业务域，或切换新的旧 Provider 命名空间。
- 问题：迁移指南说明继续双空间失效，但未保留上述切换前步骤。停止旧路径后，旧空间可能保留灰度期间残留值。
- 证据：`docs/migrations/fusioncache-adoption.md:26` 的回滚段落。
- 影响：只写“切回旧 Provider 并继续失效”不能解决切换当下已有的旧数据。
- 修复目标：补齐旧空间清理/预热/更换命名空间三选一的流程、执行时机和双空间失效持续条件。
- 验证方式：逐项对照方案第 12 节，检查文档链接和可执行步骤；无需为了这项文档修订重跑 Redis/性能测试。

## 可选测试加强

### FIX-009：频道隔离的证据仍是间接的

- 状态：OPEN_ACTIONABLE（仅 OPTIONAL，不触发默认 recommended 修复）；严重程度：LOW；处理要求：OPTIONAL。
- 问题/证据：`SharedRedis_IsolatesNamesNamespacesAndMetadata`（`FusionCacheRedisIntegrationTest.cs:29`）检查物理键和返回值。另一实例即使被无关通知扰动后再从隔离 L2 回填，也可能得到相同值。生产代码实际设置了独立 BackplaneChannelPrefix，并有身份覆盖拒绝测试，因此未发现频道串用实现缺陷。
- 影响：对方案要求的“频道隔离”缺少直接运行时证据。
- 修复目标/要求：监听实际订阅频道或通知事件，断言不同实例不接收彼此频道消息。
- 验证方式：对共享 Redis 中两个身份执行 Set/Remove/tag/Clear，核对接收事件；不要只比较最终值。

### FIX-010：故障断言可以更准确地识别错误来源

- 状态：OPEN_ACTIONABLE（仅 OPTIONAL，不触发默认 recommended 修复）；严重程度：LOW；处理要求：OPTIONAL。
- 问题/证据：`FusionCacheRedisIntegrationTest.cs:157`、`:193` 接受任意 Exception。所谓 serializer mismatch 使用相同 STJ serializer 写 string、读 Product，已经覆盖一种 schema/类型不兼容，但没有证明不同 serializer 配置互操作的边界。
- 影响：无关异常有可能令测试假通过；不能把一个类型不兼容案例推广为所有序列化迁移场景已验证。
- 修复目标/要求：断言预期包装异常/内部错误来源；如果宣称支持 serializer 配置变更验收，另补配置不兼容用例，否则限定报告表述。
- 验证方式：故障来自指定 L2/序列化操作，且不会被吞成 miss 或触发错误回源。

## 已实现能力与合理实现调整

- 七个操作全部走官方 FusionCache 真实调用链：`FusionCacheManager.cs:27` 起。TryGet 强制不返回 stale；null/miss 复用 CacheEntry；每次复制配置；标签用 Remove，Clear 明确禁止后备。
- `FusionCacheFactoryInvoker.cs:38` 在实际工厂内部申请许可；同键等待者不占全局预算；实际工厂结束才释放；没有再套同键锁。默认 8/128/30 秒已落实。
- 独立键/标签/频道前缀、重复名称/默认实例拒绝、启动解析、原有 Provider 共存均有实现与对应测试。
- net8 核心包保持两项直接消费依赖；Redis L2 10.0.1 与官方扩展 2.9.0 在消费者引用，net8/net10 包消费已有证据。
- 为真双进程测试增加 Probe、使用普通 singleton 命名集合兼容旧 DI、独立复制标签选项、SourceLink PrivateAssets、同步 CI 白名单，都属于合理实现调整，没有减少原定主要能力。
- 同实例嵌套冷回源一律拒绝，比“避免预算耗尽”更保守，但有文档与测试，不视为漏做回源功能。

## 明确延期或不属于首版的能力

以下确实没有由新门面提供，但原方案已经排除或延期，不能计入上述 8 项欠账：

1. 完整 ICache 兼容适配、同步 API、运行时 Type 反射桥接。
2. 原子 TrySet/条件写入。
3. 任意前缀/模式扫描与批量扫描删除。
4. GetAll/SetAll/流式批量 API。
5. 持久 outbox、跨进程恢复队列、重放/dead-letter 管理。
6. 严格一次回源、旧租约 fencing、“显式修改永远胜过迟到工厂”、数据库与缓存原子事务。
7. 自适应缓存、Conditional Refresh、回源结果动态标签等门面高级 API。
8. Redis 便捷组合包、微软 HybridCache 桥接。
9. net6 宿主使用该 FusionCache 2.9.0 适配包；旧 Provider 的 net6 支持仍保留。
10. Admin、JWT、权限缓存的真实业务迁移，以及自动双空间失效/预热编排。

## 已知边界与外部未验证项

- ACCEPTED_LIMITATION：Backplane 最终一致；Clear 是可见性失效；恢复队列进程内且有界；迟到工厂可能覆盖 Set/Remove；Redis L2 整秒 TTL；SizeLimit 不是字节硬上限；无法强行终止忽略取消的代码。
- VERIFIED_BOUNDARY：8 个活跃工厂、128 个排队工厂、排队超时/拒绝；上一轮测量同键 64 请求回源 1 次、不同键峰值 8/56。
- BLOCKED_EXTERNAL：远程 CI、生产容量及正式发布门禁未执行。70 项旧 EasyCaching 外部场景按门控跳过，这不是 70 项新功能欠缺。
- NOT_APPLICABLE：SQL/SQLite 变更门槛、新 API 的历史同构性能基线。
- BLOCKED_APPROVAL：本轮无；是否扩大首版到延期能力需另立需求，但本审查不要求现在决策。

## 验证范围与证据

基准 HEAD：`3919c1509907d661cfef3679dad79368c8b9cb39`，审查对象为其上的当前未提交工作区及新增文件。保留其它任务修改。

本轮只读核对完整 plan、execution、Git staged/unstaged diff、生产源码、测试、样例、CI 列表、迁移文档和固定版本的本地上游源码。测试清单由 luna_worker 独立核对；主代理负责行为判断和结论。

变更影响：生产源码/公共契约/Provider 配置没有修改；新增仅本报告及 `output/fusioncache-review/` 隔离诊断工程。诊断调用 net8 的 L1 标签路径，不连接 Redis、不重建全量基线。

复用上一轮未发生相关源码变化的证据：net8/net10 各 43 个单元用例、9 个 Redis/资源用例；旧缓存 net6/net8 各 204 个、EasyCaching 133 个通过/70 个门控跳过；`.3` 版本的包消费证据。复用表示这些已有用例继续成立，不把缺失用例补算为通过。

本轮新增诊断：

```powershell
dotnet run --project output/fusioncache-review/MetadataProbe.csproj -c Release
dotnet run --project output/fusioncache-review/MetadataProbe.csproj -c Release --no-restore
```

第一轮标签逻辑 Duration=1 秒、物理 FailSafeMaxDuration=10 分钟，结果 `Hit=False`；第二轮将物理保留期缩短为 1 秒，结果如下。两轮数据 TTL 均为 20 秒，失效后等待 2 秒。这区分了逻辑过期与物理丢失，不将第一轮阴性结果隐藏。

```text
AFTER_TAG_MARKER_EXPIRY: Hit=True, Value=42
REPRODUCED: invalidated value resurfaced
```

## 本轮收口

- Completed：完成差距审查；七操作及主要能力已核对，发现项有源码/测试/计划依据。
- Open Actionable：8 项 SHOULD_FIX；2 项 OPTIONAL 单列，默认修复范围不包含它们。
- Deferred：上述首版排除的能力，不纳入本轮实现欠账。
- No-Progress Check：CHANGED，本轮新增验收差距和一个受控行为复现。
- Next Action：STOP。本轮用户要求分析和列举，不自动修复，也不改写原 plan/execution 历史结论。
- Goal Status：未启用 Goal；Review 完成，结论 NEEDS_FIX。未提交、未发布、未发送外部通知。
