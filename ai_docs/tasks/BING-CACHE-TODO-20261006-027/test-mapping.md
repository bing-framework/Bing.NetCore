# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试方法 |
| --- | --- | --- |
| `ShardedRedisCacheManager.GetByPrefixStreamAsync<T>` | 取消或提前结束后不追加分片身份探测；完整消费保留末尾身份校验 | `ShardedScanCancellationUnitTest.GetByPrefixStream_CancellationSkipsTrailingIdentityProbe`; `GetByPrefixStream_CompleteConsumptionKeepsTrailingIdentityProbe` |
| `ShardedRedisCacheManager.RunScanAsync` | 异步前缀删除取消后不追加分片身份探测 | `ShardedScanCancellationUnitTest.RemoveByPrefixAsync_CancellationSkipsTrailingIdentityProbe` |
| `CacheFactoryExecution.CleanupAsync` | 完整成功的末尾探测有界等待，失败不伪造成功 | `ShardedScanCancellationUnitTest.GetByPrefixStream_CompleteConsumptionKeepsTrailingIdentityProbe`；既有 `CacheFactoryExecutionTest` |
