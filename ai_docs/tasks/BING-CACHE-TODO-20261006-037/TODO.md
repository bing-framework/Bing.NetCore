# 本轮 TODO

## Open Actionable

- [ ] 无（本轮只验证任务 021 已完成的标量解码候选）。

## Completed

- [x] FreeRedis 当前标量解码方向性 benchmark；批量读取 P95 5.9084ms，平均分配 2,391,680B。
- [x] CSRedis 当前标量解码方向性 benchmark；批量读取 P95 5.4470ms，平均分配 1,642,152B。
- [x] FreeRedis/CSRedis 标量 `null/default` 命中契约精准测试（net8.0，各 1/1）。
- [x] Provider netstandard2.0 Release 构建、CacheBench 构建和 `git diff --check`。

## Inherited Open TODO

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算仍未闭环；本轮结果不能替代正式 2 轮预热/10 轮采样矩阵。

## Blocked External

- [ ] 三主三从 Cluster、双节点客户端分片、独立 Hybrid 总线故障恢复和严格历史三方基线。

## Deferred

- [ ] 100,000 项性能矩阵、网络往返/连接池指标和 Provider 内部并发指标。

## 结论

- [x] 标量解码候选方向性验证完成。
- [ ] 分配量预算未改善；不能关闭 FIX-007，也不重复当前候选实验。
