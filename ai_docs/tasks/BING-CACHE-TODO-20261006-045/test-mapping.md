# 生产符号到测试方法映射

| 生产符号 | 行为 | 测试项目 | 测试方法 |
| --- | --- | --- | --- |
| `CacheManager.GetAllStreamAsync<T>(IEnumerable<string>, CancellationToken)` | EasyCaching 首批消费后取消不会发起第二批 Provider 读取 | `Bing.EasyCaching.Tests` | `CacheManagerTests.GetAllStream_CancellationAfterFirstBatch_DoesNotDispatchNextRead` |
| `FreeRedisCacheManager.GetByPrefixStreamAsync<T>` | 首批消费后取消不会继续执行命名空间 MGET | `Bing.Caching.FreeRedis.Tests.Integration` | `BatchContractTest.GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet` |
| `CSRedisCacheManager.GetByPrefixStreamAsync<T>` | 首批消费后取消不会继续执行命名空间 MGET | `Bing.Caching.CSRedis.Tests.Integration` | `BatchContractTest.GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextMGet` |
