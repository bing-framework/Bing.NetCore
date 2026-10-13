# 本轮 TODO

- [x] FreeRedis/CSRedis 首批取消后的实际 MONITOR 命令数已在上一轮正式运行中确认：net6.0/net8.0 各 1/1，首批读取后只派发 1 个批量命令；本轮复用证据，相关源码未变化。
- [x] ProcessProbe 已在 `127.0.0.1:6379` 运行 `freeredis`、`csredis`、`easyredis` 全部场景：同键合并、持有者退出接管、超时旧回源拒写、失租拒写和等待超时均通过。
- [x] EasyCaching Redis/Hybrid 单节点精准集成已通过：`RedisCoordinatorIntegrationTest` net6.0/net8.0 各 19/19；覆盖同键协调、显式写删撤销、Hybrid 两实例失效传播、总线失败报告和流式批量边界。
- [x] FreeRedis/CSRedis `CoordinationBoundaryTest` 已通过：两个 Provider 的 net6.0/net8.0 各 4/4，覆盖同键合并、失租取消、显式写删撤销、超时后门闸释放。
- [ ] 独立总线节点上的 Hybrid 重连、暂停、总线故障恢复仍需专用端点（当前单节点只验证同节点两实例传播）；状态：`BLOCKED_EXTERNAL`。
- [ ] 历史 baseline checkout、三主三从 Cluster、客户端双分片和完整性能矩阵仍缺外部环境；状态：`BLOCKED_EXTERNAL`，不以单节点证据替代。
