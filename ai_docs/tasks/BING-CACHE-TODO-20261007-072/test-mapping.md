# 生产符号到测试方法

| 生产符号 | 测试方法 | 结果 |
| --- | --- | --- |
| FreeRedis `ReadInputBatchAsync` / `ReadValuesBatchAsync` / `ReadPrefixBatchAsync` 的 ClientSharded 分支 | `Bing.Caching.FreeRedis.Tests.Integration.ShardedCacheTest.TwoShards_BatchSetAndGetAcrossBothNodes`、`TwoShards_BatchStreamAndPrefixStreamRemainConsistent` | net6.0/net8.0 均成功构建并发现 7 项；7 项因未设置双节点环境跳过，真实运行 `BLOCKED_EXTERNAL` |
| CSRedis `FillShardedEntriesAsync` / `ReadShardedValuesAsync` 的 ClientSharded 分支 | `Bing.Caching.CSRedis.Tests.Integration.ShardedCacheTest.TwoShards_BatchSetAndGetAcrossBothNodes`、`TwoShards_PrefixListAndStreamRemainConsistentAcrossBothNodes` | net6.0/net8.0 均成功构建并发现 7 项；7 项因未设置双节点环境跳过，真实运行 `BLOCKED_EXTERNAL` |
| 有界并发任务观察与取消 | `Bing.Caching.FreeRedis.Tests.Integration.CommandCancellationTest`、`Bing.Caching.CSRedis.Tests.Integration.CommandCancellationTest`、Provider 流式第二批失败回归；公共 `CacheCommandProbeTest` | 公共探针 net6.0/net8.0 各 8/8 通过；Redis 命令闸门和双节点回归受环境门控，未以单节点结果替代 |

## 已执行命令

```text
dotnet build framework/src/Bing.Caching.FreeRedis/Bing.Caching.FreeRedis.csproj --no-restore
dotnet build framework/src/Bing.Caching.CSRedis/Bing.Caching.CSRedis.csproj --no-restore
dotnet test framework/tests/Bing.Caching.FreeRedis.Tests.Integration/Bing.Caching.FreeRedis.Tests.Integration.csproj --no-restore --framework net6.0 --filter FullyQualifiedName~ShardedCacheTest
dotnet test framework/tests/Bing.Caching.FreeRedis.Tests.Integration/Bing.Caching.FreeRedis.Tests.Integration.csproj --no-restore --framework net8.0 --filter FullyQualifiedName~ShardedCacheTest
dotnet test framework/tests/Bing.Caching.CSRedis.Tests.Integration/Bing.Caching.CSRedis.Tests.Integration.csproj --no-restore --framework net6.0 --filter FullyQualifiedName~ShardedCacheTest
dotnet test framework/tests/Bing.Caching.CSRedis.Tests.Integration/Bing.Caching.CSRedis.Tests.Integration.csproj --no-restore --framework net8.0 --filter FullyQualifiedName~ShardedCacheTest
dotnet test framework/tests/Bing.Caching.Tests/Bing.Caching.Tests.csproj --no-restore --framework net6.0 --filter FullyQualifiedName~CacheCommandProbeTest
dotnet test framework/tests/Bing.Caching.Tests/Bing.Caching.Tests.csproj --no-restore --framework net8.0 --filter FullyQualifiedName~CacheCommandProbeTest
git diff --check
```

生产两个项目 netstandard2.0 构建均为 0 警告/0 错误；`CacheCommandProbeTest` 两个 TFM 各 8/8 通过；分片测试两套项目两个 TFM 均为 0 失败、7 跳过。
