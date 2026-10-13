# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试方法/证据 |
| --- | --- | --- |
| `Bing.Caching.CacheJsonBatch` | JSON 片段池化组装、null/default、Unicode、UTF-8 和空批次 | `Bing.Caching.Tests.CacheJsonBatchTest` 8 项（net6/net8） |
| `Bing.Caching.CacheKeyFormat.GetPhysicalKey` | v2 物理键格式、百分号大写编码、哈希标签、特殊键和命名空间隔离 | `CacheConfigurationAndKeyFormatTest.PhysicalKey_RemainsByteForByteCompatibleWithOriginalV2Format`；`PhysicalKey_Isolated_AndRoundTripsSpecialCharacters`；`TryGetLogicalKey_RequiresCanonicalHashTagAndEscaping` |
| `Bing.Caching.FreeRedis.FreeRedisCacheManager` | 池化 JSON/物理键生成后的批量顺序、缺失、重复、流式一致性 | `Bing.Caching.FreeRedis.Tests.SharedCacheContractTest.Provider_SatisfiesSharedContract`；`BatchContractTest`（net6/net8 单节点） |
| `Bing.Caching.CSRedis.CSRedisCacheManager` | 池化 JSON/物理键生成后的批量顺序、缺失、重复、流式一致性 | `Bing.Caching.CSRedis.Tests.SharedCacheContractTest.Provider_SatisfiesSharedContract`；`BatchContractTest`（net6/net8 单节点） |
| `Bing.EasyCaching.RedisCacheManager` | 共享物理键格式与 Redis 批量流式顺序/缺失语义 | `RedisCoordinatorIntegrationTest.Provider_SatisfiesSharedContract`；`RedisBatchReadAndStream_KeepOrderDuplicatesAndMissingConsistent`（net6/net8） |
| `CacheBench` current/baseline | 同端点批量/扫描 P95 与分配方向性对照 | `baseline-freeredis-1k.json` vs `current-keypool-freeredis-1k.json`；CSRedis 对应 JSON |
