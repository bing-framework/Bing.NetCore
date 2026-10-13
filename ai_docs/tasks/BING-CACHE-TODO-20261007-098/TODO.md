# BING-CACHE-TODO-20261007-098

## 本轮已完成

- [x] 新增 `IHybridInvalidationDeadLetterAdmin`、条目 DTO、授权器和审计接收器契约。
- [x] dead-letter 条目使用 SHA-256 opaque handle 与域分离 confirmation token，原始 payload 只保留在内部状态。
- [x] Redis outbox 增加有界查看、容量受控原子重放和 confirmation token 删除。
- [x] 重放同步维护 pending、processing、attempts、dead-letter、sizes 和 bytes 状态。
- [x] 历史辅助键缺失或计数不一致时继续重算 payload 字节计数。
- [x] malformed payload 可查看和确认删除，明确拒绝重放。
- [x] 管理操作必须先通过 actor、取消和授权检查；授权失败、Redis 失败和审计失败均有明确传播语义。
- [x] 单节点与客户端分片 Hybrid DI 注册管理服务；无授权器/审计器时访问 Redis 前 fail fast。
- [x] 单元测试覆盖授权、失败审计、取消、malformed、opaque token 和注册描述符。
- [x] 真实 Redis 双 TFM 覆盖重放、容量满保留、删除幂等、payload 预算和并发单赢家。
- [x] EasyCaching 完整双 TFM 回归、生产 netstandard2.0 构建和文档同步完成。

## 本轮未完成或保留边界

- [ ] Redis allocator、Sorted Set/hash/list 元数据和碎片的实际进程内存预算（`ACCEPTED_LIMITATION`）。
- [ ] 跨进程全局指标聚合和统一指标后端（`DEFERRED`）。
- [ ] Redis 数据提交到失效消息入队之间的事务 outbox/补偿窗口（`ACCEPTED_LIMITATION`，另立设计任务）。
- [ ] NullCache 流式缺失项语义（`BLOCKED_APPROVAL`）。
- [ ] 正式性能预算、100,000 项超时矩阵、严格旧/中间/优化三方 baseline（`VERIFIED_BOUNDARY`/`BLOCKED_EXTERNAL`）。
- [ ] 真实网络往返和客户端内部排队观测（`DEFERRED`）。

本轮没有新的 `OPEN_ACTIONABLE`；后续只能在用户批准 NullCache 语义、提供外部 baseline 或明确新的运维指标范围后重新立项。
