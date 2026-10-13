# TODO 状态

## Completed

- [x] 前缀输入拒绝孤立高/低 UTF-16 代理项。
- [x] 模式输入拒绝孤立高/低 UTF-16 代理项。
- [x] 合法代理对、Unicode、通配符和转义语义保持兼容。
- [x] 完成 net6.0/net8.0 精准及完整测试、四个生产构建。

## Open Actionable

- none

## Blocked Approval

- none for this task.

## Blocked External

- 真实 Redis 单节点、Cluster、客户端分片、Hybrid 总线和性能矩阵没有在本轮执行；当前环境无 Redis 监听端点，Docker API 也不可访问。

## Not Applicable

- 本任务不新增公共 API、配置或数据迁移。
- 本任务不改变 Redis 协议、物理键格式、租约、拓扑路由或 SQL 内部缓存。

## Accepted Limitations

- 非法 UTF-16 前缀/模式不再被容错编码；调用方必须使用有效 Unicode 或显式业务编码。

## Verified Boundaries

- 孤立代理项在 URI 编码、扫描模式生成和 Redis 命令派发前失败。

## Deferred

- 历史性能预算、严格三方 baseline、真实网络/连接池指标及多拓扑故障证据继续由前序任务跟踪。

当前任务 `OPEN_ACTIONABLE = 0`，下一动作：STOP。
