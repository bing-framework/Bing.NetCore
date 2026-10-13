# 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `CacheCollectionCount.TryGetCount` | 读取已知集合规模且不枚举输入 | `CacheCollectionCountTest.TryGetCount_ReadsCollectionCountWithoutEnumerating`、`TryGetCount_ReadsReadOnlyCollectionCountWithoutEnumerating`、`TryGetCount_ReturnsFalseForUnknownEnumerable` | PASS net6/net8 |
| `FreeRedisCacheManager.GetAllAsync` | 已知输入容量初始化结果列表 | FreeRedis `BatchContractTest` | PASS net6/net8 |
| `CSRedisCacheManager.GetAllAsync` | 已知输入容量初始化结果列表 | CSRedis `BatchContractTest` | PASS net6/net8 |
| `CacheManager.GetAllAsync` | EasyCaching List API 结果收集 | EasyCaching `CacheManagerTests` | PASS net6/net8 |
