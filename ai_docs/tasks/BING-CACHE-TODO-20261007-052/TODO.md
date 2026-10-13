# TODO 状态

## Completed

- [x] 在共享键入口拒绝孤立高/低 UTF-16 代理项。
- [x] 保持合法代理对、Unicode、特殊字符和物理键格式兼容。
- [x] 覆盖单键、批量键和租约键失败语义。
- [x] 完成 net6.0/net8.0 测试、四个 `netstandard2.0` Release 构建和差异检查。

## Open Actionable

- none

## Blocked Approval

- none for this task.

## Blocked External

- 真实 Redis 单节点、Cluster、客户端分片和 Hybrid 集成环境未提供；沿用前序任务的外部门禁状态，不把本地测试当作替代证据。

## Not Applicable

- 本任务不新增公共 API，不需要 API baseline 或消费者迁移测试。
- 本任务不改变 Redis 协议、拓扑路由、租约实现或 SQL 内部缓存。

## Accepted Limitations

- 非法 UTF-16 逻辑键不再被容错编码；调用方必须传入有效 Unicode 或自行完成业务编码。

## Verified Boundaries

- 未配对代理项在键格式化前失败，不进入哈希、URI 编码或 Redis 命令路径。

## Deferred

- 历史性能基线、P95/分配预算和真实多拓扑证据继续由前序缓存性能任务跟踪；推荐后续任务只在提供 Redis 拓扑后执行。

当前任务 `OPEN_ACTIONABLE = 0`，下一动作：STOP。
