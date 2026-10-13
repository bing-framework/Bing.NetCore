# Hybrid outbox dead-letter 管理能力计划

## 1. 目标与当前结论

为 Hybrid Redis outbox 增加受控的 dead-letter 人工查看、重放和确认删除能力，并提供显式授权与审计扩展点。默认投递行为、自动保留期清理、消息数/字节预算和现有诊断快照保持兼容。

当前真实实现只有 `IHybridInvalidationDeliveryDiagnostics` 状态快照、自动保留期清理和内部 `TryDeadLetter`；`RedisHybridInvalidationOutbox` 没有人工读取或状态转移入口，死信 payload 只存在于 Redis sorted set。注册入口也没有公开管理服务。上一轮 097 已完成精确 payload 字节预算和历史计数重算，但将人工查看、重放、确认删除和权限审计列为后续能力。

本轮选择该项作为新的可执行 TODO。它不改变缓存读写 API，也不把 Redis 管理操作误认为业务事务；所有管理操作必须经过调用方提供的授权器，并将成功操作发送到审计接收器。未配置授权器或审计接收器时，管理接口 fail fast。

## 2. 设计约束

- 只支持 Redis outbox；内存 Hybrid 或未启用 outbox 的实例调用管理方法直接返回明确的 `NotSupportedException`。
- 读取按 dead-letter 转移时间升序、限制最大返回条数，禁止一次性无界读取；条目不直接暴露原始 JSON，使用 Base64 opaque handle 和 SHA-256 confirmation token。
- 重放必须在 Redis Lua 中原子完成：检查 pending+processing 容量、从 dead-letter 删除、放回 pending、清除投递尝试和字节辅助计数；容量不足或条目不存在不改变状态。
- 确认删除必须校验 confirmation token，并在同一 Lua 操作中删除 dead-letter member、sizes 记录和 bytes counter。
- 历史 095/096/097 dead-letter 没有管理索引时仍可查看、重放和删除；handle 由当前 payload 计算，不依赖新增索引键。
- 授权器在每次查看、重放和确认删除前执行；审计接收器记录 actor、topic、action、message id、结果、影响数量和时间。审计失败向调用方传播，但不伪造 Redis 回滚保证。
- 不提供自动重放、不绕过现有 dead-letter 容量限制、不把管理操作纳入 Hybrid 缓存业务事务。

## 3. 实施任务

### 098-01 公共管理契约

目标：增加可被 DI 消费的管理接口、条目 DTO、授权请求和审计记录。

现状/证据：`IHybridInvalidationDeliveryDiagnostics` 仅提供快照；`HybridInvalidationDelivery` 是内部单例，注册入口只公开 diagnostics。

修改范围：`Bing.EasyCaching` 新增管理接口和 DTO；不修改 `ICache`/`IRedisCache`。

实施步骤：

1. 增加 `IHybridInvalidationDeadLetterAdmin`，提供异步查看、重放、确认删除。
2. 增加 dead-letter action 枚举、entry DTO、authorization request、audit entry。
3. 增加 `IHybridInvalidationDeadLetterAuthorizer` 与 `IHybridInvalidationDeadLetterAuditSink`，明确异步、调用方 actor 和取消语义。
4. entry 保留 topic、message id、cache key 数量/内容、prefix/pattern 标记、dead-letter 时间、payload 字节数、opaque handle 和 confirmation token；解析失败条目标记 malformed 并仍可删除。

依赖：无。

验证：公共 API 编译、默认未配置授权/审计时 fail fast 的单元测试、合法 actor/取消参数校验。

风险：新增公开类型但不改变已有接口；handle 不能当作权限凭据，授权器仍是安全边界。

验收标准：调用方可从 DI 获得管理接口；没有授权器或审计器的调用不会访问 Redis；类型文档说明操作非事务性和敏感数据处理。

### 098-02 Redis outbox 原子管理路径

目标：在 `RedisHybridInvalidationOutbox` 中实现有界查看、容量受控重放和确认删除。

现状/证据：dead-letter 使用 sorted set 保存完整 payload，`:sizes`/`:bytes` 只用于 097 字节预算；已有自动清理 Lua 可复用计数修复逻辑。

修改范围：`RedisHybridInvalidationOutbox` 及内部 outbox 管理扩展接口；不改变自动投递路径。

实施步骤：

1. 增加最大查看条数校验和按 rank 读取，解析 envelope，生成 opaque handle/confirmation token。
2. 增加 replay Lua，先检查容量和 member 存在性，再原子移入 pending、清理 attempts 与 dead-letter 字节辅助数据。
3. 增加 delete Lua，原子校验并移除 member，维护 sizes/bytes；启用字节预算时复用历史辅助键重建逻辑。
4. 为 `IHybridInvalidationDeadLetterAdminOutbox` 提供异步方法，使 delivery 不依赖具体 Redis 类。
5. 对 malformed payload 保留原始 handle 和字节信息；重放继续由 Redis envelope 校验失败显式拒绝。

