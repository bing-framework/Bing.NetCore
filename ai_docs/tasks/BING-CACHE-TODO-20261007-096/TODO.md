# BING-CACHE-TODO-20261007-096

## 本轮目标

为 Redis Hybrid outbox 增加默认关闭、按批次运行的 dead-letter 保留期清理，避免死信集合无限累积，同时保持未配置保留期时的永久保留行为。

## TODO

- [x] 增加 `DeadLetterRetention`、`DeadLetterCleanupInterval` 和 `DeadLetterCleanupBatchSize` 配置并校验范围。
- [x] 保留期未启用或 dead-letter 未启用时不发送自动清理命令。
- [x] 使用 dead-letter 转移时间作为 Sorted Set score，报告最早死信年龄。
- [x] 使用 Lua 按截止时间和批大小原子删除过期 dead-letter 成员。
- [x] worker 按最小轮询间隔调度清理；清理异常只进入已有失败诊断，不影响 processing 保留。
- [x] 诊断快照增加最早死信年龄和当前进程过期清理计数。
- [x] 单元测试覆盖默认关闭、配置校验、批大小、清理成功和清理失败。
- [x] 真实 Redis 测试覆盖未过期成员保留、批大小上限、过期删除和清理后容量恢复。
- [x] 更新 README、迁移说明、权威 TODO 索引和生产符号映射。

## 本轮未解决的运维 TODO

- [ ] 精确 Redis 字节级空间预算和历史 payload 大小重算。
- [ ] dead-letter 人工查看、重放、确认删除和权限审计接口。
- [ ] 跨进程聚合成功/失败/隔离/清理计数并接入统一指标后端。
- [ ] 设计写入提交到失效消息入队之间崩溃窗口的事务 outbox 或补偿机制。

## 既有边界

- [ ] NullCache 流式缺失项语义仍需公共行为决策（`BLOCKED_APPROVAL`）。
- [ ] 正式性能预算、100,000 项超时矩阵、真实网络/客户端排队观测和严格三方基线仍按权威索引保留。
