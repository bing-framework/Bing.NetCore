# Hybrid 流式特殊键隔离 TODO

## Open Actionable

- [ ] 当前任务范围内无剩余本地可执行项。

## Completed

- [x] Hybrid `GetByPrefixStreamAsync` 特殊逻辑键读取。
- [x] 相似命名空间哨兵隔离及 `ClearAsync` 不越界。
- [x] 特殊键模式删除只删除目标键，普通键保持存在。
- [x] net6.0/net8.0 真实单节点 Redis 精准测试通过。

## Blocked External

- [ ] 双实例总线故障、双分片和三主三从 Cluster 仍需专用环境，不由本轮单节点测试替代。

## Deferred

- [ ] 批量性能预算、真实网络往返和严格三方 baseline 继续沿用前序任务状态。
