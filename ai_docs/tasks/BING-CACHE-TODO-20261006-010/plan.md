# 缓存单节点证据收口

## 目标

- 在当前隔离 Redis `127.0.0.1:6379` 上重新验证仍可执行的缓存 TODO。
- 关闭 FreeRedis/CSRedis 流式取消命令派发、三 Provider 跨进程回源协调和 EasyCaching Redis/Hybrid 单节点批量一致性证据缺口。
- 保留 Cluster、客户端分片、独立总线故障拓扑、历史 baseline 和性能预算的原有边界，不用单节点结果替代外部证据。

## 验收范围

- FreeRedis、CSRedis `CommandCancellationTest`：net6.0/net8.0 各 1/1。
- FreeRedis、CSRedis 单节点完整集成回归：记录通过数和门控跳过数。
- EasyCaching `RedisCoordinatorIntegrationTest`：net6.0/net8.0 全类通过。
- `Bing.Caching.ProcessProbe`：FreeRedis、CSRedis、EasyCaching Redis 五类双进程协调场景通过。
- 更新任务 TODO 和生产符号到测试方法映射；`git diff --check` 通过。

## 非目标

- 不修改缓存生产实现、公共接口、物理键、租约算法或性能探针。
- 不运行或重算已受 No Progress Guard 约束的同类性能基准。
- 不宣称三主三从 Cluster、客户端双分片、独立总线节点恢复或严格三方 baseline 已通过。
