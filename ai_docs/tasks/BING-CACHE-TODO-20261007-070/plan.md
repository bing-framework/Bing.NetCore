# Hybrid 失效消息字节预算与有界拆分计划

## 目标

补齐当前 Hybrid 批量失效通知的本地资源保护：当一次批量删除或协调写回需要向 EasyCaching Redis 总线发布大量物理键时，使用可选的独立消息字节预算拆分消息，避免单条总线消息随键数量无界增长。

本轮只实现消息大小预算和顺序拆分，不宣称消息代理背压、重试、持久化 outbox 或崩溃恢复；这些仍保留为独立延后/接受限制。

## 现状与证据

- `HybridCacheManager` 和分片注册入口把 `physicalKeys` 一次性放入一个 `EasyCachingMessage.CacheKeys` 后发布。
- `RedisLeaseCoordinator` 已按 Redis 命令字节预算切分删除批次，但总线消息仍可能大于代理允许的消息大小。
- `HybridBusFailureTracker` 会传播发布失败；拆分后仍按顺序发布，已发布消息不回滚。
- EasyCaching Provider 暴露与总线相同的 `IEasyCachingSerializer`，可以用实际序列化结果计算消息字节，而不是用键字符串长度近似。

## 设计与范围

### P1：配置与异常

- 在 `CacheResourceOptions` 末尾增加可选 `maxInvalidationMessageBytes`，默认 `0` 表示不限制，保持已有构造调用兼容。
- 正值表示单条 `EasyCachingMessage` 序列化后的最大 UTF-8/序列化载荷字节数；`0` 保持旧行为。
- 单个物理键连同消息固定字段已经超过预算时，抛出明确的公共异常，不发送超限消息。

### P2：共享拆分器

- 新增 EasyCaching 内部消息拆分器，使用实际 Provider serializer 试算候选消息大小。
- 保持输入顺序和重复键；只在追加下一个键会超限时切分。
- 每个分片生成独立消息 ID；空键集合不发布消息。
- 同步和异步发布共用同一拆分逻辑；异步在每个分片前检查取消令牌，已发布分片不撤回。

### P3：Hybrid 接入

- `HybridCacheManager` 和客户端分片 Hybrid 注册入口的单键、批量同步/异步失效回调统一走拆分器。
- 本地层清理顺序保持不变：先完成本地删除，再按顺序发布分片。
- 总线异常仍由 `HybridBusFailureTracker` 报告；不新增静默吞错或重试。

## 非目标与边界

- 不修改 Redis 数据键、租约键、批量命令字节预算或默认消息行为。
- 不推断消息代理实际网络帧大小；预算只针对配置的 EasyCaching serializer 输出。
- 不实现背压、重试策略、outbox、跨进程严格一次或崩溃恢复。

## 验证

- `Bing.Caching.Tests`：配置默认值、负数拒绝和异常属性。
- `Bing.EasyCaching.Tests`：拆分边界、单键超限、顺序/重复键、同步/异步取消、发布失败传播。
- 受影响生产项目 `Bing.Caching`、`Bing.EasyCaching` 构建 netstandard2.0；EasyCaching 测试 net6.0/net8.0 精准及完整单元回归。
- L0：UTF-8、静态调用点、`git diff --check`。

## 验收标准

1. 默认 `MaxInvalidationMessageBytes=0` 时现有消息数量和行为不变。
2. 正值预算下每条发布消息的 serializer 载荷不超过预算，单键超限 fail fast。
3. 同步/异步、单键/批量、普通 Hybrid/分片 Hybrid 均使用同一拆分语义。
4. 取消只阻止后续消息；已经成功发布的消息不伪造为可撤销。
5. 现有总线失败异常传播和本地层一致性测试保持通过。
