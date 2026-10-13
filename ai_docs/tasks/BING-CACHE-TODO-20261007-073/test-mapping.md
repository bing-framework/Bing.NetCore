# 生产符号到测试方法

| 生产符号 | 测试方法 | 结果 |
| --- | --- | --- |
| FreeRedis `GetAll<T>` 的 ClientSharded 分支 | `Bing.Caching.FreeRedis.Tests.Integration.ShardedCacheTest.TwoShards_BatchSetAndGetAcrossBothNodes` | net6/net8 均发现；因缺少第二节点环境跳过，真实运行 `BLOCKED_EXTERNAL` |
| FreeRedis `ReadScannedBatch<T>` 的 ClientSharded 分支 | `Bing.Caching.FreeRedis.Tests.Integration.ShardedCacheTest.TwoShards_BatchSetAndGetAcrossBothNodes` | net6/net8 均发现；因缺少第二节点环境跳过，真实运行 `BLOCKED_EXTERNAL` |
| CSRedis `GetAll<T>` 的 ClientSharded 分支 | `Bing.Caching.CSRedis.Tests.Integration.ShardedCacheTest.TwoShards_BatchSetAndGetAcrossBothNodes` | net6/net8 均发现；因缺少第二节点环境跳过，真实运行 `BLOCKED_EXTERNAL` |
| CSRedis `ReadScannedBatch<T>` 的 ClientSharded 分支 | `Bing.Caching.CSRedis.Tests.Integration.ShardedCacheTest.TwoShards_PrefixListAndStreamRemainConsistentAcrossBothNodes` | net6/net8 均发现；因缺少第二节点环境跳过，真实运行 `BLOCKED_EXTERNAL` |
| 分片路由窗口和失败观察 | 现有 Provider 流式/命令取消测试、`CacheCommandProbeTest` | 公共命令探针 net8 8/8 通过；复用 072 的有界窗口证据，真实双节点运行仍受环境门控 |

## 计划命令

```text
dotnet build framework/src/Bing.Caching.FreeRedis/Bing.Caching.FreeRedis.csproj --no-restore
dotnet build framework/src/Bing.Caching.CSRedis/Bing.Caching.CSRedis.csproj --no-restore
dotnet test framework/tests/Bing.Caching.FreeRedis.Tests.Integration/Bing.Caching.FreeRedis.Tests.Integration.csproj --no-restore --framework net6.0 --filter FullyQualifiedName~ShardedCacheTest
dotnet test framework/tests/Bing.Caching.FreeRedis.Tests.Integration/Bing.Caching.FreeRedis.Tests.Integration.csproj --no-restore --framework net8.0 --filter FullyQualifiedName~ShardedCacheTest
dotnet test framework/tests/Bing.Caching.CSRedis.Tests.Integration/Bing.Caching.CSRedis.Tests.Integration.csproj --no-restore --framework net6.0 --filter FullyQualifiedName~ShardedCacheTest
dotnet test framework/tests/Bing.Caching.CSRedis.Tests.Integration/Bing.Caching.CSRedis.Tests.Integration.csproj --no-restore --framework net8.0 --filter FullyQualifiedName~ShardedCacheTest
dotnet test framework/tests/Bing.Caching.Tests/Bing.Caching.Tests.csproj --no-restore --framework net8.0 --filter FullyQualifiedName~CacheCommandProbeTest
git diff --check
```

## 运行结果

- FreeRedis/CSRedis 生产项目 `netstandard2.0`：0 警告、0 错误。
- FreeRedis 分片精准测试 net6/net8：各发现 7 项，0 失败、7 跳过。
- CSRedis 分片精准测试 net6/net8：各发现 7 项，0 失败、7 跳过。
- `CacheCommandProbeTest` net8：8/8 通过。
