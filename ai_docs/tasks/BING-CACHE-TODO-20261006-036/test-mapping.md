# 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `FreeRedisCacheManager.GetAllAsync<T>` / `GetByPrefixAsync<T>` | 100,000 项 List 顺序、重复、缺失和前缀读取与 Stream 结果一致 | `Bing.Caching.FreeRedis.Tests.Integration.BatchContractTest.GetAllAndGetByPrefix_100000Items_ListAndStreamRemainConsistent` | net6.0/net8.0 PASS |
| `CSRedisCacheManager.GetAllAsync<T>` / `GetByPrefixAsync<T>` | 100,000 项 List 顺序、重复、缺失和前缀读取与 Stream 结果一致 | `Bing.Caching.CSRedis.Tests.Integration.BatchContractTest.GetAllAndGetByPrefix_100000Items_ListAndStreamRemainConsistent` | net6.0/net8.0 PASS |
