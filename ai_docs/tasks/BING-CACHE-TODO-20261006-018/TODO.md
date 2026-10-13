# 本轮 TODO

## Completed

- [x] 增加单键写入和删除的逻辑命令分类。
- [x] 标注 FreeRedis、CSRedis、EasyCaching 的异步命中读、写入和删除路径。
- [x] 为 CacheBench 命中阶段接入命令探针和分类输出。
- [x] 补充分类单元测试、生产构建、基准构建、smoke 和执行映射。

## Open Actionable

- none。本轮计划项已完成。

## Blocked Approval

- none。

## Blocked External

- none；本轮只使用仓库内代码和可用单节点 smoke。

## Accepted Limitations

- 分类统计已包裹的逻辑客户端调用，不代表 TCP/Redis 网络往返。
- Provider 内部连接池排队、重试、重定向及未包裹的租约内部调用仍保持未知或不计入对应阶段。

## Deferred

- 严格三方性能对照、性能预算和真实网络/客户端资源指标沿用前序任务状态。
