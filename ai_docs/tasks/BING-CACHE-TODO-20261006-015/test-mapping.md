# 生产符号到测试方法

| 生产符号 | 测试方法 |
| --- | --- |
| `Bing.Caching.FreeRedis.FreeRedisCacheManager.GetByPrefixStreamAsync` | `Bing.Caching.FreeRedis.Tests.BatchContractTest.GetByPrefixStream_PropagatesSecondBatchFailureAfterFirstBatch` |
| `Bing.Caching.CSRedis.CSRedisCacheManager.GetByPrefixStreamAsync` | `Bing.Caching.CSRedis.Tests.BatchContractTest.GetByPrefixStream_PropagatesSecondBatchFailureAfterFirstBatch` |
| `Bing.EasyCaching.CacheManager.GetByPrefixStreamAsync` | `Bing.EasyCaching.Tests.CacheManagerTests.GetByPrefixStream_PropagatesSecondBatchFailureAfterFirstBatch` |
