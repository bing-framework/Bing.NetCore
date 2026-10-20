# Hybrid dead-letter handle 无状态操作与 Cluster 验证计划

## 目标与现状

上一轮审计发现 `IHybridInvalidationDeadLetterAdmin` 把 `Handle` 定义为后续操作句柄，但 `ReplayAsync` 和 `ConfirmDeleteAsync` 接收的 DTO 含有仅内部可见的原始 payload，跨 HTTP/JSON 往返后无法仅凭公开 handle 完成操作。当前 Redis ZSET 直接以 JSON payload 为 member，因此尚无可按 handle 查询的运行时索引。现有 dead-letter 管理真实 Redis 测试仅覆盖单节点；缓存键布局虽将 outbox 子键放在同一 hash tag，仍需在 Cluster 中验证所有新增 Lua 路径。

## 099-01 公开 handle 操作和兼容迁移

目标：让管理调用方只需提交 `Handle` 和确认删除 token，即可在不同请求或进程中完成重放、确认删除。

修改范围：`Bing.EasyCaching` 的 dead-letter 管理契约、Redis outbox、Hybrid delivery、相关单元/真实 Redis 测试、README 和迁移文档。

实施步骤：

1. 新增按 handle 操作的 `ReplayAsync(string handle, ...)`、`ConfirmDeleteAsync(string handle, string confirmationToken, ...)`；保留现有 DTO 重载并转发到 handle API，避免源码兼容回归。
2. 将 Redis dead-letter ZSET member 改为 payload 的 SHA-256 handle，使用同一命名空间内的 payload hash 保存 payload。新建 `:dead`、payload hash 和 `:sizes`、`:bytes` 相关键必须共享 outbox root 的 hash tag。
3. 查看时按有界数量批量解析 member；对历史“payload 直接作为 ZSET member”的数据执行原子惰性迁移，保留 score 和字节预算，保证升级后旧死信仍可查看、重放和删除。
4. 重放、确认删除、保留期清理和字节计数重算均按 handle/payload 分离后的布局原子维护所有辅助状态；并发管理同一条目最多一个操作成功。
5. 管理方法先执行授权，再按 handle 读取并校验 payload；授权请求允许携带 handle、message id 在解析前为空。确认 token 仍由 payload 派生并在操作前校验。
6. 更新 API 文档和运维说明，覆盖 HTTP/JSON 客户端只保存 handle/token 的使用方式、旧数据惰性迁移及 payload 字节预算口径。

依赖：现有 098 dead-letter 管理实现和 `HybridInvalidationDeadLetterAdminTest`。

验证：单元测试覆盖 handle 跨 delivery 实例操作、错误 handle/token、授权失败先于 Redis 查询、旧 DTO 重载兼容、历史 payload member 迁移和字节计数；真实单节点 Redis 覆盖查看/迁移/重放/删除/并发及重建后凭 handle 操作。

风险：新增公开重载和 Redis 内部存储布局变化。惰性迁移必须避免在并发查看、重放、清理下丢失 dead-letter；历史数据及配置升级时不能要求停机迁移。

验收标准：调用方将公开 `Handle` 和确认 token 序列化后，可通过新 API 完成操作；旧签名仍可用；历史死信在惰性迁移后语义不变；所有状态计数与 Redis 原子操作保持一致。

## 099-02 Redis Cluster 实机验证

目标：证明 dead-letter 管理涉及的 Redis 命令和 Lua 脚本在三主 Cluster 中同槽执行，并保留 outbox 命名空间隔离。

修改范围：`Bing.EasyCaching.Tests` 的 Cluster 集成测试；必要时只为测试门控新增专用环境变量。

实施步骤：

1. 复用 `EasyClusterIntegrationTest` 的三主 Cluster 门控和 Provider 构造方式，使用随机缓存命名空间。
2. 覆盖入队、失败转 dead-letter、有界查看/历史格式迁移、跨实例按 handle 重放、再次 dead-letter、按 handle 确认删除和字节计数归零。
3. 在新建 outbox 命名空间旁设置哨兵键，确认测试操作和清理不触碰其他命名空间。
4. 保持 `RUN_INTEGRATION_TESTS=true` 与 `RUN_CACHE_CLUSTER_TESTS=true` 门控；未提供隔离 Cluster 时保留为 `BLOCKED_EXTERNAL`，不以单节点结果替代。

依赖：099-01。

验证：新增 Cluster 集成用例；运行 `Bing.EasyCaching.Tests` 精准单元测试、outbox 单节点真实 Redis 测试、Cluster 测试（环境可用时）、受影响完整测试、`Bing.EasyCaching` netstandard2.0 构建及 `git diff --check`。

风险：StackExchange.Redis 对 Cluster 的脚本执行要求所有 `KEYS` 同槽；本测试必须从真实 Cluster 端点执行，不使用 mock 或单节点推断。

验收标准：真实 Cluster 中管理脚本不产生 `CROSSSLOT`，handle 状态迁移正确，命名空间哨兵保留；未执行的外部门禁如实标记。

## Change Impact Analysis

- ChangedFiles：dead-letter 公共契约、delivery、Redis outbox、单元与集成测试、EasyCaching README、任务报告和缓存 TODO 权威索引。
- ChangedProjects：`Bing.EasyCaching`、`Bing.EasyCaching.Tests`。
- ChangedPublicContracts：增加 handle 字符串重载及授权请求 handle 字段，保留 DTO 重载。
- ChangedRuntimePaths：Hybrid Redis outbox dead-letter 查看、重放、删除、清理和字节计数；普通缓存读写不变。
- ChangedProviders：EasyCaching Hybrid Redis。
- ChangedTFMs：生产 netstandard2.0；测试 net6.0/net8.0。
- ChangedBuildPackaging：无新增依赖、无版本变化。
- ChangedBenchmarkHarness：无。
- AffectedDependents：Hybrid dead-letter 管理 API 使用者、Redis outbox 历史数据。
- RiskLevel：MEDIUM。

## 非目标

- 不改变 NullCache 流式语义、性能预算或 100,000 项矩阵结论。
- 不实现审计跨系统事务、dead-letter 分页、Redis allocator 进程内存预算或跨进程全局指标。
- 不自动提交、推送、发布或批准新的容量/性能阈值。

## Definition of Done

- 新 handle API 能跨 delivery 实例工作，旧 DTO API 仍兼容。
- 历史 payload member 可安全惰性迁移；字节预算、保留期清理和并发语义经过直接测试。
- 单节点 Redis 测试通过；Cluster 有真实门禁结果或明确记录为外部阻塞。
- 文档、TODO 权威索引、`execution.md` 和生产符号到测试方法映射同步。
- 受影响构建与 `git diff --check` 通过；未提交 Git 变更。
