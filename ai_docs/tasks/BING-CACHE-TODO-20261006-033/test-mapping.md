# 生产符号到测试方法映射

| 生产符号 | 行为 | 测试 | 状态 |
| --- | --- | --- | --- |
| `FreeRedisCacheManager.ReadValuesBatchAsync` | 单节点批量 List 读取复用数组，值顺序与缺失语义不变 | `Bing.Caching.FreeRedis.Tests.Integration.BatchContractTest.GetAllAsync_*`、`GetAllStream_*` | net6/net8 编译通过；Redis 门控跳过 |
| `CSRedisCacheManager.ReadValuesBatchAsync` | 单节点批量 List 读取复用数组，值顺序与缺失语义不变 | `Bing.Caching.CSRedis.Tests.Integration.BatchContractTest.GetAllAsync_*`、`GetAllStream_*` | net6/net8 编译通过；Redis 门控跳过 |
| 两个 Provider 流式路径 | 前缀、特殊键、取消和大批量 List/Stream 一致性 | 对应 `BatchContractTest`、`CacheSpecialKeyTest` | net6/net8 编译通过；Redis 门控跳过 |
