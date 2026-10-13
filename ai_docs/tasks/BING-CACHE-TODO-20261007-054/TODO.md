# TODO 状态

## Completed

- [x] 命名空间拒绝孤立高/低 UTF-16 代理项。
- [x] Provider 名称拒绝孤立高/低 UTF-16 代理项。
- [x] 合法代理对配置与 `U+FFFD` 配置保持物理键隔离。
- [x] 完成 net6.0/net8.0 测试、EasyCaching 回归、四个生产构建。

## Open Actionable

- none

## Blocked Approval

- none for this task.

## Blocked External

- 真实 Redis 单节点、Cluster、客户端分片、Hybrid 总线和性能矩阵没有在本轮执行；当前环境无 Redis 监听端点，Docker API 也不可访问。

## Not Applicable

- 本任务不新增公共 API、配置项或数据迁移。
- 本任务不改变 Redis 协议、物理键格式、租约、拓扑路由或 SQL 内部缓存。

## Accepted Limitations

- 非法 UTF-16 配置现在在构造阶段失败；调用方必须使用有效 Unicode。

## Verified Boundaries

- 配置校验发生在 `PhysicalKeyPrefix` 和 `HashInputPrefix` 派生之前，不进入 URI/UTF-8 替换编码。

## Deferred

- 历史性能预算、严格三方 baseline、真实网络/连接池指标及多拓扑故障证据继续由前序任务跟踪。

当前任务 `OPEN_ACTIONABLE = 0`，下一动作：STOP。
