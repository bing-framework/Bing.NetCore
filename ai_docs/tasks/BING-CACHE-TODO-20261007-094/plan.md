# BING-CACHE-TODO-20261007-094：Hybrid outbox 运维可观测性

## 目标

继续处理 093 轮识别的 Hybrid outbox 运维 TODO：提供只读的队列深度、最早消息年龄、投递成功/失败次数和最近失败信息，帮助调用方发现积压、连接故障和恢复状态。保持默认行为、失效语义和 Redis outbox 数据格式兼容，不在本轮引入死信或自动删除策略。

## 当前缺口

- outbox 只有后台循环，没有公开的队列深度和消息年龄读数。
- 发布失败和成功没有可查询的进程内计数；排障只能依赖日志或 Redis 手工检查。
- pending/processing 中的消息没有入队时间，因此无法判断积压年龄。
- 毒消息、死信、保留期和跨进程全局计数仍需要后续独立设计，本轮不伪造已实现。

## 实现范围

### P1：只读诊断契约

- 新增 `IHybridInvalidationDeliveryDiagnostics` 和不可变 `HybridInvalidationDeliverySnapshot`。
- 快照包含主题、是否启用 outbox、pending/processing 数量、两类最早消息年龄、成功发布次数、失败尝试次数、入队次数、最近失败时间和简短错误文本。
- 提供同步和异步读取；无 outbox 时返回稳定的零值快照。
- 计数明确是当前进程视角，Redis 队列深度是跨进程共享视角。

### P2：Redis outbox 状态读取

- `IHybridInvalidationOutbox` 增加同步/异步状态读取。
- Envelope 增加入队 Unix 毫秒时间；旧消息缺失该字段时年龄返回 null，不阻断投递。
- Redis 状态读取只访问列表长度和队尾样本，不完整收集消息，不改变 pending/processing 语义。
- pending/processing 继续使用同一物理键根，保持 Cluster 同槽约束。

### P3：调用链、测试和文档

- DI 将每个 Hybrid 投递器同时注册为 `IHybridInvalidationDeliveryDiagnostics`；客户端分片可通过 `IEnumerable` 按主题读取。
- 发布、入队、后台重试和确认路径更新进程内计数并记录最近失败。
- 补充无 outbox、失败/入队、快照年龄和真实 Redis 队列深度测试。
- 更新 README、迁移说明、权威 TODO 索引、执行报告和生产符号映射。

## 不在本轮范围

- 不实现死信队列、毒消息自动隔离、人工重放或保留期清理。
- 不宣称跨进程全局成功/失败计数、严格一次投递、全局顺序或事务性 outbox。
- 不重跑与本轮源码范围无关的 100K 性能矩阵和三方历史性能基线。

## Change Impact Analysis

- ChangedFiles：Bing.EasyCaching 诊断契约、投递器、Redis outbox、DI 注册；EasyCaching 测试、README、迁移文档、TODO 索引和任务证据。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：新增只读诊断接口、快照类型和 DI 服务；既有缓存操作签名不变。
- ChangedRuntimePaths：Hybrid outbox 入队 envelope、状态读取和本地计数，不改变缓存读写和失效发布结果。
- ChangedProviders：EasyCaching Redis/Hybrid；FreeRedis、CSRedis、Memory 不变。
- ChangedTFMs：生产 `netstandard2.0`；测试 `net6.0`、`net8.0`。
- ChangedBuildPackaging：无新包。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：README、迁移说明、索引和任务证据。
- AffectedDependents：Hybrid DI 消费方、客户端分片诊断消费方、现有 outbox 集成测试。
- RiskLevel：MEDIUM；新增只读观测，Redis envelope 向后兼容，默认缓存行为不变。

## 验收标准

- 不启用 outbox 时快照可读取且所有队列字段为零/null。
- 启用 outbox 后快照反映 pending/processing 深度及入队年龄，读取不改变队列。
- 发布成功、失败尝试、接受入队和后台确认计数符合实际调用；最近失败信息不泄露完整消息载荷。
- 旧 envelope 没有时间字段时仍可正常投递，年龄返回 null。
- 生产 netstandard2.0 构建、EasyCaching 精准测试和显式 Redis outbox 门禁在 net6/net8 通过。
- 更新索引时只关闭“Hybrid outbox 运维可观测性”这一项；死信、保留期、跨进程全局计数仍列为未完成/延后内容。
