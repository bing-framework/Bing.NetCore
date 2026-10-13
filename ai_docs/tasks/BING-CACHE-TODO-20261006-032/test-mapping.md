# 生产符号到测试方法映射

| 生产符号 | 行为 | 测试 | 状态 |
| --- | --- | --- | --- |
| `CacheKeyFormat.TryMatchLogicalPrefix` | 规范转义前缀直接匹配、UTF-8/哈希校验、特殊键回退 | `CacheConfigurationAndKeyFormatTest.TryMatchLogicalPrefix_*` | 通过 net6/net8 |
| `FreeRedisCacheManager.GetByPrefixAsync` | List 顺序、特殊键、命名空间隔离 | `Bing.Caching.FreeRedis.Tests.Integration.BatchContractTest` / `CacheSpecialKeyTest` | 通过 net6/net8 |
| `CSRedisCacheManager.GetByPrefixAsync` | List 顺序、特殊键、命名空间隔离 | `Bing.Caching.CSRedis.Tests.Integration.BatchContractTest` / `CacheSpecialKeyTest` | 通过 net6/net8 |
