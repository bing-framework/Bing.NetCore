# 本轮 TODO

## Completed

- [x] EasyCaching 流式特殊键与相似命名空间哨兵隔离测试。
- [x] 更新执行记录和生产符号测试映射。

## Open Actionable

- none。本轮计划项已完成。

## Blocked Approval

- none。

## Blocked External

- none。本轮使用本地 mock，不依赖外部 Redis。

## Not Applicable

- Cluster、客户端分片、独立总线、性能基线和 100k 矩阵不属于本轮范围。

## Accepted Limitations

- mock 证明 CacheManager 的逻辑键过滤和批量结果契约，不证明真实 Redis SCAN 的节点遍历行为。

## Verified Boundaries

- [x] mock 流式结果只包含目标逻辑前缀，特殊字符保持原样，哨兵键被排除。

## Deferred

- Hybrid 实机批量一致性、流式部分失败和性能预算沿用前序任务状态。
