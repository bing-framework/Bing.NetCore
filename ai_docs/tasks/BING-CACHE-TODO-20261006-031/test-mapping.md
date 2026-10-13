# 生产符号到测试方法映射

| 生产符号 | 行为 | 测试 |
| --- | --- | --- |
| FreeRedisCacheManager.GetAllAsync | List 批量顺序、重复、缺失/default | FreeRedis `BatchContractTest` |
| FreeRedisCacheManager.GetByPrefixAsync | 前缀扫描回读和取消 | FreeRedis `BatchContractTest`、`CacheSpecialKeyTest` |
| CSRedisCacheManager.GetAllAsync | List 批量顺序、重复、缺失/default | CSRedis `BatchContractTest` |
| CSRedisCacheManager.GetByPrefixAsync | 前缀扫描回读和取消 | CSRedis `BatchContractTest`、`CacheSpecialKeyTest` |
| FreeRedis/CSRedis List/Stream 快路径对照 | 1k 批量读/扫描 P95 与分配方向 | `current-listfast-freeredis-1k.json`、`current-listfast-csredis-1k.json` |
| EasyCaching CacheManager List/Stream | 重复、缺失、null、首批取消 | `CacheManagerTests.GetAllAsync_ListMatchesStream_ForDuplicatesMissingAndNullValues`、`GetByPrefixStream_CancellationAfterFirstBatch_DoesNotDispatchNextBatch` |
