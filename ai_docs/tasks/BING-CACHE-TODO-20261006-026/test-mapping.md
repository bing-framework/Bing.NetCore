# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试或证据 |
| --- | --- | --- |
| `GetByPrefixStreamAsync<T>` 三 Provider 公共契约 | 特殊逻辑键按字面量匹配，命中 null 保留，相似前缀哨兵不泄漏 | `CacheContractAssertions.VerifyAsync` 的 `specialPrefix` 流式段；FreeRedis/CSRedis/EasyCaching `SharedCacheContractTest` |
| `CacheManager.GetAllStreamAsync<T>` | 首批结果先产出，第二批失败原样传播，不伪造完整成功 | `CacheManagerTests.GetAllStream_PropagatesSecondBatchFailureAfterFirstBatch` |
| `CacheEntry<T>` | 逻辑键、值和 `Hit` 状态在特殊键/异常前批次中保持可观察 | `CacheContractAssertions.VerifyAsync`、上述 EasyCaching 测试 |
