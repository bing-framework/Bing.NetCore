# 本轮 TODO

## Open Actionable

- [x] FreeRedis 扫描删除改为按客户端/槽位分组的有界批量删除。
- [x] CSRedis 扫描删除改为按客户端/槽位分组的有界批量删除。
- [x] 为两个 Provider 补充批量删除、租约键、取消和命名空间隔离回归证据。

本轮无剩余本地可执行项。

## Blocked External

- [ ] Redis 单节点、Cluster 和客户端分片实机门控未在当前环境开启。

## Verified Boundary

- [ ] 既有批量读/扫描性能预算失败属于已验证边界；本轮不重复同类实验。

## Deferred

- [ ] 实际网络往返、客户端内部排队和删除批次的新鲜性能矩阵，待本轮结构修改完成且外部环境可用后执行。
