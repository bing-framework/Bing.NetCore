# 生产符号到测试方法

| 生产符号 | 测试方法 | 结果 |
| --- | --- | --- |
| `CacheScanBudget.Consume` / `ConsumePage` / 分片作用域 | `Bing.Caching.Tests.CacheResourceBoundaryTest.ScanBudget_ConcurrentCandidatesAndPagesRemainWithinLimits` | net6/net8 各 15/15 预算精准测试通过；并发计数不超限并报告失败分片 |
| `ShardedRedisCacheManager.GetByPrefixAsync<T>` | `Bing.EasyCaching.Tests.ShardedScanCancellationUnitTest.GetByPrefixAsync_ReturnsValuesFromAllShardsAndUsesSharedBudget` | net6/net8 精准测试通过；共享预算和双分片结果通过 |
| `ShardedRedisCacheManager.GetByPrefixStreamAsync<T>` | `Bing.EasyCaching.Tests.ShardedScanCancellationUnitTest.GetByPrefixStream_CancellationSkipsTrailingIdentityProbe`、`GetByPrefixStream_CompleteConsumptionKeepsTrailingIdentityProbe` | net6/net8 精准测试各 9/9 通过；交错消费、取消和尾部身份探测通过 |
| 同步 `GetByPrefix<T>` 桥接 | `Bing.EasyCaching.Tests.ShardedScanCancellationUnitTest.GetByPrefix_SharesAsyncShardReadPath` | net6/net8 通过；同步结果复用异步路径且异常不包 AggregateException |
| 真实 Redis/Hybrid 分片行为 | `EasyShardedIntegrationTest.ShardedRedis_CrossNodeBatchStreamScanClearAndRetriever_Work` | net6/net8 各发现 6 项、0 失败、6 跳过；缺少双节点，分类为 `BLOCKED_EXTERNAL` |

## 计划命令

```text
dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --framework net8.0 --filter FullyQualifiedName~ShardedScanCancellationUnitTest
dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --framework net6.0 --filter FullyQualifiedName~ShardedScanCancellationUnitTest
dotnet build framework/src/Bing.Caching/Bing.Caching.csproj --no-restore
dotnet build framework/src/Bing.EasyCaching/Bing.EasyCaching.csproj --no-restore
git diff --check
```

## 运行结果

- `Bing.Caching` netstandard2.0 构建：0 警告、0 错误。
- `Bing.EasyCaching` netstandard2.0 构建：0 警告、0 错误。
- `Bing.Caching.Tests` 完整测试 net6/net8：各 203/203 通过。
- `Bing.EasyCaching.Tests` 完整测试 net6/net8：各 94 通过、54 跳过。
- `ShardedScanCancellationUnitTest` net6/net8：各 9/9 通过。
