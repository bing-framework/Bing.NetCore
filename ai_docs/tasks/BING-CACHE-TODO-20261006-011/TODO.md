# 本轮 TODO

## Completed

- [x] 新增不可变输入限制配置并保持旧构造器兼容。
- [x] 统一逻辑键、前缀和模式长度校验。
- [x] 覆盖 FreeRedis、CSRedis、EasyCaching（含客户端分片）入口。
- [x] 补公共配置、键格式和 Provider 边界测试。
- [x] 更新迁移说明、执行记录和生产符号测试映射。

## Open Actionable

- none。本轮没有新的本地可执行项。

## Blocked Approval

- [ ] 是否把既有性能预算提升为正式发布门禁，需要维护者/产品确认。

## Blocked External

- [ ] 三主三从 Cluster、双节点客户端分片、独立总线失效恢复和固定旧候选版本的三方 baseline。

## Not Applicable

- [ ] 本轮不限制批量总项数、网络接收缓冲，不修改 SQL 内部缓存或旧内存管理器。

## Accepted Limitations

- [ ] 输入限制按 UTF-16 `string.Length` 计算；Unicode scalar/grapheme 限制由调用方负责。
- [ ] 默认限制为 0（无限制），长键调用方需显式启用 `WithInputLimits`。

## Verified Boundaries

- [x] 超限键、前缀和模式在本地构造物理键或派发 Provider 命令前抛出 `ArgumentOutOfRangeException`。
- [x] 缺省配置及 0 上限保持兼容行为。

## Deferred

- [ ] 批量总项数、网络接收缓冲、阶段级工作集和真实内部并发观测。
- [ ] 历史 baseline、100k 正式性能矩阵、Cluster/客户端分片全矩阵和独立总线恢复证据。
