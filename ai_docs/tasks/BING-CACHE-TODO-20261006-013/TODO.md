# 本轮 TODO

## Completed

- [x] EasyCaching 前缀流首批取消后的批量 Provider 调用计数。
- [x] 更新执行记录和生产符号测试映射。

## Open Actionable

- none。本轮没有新的本地可执行项。

## Blocked Approval

- none。

## Blocked External

- none。本轮使用本地 mock，不依赖 Redis。

## Not Applicable

- Cluster、客户端分片、总线、100k 性能矩阵和历史 baseline 不属于本轮范围。

## Accepted Limitations

- mock 只能证明 CacheManager 的批次派发契约，不能证明 EasyCaching Provider 内部网络请求数。

## Verified Boundaries

- [x] EasyCaching 首批前缀读取后取消，后续 `MoveNextAsync` 被取消且 `GetAllAsync` 调用次数为 1。

## Deferred

- Hybrid 实机批量一致性、流式部分失败和大规模 List/Stream 对照沿用后续任务。
