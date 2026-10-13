# 缓存删除取消测试闸门 TODO

## Completed

- [x] 在 `Bing.Test.Shared` 增加 RESP 删除响应闸门，固定首个目标 `DEL` 已到达 Redis 且响应未返回的窗口。
- [x] FreeRedis/CSRedis 取消测试改用闸门，不再依赖独立 MONITOR 观察时序。
- [x] FreeRedis/CSRedis net6.0/net8.0 精准测试各通过，断言取消异常和目标删除命令只派发一次。
- [x] CSRedis 测试代理设置 `poolsize=2`，避免扫描连接占满单连接池导致删除任务无法派发。
- [x] 两个 Provider 完整单节点门控回归通过，Cluster、分片和大规模场景保持明确跳过。
- [x] 共享测试项目及两个集成项目 Release 构建通过，`git diff --check` 通过。

## Open Actionable

- [ ] 当前任务范围内无剩余本地可执行项。

## Blocked Approval

- [ ] 无。未请求调整性能预算，也未执行提交、推送或发布。

## Blocked External

- [ ] 三主三从 Redis Cluster 的跨槽批量、全节点扫描、槽迁移、主从切换和故障恢复。
- [ ] 两个独立 Redis 客户端分片的跨节点批量、扫描、节点故障和 Hybrid 失效传播。
- [ ] 双进程回源在最新实现上的持有者退出、续租失败、旧持有者拒写和等待超时。
- [ ] Hybrid 专用 `52800` 数据/总线端点的实机失效通知和扫描取消。

## Not Applicable

- [ ] 本任务只修改测试基础设施和集成测试，不改变生产缓存公共 API、键格式、租约实现或数据迁移。
- [ ] 未以单节点结果替代 Cluster、分片或跨进程结论。

## Accepted Limitations

- [ ] 删除扫描不是原子操作；已经发送的删除命令不承诺撤销。
- [ ] RESP 闸门属于测试代码，只验证派发边界和异常传播，不把客户端取消误判为 Redis 命令回滚。

## Verified Boundaries

- [x] 测试使用随机命名空间并在 finally 清理，没有执行 `FLUSHDB`。
- [x] FreeRedis 完整单节点回归：net8/net6 各 99 通过、21 跳过、0 失败。
- [x] CSRedis 完整单节点回归：net8/net6 各 97 通过、20 跳过、0 失败。

## Deferred

- [ ] 旧版/当前版/优化版完整性能矩阵、P50/P95/P99、Redis 命令数、分配量、工作集和回源次数。
- [ ] 1,000/10,000/100,000 项在单节点、客户端分片和 Cluster 下的可比基线。
- [ ] 全方案解决方案回归；当前任务门禁仅覆盖受影响项目。

## No-Progress Check

本轮关闭了唯一的本地可执行证据缺口；继续重复 MONITOR 时序实验不会产生新信息。下一轮应优先准备多节点/双进程环境或正式性能采样，不重复修改已通过的单节点测试。
