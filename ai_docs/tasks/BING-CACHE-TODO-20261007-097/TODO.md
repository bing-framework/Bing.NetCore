# BING-CACHE-TODO-20261007-097

## 本轮已完成

- [x] 增加 `DeadLetterMaxBytes`，零值保持旧行为并校验 1 GiB 上限。
- [x] 将字节预算从 Hybrid 配置传入 Redis outbox，并扩展诊断快照的 `DeadLetterBytes`。
- [x] 增加同命名空间的 dead-letter `:sizes` hash 和 `:bytes` counter。
- [x] dead-letter 转移使用 Lua 原子检查消息数与 payload UTF-8 字节总数，预算不足时保留 processing。
- [x] 清理 Lua 同步删除 size 记录和总字节计数，归零时删除 counter。
- [x] 辅助键缺失或数量不一致时按 Redis member 实际字节长度重算历史 payload。
- [x] 单元测试覆盖配置校验、默认 null 诊断和快照字节映射。
- [x] 真实 Redis 测试覆盖精确边界、超限拒绝、processing 保留、历史辅助键重建、清理归零和竞争 writer 原子性。
- [x] 更新 EasyCaching README、迁移说明、权威索引、执行报告、审查报告和生产符号映射。

## 本轮未完成

- [ ] Redis allocator、Sorted Set/hash/list 元数据和碎片的实际进程内存预算。
- [ ] dead-letter 人工查看、重放、确认删除和权限审计接口。
- [ ] 跨进程全局指标聚合和统一指标后端。
- [ ] 数据提交到失效消息入队之间的事务 outbox 或补偿机制。

## 既有边界

- [ ] NullCache 流式缺失项语义仍需公共行为决策（`BLOCKED_APPROVAL`）。
- [ ] 正式性能预算、100,000 项超时矩阵、真实网络/客户端排队观测和严格三方 baseline 仍按权威索引保留。
