# 本轮 TODO

## Verified Boundary

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算仍未闭环；已有正式数据确认预算边界，本轮不重复同类微优化。

## Completed

- [x] EasyCaching `GetAllStreamAsync` 首批消费后取消只允许首个 Provider 批量读取调用。
- [x] FreeRedis `GetByPrefixStreamAsync` 首批消费后取消通过 `MONITOR` 确认不再执行下一次命名空间 MGET。
- [x] CSRedis `GetByPrefixStreamAsync` 首批消费后取消通过 `MONITOR` 确认不再执行下一次命名空间 MGET。
- [x] 四个受影响测试目标在 net6.0/net8.0 均完成编译；EasyCaching 精准测试通过。

## Blocked External

- [ ] FreeRedis/CSRedis 真实 Redis 环境下运行新增命令监视测试；本轮因集成门控关闭而跳过。
- [ ] 三主三从 Cluster、客户端分片和故障转移中的流式取消命令证据。
- [ ] 历史同构 baseline、100,000 项资源矩阵和真实网络/连接池指标。

## Deferred

- [ ] 独立 `review.md`、完整性能报告和发布门禁。
- [ ] 流式部分失败、特殊键大规模组合和跨进程回源协调的专用矩阵。
