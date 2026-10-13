# BING-CACHE-TODO-20261007-095

## 本轮目标

为 Redis Hybrid outbox 增加默认关闭、可验证的毒消息隔离能力。持续失败消息达到持久化投递次数后，原子转移到有界 dead-letter 集合，避免长期占用 processing；默认配置继续保持无限重试和不丢消息行为。

## TODO

- [x] 增加 `MaxDeliveryAttempts` 和 `DeadLetterMaxMessages`，限制范围并要求成对启用。
- [x] 为启用 dead-letter 的实例增加同 hash tag 的 `:attempts` Hash 和 `:dead` Sorted Set。
- [x] `TryTake`/`TryTakeAsync` 跨 outbox 重建持久化投递尝试次数；默认关闭时不创建 attempts 结构。
- [x] 用 Lua 原子完成 processing 删除、dead-letter 写入和 attempts 清理。
- [x] dead-letter 达到容量上限、取消或 Redis 脚本失败时保留 processing。
- [x] worker 达到阈值后继续 drain 后续 pending；成功隔离计入进程内诊断快照。
- [x] 快照增加共享 dead-letter 数量和当前进程隔离数量。
- [x] 单元测试覆盖默认关闭、配置成对校验、阈值隔离和死信计数。
- [x] 真实 Redis 测试覆盖重建后尝试次数、原子隔离、容量满时保留 processing 和默认 attempts 清理。
- [x] 更新 README、迁移说明、权威 TODO 索引和生产符号映射。

## 本轮未解决的运维 TODO

- [ ] 增加 dead-letter 保留期、空间预算、过期清理和容量告警。
- [ ] 增加 dead-letter 人工查看、重放、确认删除和权限审计接口。
- [ ] 跨进程聚合成功/失败/隔离计数并接入统一指标后端。
- [ ] 设计写入提交到失效消息入队之间崩溃窗口的事务 outbox 或补偿机制。

## 既有边界

- [ ] NullCache 流式缺失项语义仍需公共行为决策（`BLOCKED_APPROVAL`）。
- [ ] 正式性能预算、100,000 项超时矩阵、真实网络/客户端排队观测和严格三方基线仍按权威索引保留。
