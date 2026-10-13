# 生产符号到测试映射

| 生产符号/行为 | 测试方法或探针 | 证据边界 |
| --- | --- | --- |
| FreeRedis/CSRedis 流式取消停止后续命令 | `CommandCancellationTest.GetAllStream_CancellationDoesNotDispatchSecondMGet` | `127.0.0.1:6379`，net6.0/net8.0 各 1/1；单节点 |
| FreeRedis/CSRedis 批量、扫描、命名空间、异常传播 | 两个 Provider 集成测试项目完整运行 | FreeRedis 87/87，CSRedis 84/84；分别跳过 Cluster/分片/100k 门控 |
| EasyCaching Redis/Hybrid 批量、流式、租约和失效 | `RedisCoordinatorIntegrationTest` 全类 19 个方法 | net6.0/net8.0 各 19/19；同一单节点 Redis，总线独立节点仍未覆盖 |
| 跨进程同键回源协调 | `Bing.Caching.ProcessProbe` `normal`、`crash-holder`、`timeout-holder`、`lost-lease`、`waiter-timeout` | FreeRedis、CSRedis、EasyCaching Redis 全部通过；随机命名空间；单节点 |
