# BING-CACHE-TODO-20261007-078：FreeRedis/CSRedis 双节点分片实机门禁

## 目标

使用本机已有的非生产 Redis 镜像创建两个临时、隔离的 Redis 主节点，运行 FreeRedis 与 CSRedis 客户端分片集成门禁，补齐真实跨节点路由和扫描证据。测试结束后删除临时容器，不触碰现有 Redis 服务。

## 范围

- 启用 `Bing.Caching.FreeRedis.Tests.Integration/ShardedCacheTest`，验证双节点扫描、Clear、批量写入/读取、List/Stream/PrefixStream 一致性、特殊序列化和单节点不可用边界。
- 启用 `Bing.Caching.CSRedis.Tests.Integration/ShardedCacheTest`，验证双节点扫描、Clear、批量写入/读取、List/Stream/PrefixStream、一致路由和单节点不可用边界。
- 使用进程级临时变量：`RUN_INTEGRATION_TESTS=true`、`ConnectionStrings__RedisConnection=localhost:6390`、`BING_TEST_REDIS_SHARD_CONNECTION=localhost:6391`；不写入用户或系统环境。
- 测试命名空间使用随机后缀；容器使用任务专用精确名称并在最终收口时清理。
- 如果双节点门禁复现同一 `RedisClient` 上的异步批量写入响应错配，则将 FreeRedis 客户端分片 `SetAllAsync` 按路由客户端分组：不同客户端保持 `MaxConcurrency` 有界并发，同一客户端按输入顺序串行写入。
- 如果门禁确认 FreeRedis 1.1.5 的单键 `MGetAsync` 返回形状不稳定，则单键批次统一降级为同客户端 `GET`，多键批次继续使用 `MGET`。

## 不在本任务内

- 不运行 Redis Cluster 三主三从、槽迁移、故障转移或双进程回源。
- 不运行 100,000 项性能矩阵，不改变性能预算、公共 API 或 Provider 注册方式；仅调整 FreeRedis 客户端分片异步批量写入的调度粒度。
- 不把 EasyCaching Hybrid 独立总线证据混入本任务；该项另按专用门禁处理。

## 验收

- 两个临时 Redis 均可连接；两个 Provider 的分片标准测试无失败。
- 测试输出记录各 TFM 的通过/跳过/失败计数；环境错误归类为 `BLOCKED_EXTERNAL`，生产代码错误才另立修复任务。
- 收口后两个专用容器和端口清理完成，现有 Redis 容器仍运行。
- execution.md 和 test-mapping.md 记录 Change Impact Analysis、测试方法及未覆盖拓扑；权威缓存索引仅更新 FreeRedis/CSRedis 双节点证据。
