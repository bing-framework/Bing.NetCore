# 缓存单节点证据轮 TODO

## Completed

- [x] 确认本机存在非生产 `redis:5.0.12` 单节点容器并映射 `localhost:6379`。
- [x] FreeRedis 单节点批量、扫描、TTL、命名空间、租约、特殊键和 10,000 项流式一致性已实际运行。
- [x] CSRedis 单节点批量、扫描、TTL、命名空间、租约、特殊键和 10,000 项流式一致性已实际运行。
- [x] EasyCaching Redis/Hybrid 单节点协调、批量、扫描和 10,000 项流式一致性已实际运行。
- [x] FreeRedis/CSRedis 删除扫描批次完成后增加取消令牌再检查。
- [x] FreeRedis/CSRedis Release netstandard2.0 构建通过，`git diff --check` 通过。

## Open Actionable

- [ ] 为 FreeRedis/CSRedis 删除取消集成测试增加测试专用 RESP 响应闸门：挂起首个匹配 `DEL` 的响应，测试在请求已捕获但响应未返回时取消，再释放响应，断言没有第二个 `DEL`。验收：net6/net8 两个 Provider 的该测试稳定通过，且不依赖 MONITOR 连接调度。

## Blocked Approval

- [ ] 无新的审批项。正式性能回退预算沿用既有计划，不在本轮自行调整。

## Blocked External

- [ ] 三主三从 Redis Cluster 的跨槽批量、全节点扫描、槽迁移、主从切换和故障恢复。
- [ ] 两个独立 Redis 客户端分片的跨节点批量、扫描、节点故障和 Hybrid 失效传播。
- [ ] 双进程回源在最新 T02/T03 候选上的持有者退出、续租失败、旧持有者拒写和等待超时。
- [ ] Hybrid 专用 `52800` 数据/总线端点的实机前缀失效和扫描取消。

## Not Applicable

- [ ] 单节点结果不适用于 Cluster 槽位或分片路由结论。
- [ ] 本任务没有旧版/当前版/优化版可比的性能基线采样，不伪造三方对照。

## Accepted Limitations

- [ ] Redis 删除扫描是非原子操作；已经发送的删除命令不承诺撤销，取消只阻止后续派发。
- [ ] 独立 MONITOR 只能观察命令到达，不能作为业务连接响应闸门；当前删除取消测试因此可能受调度竞态影响。

## Verified Boundaries

- [x] 单节点门控测试中 Cluster、分片、专用端口和大规模 100,000 项用例按条件跳过；没有把跳过伪装成通过。
- [x] 10,000 项 List/Stream 一致性在 FreeRedis、CSRedis、EasyCaching 单节点运行通过。

## Deferred

- [ ] 旧版/当前版/优化版完整性能矩阵、P50/P95/P99、命令数、分配量和工作集报告。
- [ ] 1,000/10,000/100,000 项单节点/分片/Cluster 对照基线。
- [ ] 全方案 `dotnet test`；当前计划门禁是受影响项目测试和生产构建。

## No-Progress Check

删除取消测试的失败根因为测试闸门竞态；继续重复运行同一 MONITOR 实验不会产生新证据。下一次执行应先增加响应闸门或明确将该用例移入外部/测试基础设施任务。
