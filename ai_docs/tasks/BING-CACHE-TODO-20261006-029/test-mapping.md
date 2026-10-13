# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试方法 |
| --- | --- | --- |
| `Bing.Caching.FreeRedis.FreeRedisCacheManager` | 单节点读写、批量顺序/重复/缺失、字面量特殊前缀、流式结果和 TTL 契约 | `Bing.Caching.FreeRedis.Tests.Provider_SatisfiesSharedContract`；`Bing.Caching.FreeRedis.Tests.BatchContractTest.GetAllAndGetByPrefix_100000Items_ListAndStreamRemainConsistent`（门控跳过时不计通过） |
| `Bing.Caching.CSRedis.CSRedisCacheManager` | 单节点读写、批量顺序/重复/缺失、字面量特殊前缀、流式结果和 TTL 契约 | `Bing.Caching.CSRedis.Tests.Provider_SatisfiesSharedContract`；`Bing.Caching.CSRedis.Tests.BatchContractTest.GetAllAndGetByPrefix_100000Items_ListAndStreamRemainConsistent`（门控跳过时不计通过） |
| `Bing.EasyCaching.RedisCacheManager` | Redis Provider 共享契约、批量读取与流式顺序/重复/缺失、资源限制和协调租约 | `Bing.EasyCaching.Tests.RedisCoordinatorIntegrationTest.Provider_SatisfiesSharedContract`；`RedisBatchReadAndStream_KeepOrderDuplicatesAndMissingConsistent`；`RedisAndHybrid_DifferentKeyFactoriesAndBatchPayloadRespectResourceLimits` |
| `Bing.Caching.CacheContractAssertions` | `*`、`?`、花括号、Unicode 的字面量前缀隔离及相似前缀哨兵 | `CacheContractAssertions.VerifyAsync`（由上述三个 Provider 集成测试调用） |
