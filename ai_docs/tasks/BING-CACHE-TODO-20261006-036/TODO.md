# 本轮 TODO

## Open Actionable

- [ ] 无（本轮计划项已完成）。

## Completed

- [x] FreeRedis 单节点 100,000 项 List/Stream/前缀一致性测试：net8.0、net6.0 各 1/1 通过。
- [x] CSRedis 单节点 100,000 项 List/Stream/前缀一致性测试：net8.0、net6.0 各 1/1 通过。
- [x] 单节点 100,000 项一致性缺口已关闭；Cluster/分片大数据不由本结果替代。

## Inherited Open TODO

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算仍未闭环；数组复用、键格式、JSON 批量解码等候选已验证，不能重复同类实验。
- [ ] T02/T03 之后的跨进程回源协调回归仍需重新生成或复用当前源码证据。

## Blocked External

- [ ] 三主三从 Cluster、双节点客户端分片、独立 Hybrid 总线故障恢复和严格历史三方性能基线仍需要外部拓扑或可重建基线。

## Deferred

- [ ] 网络往返、连接池排队、服务端扫描停止和 Provider 内部并发指标。