依赖：098-01 的 DTO/handle 规则。

验证：真实 Redis 门禁覆盖空集合、有界列表、历史 payload、容量满、重放后 attempts 清理、确认 token 错误、重复删除和 bytes 归零；单元测试覆盖脚本结果映射。

风险：管理读取仍是非事务快照；重放后 pending 可能在后台投递前再次失败；审计与 Redis 变更之间不提供跨系统事务。

验收标准：任何失败分支不改变 Redis 状态；并发重放/删除同一 member 至多成功一次；重放遵守原有 `MaxPendingMessages` 与 dead-letter 字节计数。

### 098-03 Delivery、DI、授权与审计接入

目标：将管理能力接入 `HybridInvalidationDelivery` 和单节点/客户端分片注册。

现状/证据：`AddHybridCaching` 和 `AddShardedHybridCaching` 已按 topic 注册 diagnostics，但没有管理服务。

实施步骤：

1. 让 delivery 实现管理接口，构造时接收可选 authorizer/audit sink。
2. 每次管理操作先校验 outbox、actor、取消令牌，再调用 authorizer；成功或失败结果按定义写入 audit sink。
3. 单节点注册一个 `IHybridInvalidationDeadLetterAdmin`；客户端分片按 topic 注册多个实例，保持 `IEnumerable<>` 语义。
4. 更新 README 和迁移文档，说明 DI 注册、授权器必须拒绝未授权调用、审计失败不回滚 Redis、handle 不应写入普通日志。

依赖：098-01、098-02。

验证：DI 单元测试、单节点/分片服务数量测试、取消和授权失败不访问 Redis、审计失败传播测试。

风险：管理服务使用单例；授权器和审计器必须自行保证线程安全和跨进程持久性。

验收标准：默认缓存读写路径无行为变化；管理调用链覆盖所有公开操作，且 no-op/unsupported 情况显式失败。

### 098-04 证据与交付

目标：完成精准测试、受影响项目回归、报告和 TODO 状态治理。

验证顺序：

1. `Bing.EasyCaching.Tests` 精准单元测试。
2. Redis outbox net6.0/net8.0 真实门禁（`RUN_INTEGRATION_TESTS=true`、明确非生产端点）。
3. EasyCaching 受影响完整测试双 TFM。
4. `Bing.EasyCaching` netstandard2.0 `--no-restore` 构建。
5. `git diff --check` 和 UTF-8 文档检查。

交付物：`execution.md`、`TODO.md`、`test-mapping.md`、`review.md`，并更新 `ai_docs/cache-todo-index.md`。

## 4. Change Impact Analysis 预估

ChangedFiles：Bing.EasyCaching 公共管理类型、delivery、Redis outbox、注册扩展、测试和文档。

ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。

ChangedPublicContracts：新增 dead-letter 管理/授权/审计接口和 DTO；不修改既有缓存接口。

ChangedRuntimePaths：仅 Hybrid Redis outbox 管理路径和 DI 注册；正常缓存读写、自动投递、自动清理保持原路径。

ChangedProviders：EasyCaching Hybrid Redis outbox；FreeRedis/CSRedis 不受影响。

ChangedTFMs：netstandard2.0 生产构建、net6.0/net8.0 测试。

ChangedBuildPackaging：无新依赖、无版本变化。

ChangedBenchmarkHarness：无。

ChangedDocsOnly：README、迁移说明、任务证据。

AffectedDependents：使用 Hybrid DI 注册和 `IHybridInvalidationDeliveryDiagnostics` 的测试/应用；新增接口为可选消费。

RiskLevel：MEDIUM（新增公开运维 API、Redis Lua 状态转移和授权审计边界）。

## 5. 非目标与未决边界

- 不实现 Redis allocator 实际进程内存预算。
- 不实现跨进程全局指标后端。
- 不实现数据提交到失效入队之间的事务 outbox/补偿窗口。
- 不改变正式性能预算、100,000 项性能矩阵或 NullCache 语义审批状态。
- 不自动提交、推送、发布或批准性能/容量阈值。

## 6. Definition of Done

- 管理接口、授权器、审计器和 DTO 进入真实 DI 调用链。
- 查看、重放、确认删除均有 Redis 原子实现和双 TFM 真实 Redis 证据。
- 历史 dead-letter、malformed payload、容量满、重复操作、取消和审计失败行为有明确测试。
- README、迁移说明、权威索引、execution/review/TODO/test mapping 同步完成。
- 生产构建和 `git diff --check` 通过，未提交 Git 变更。
