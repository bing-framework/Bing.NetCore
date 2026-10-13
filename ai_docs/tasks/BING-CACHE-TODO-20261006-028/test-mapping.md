# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试方法 |
| --- | --- | --- |
| `ShardedRedisCacheManager.RunScan` | 同步分片扫描失败保留主异常，失败后不追加身份探测；完整成功仍校验 | `ShardedScanCancellationUnitTest.RemoveByPrefix_SyncFailurePreservesPrimaryExceptionAndSkipsTrailingIdentityProbe` |
