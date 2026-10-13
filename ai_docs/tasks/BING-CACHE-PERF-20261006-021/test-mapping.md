# 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `FreeRedisCacheManager.FillEntries<T>` | 默认标量缺失/JSON null 转 `default(T)`，保留 Hit | `Bing.Caching.FreeRedis.Tests.BatchContractTest.GetAllAsync_ScalarBatchNullUsesDefaultWithoutLosingHit` | 代码通过；net6/net8 因 Redis 门控跳过 |
| `CSRedisCacheManager.FillEntries<T>` | 默认标量缺失/JSON null 转 `default(T)`，保留 Hit | `Bing.Caching.CSRedis.Tests.BatchContractTest.GetAllAsync_ScalarBatchNullUsesDefaultWithoutLosingHit` | 代码通过；net6/net8 因 Redis 门控跳过 |
| FreeRedis/CSRedis Stream/List 批量路径 | 顺序、重复键、缺失值和 Hit 状态 | 两个 Provider 既有 `GetAllStream_LargeInputMatchesListIncludingDuplicatesAndMissingKeys`、`GetAllStream_10000ItemsMatchesListIncludingDuplicatesMissingOrderAndHits` | 历史同源证据；本轮未重跑 Redis |
| 自定义序列化器分支 | 不受默认标量优化影响 | FreeRedis `GetAllStream_CustomSerializerPreservesPerItemProtocol` | 历史同源证据 |
| 生产构建 | netstandard2.0 API/实现可编译 | `dotnet build` 两个 Provider 项目 `-f netstandard2.0 --no-restore` | PASS |
