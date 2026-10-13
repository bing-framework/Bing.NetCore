# 生产符号到测试方法映射

| 生产行为 | 测试方法/证据 | 状态 |
|---|---|---|
| FreeRedis 流式 MGET 取消后停止派发 | `Bing.Caching.FreeRedis.Tests.CommandCancellationTest.GetAllStream_CancellationDoesNotDispatchSecondMGet` | net6/net8 通过 |
| FreeRedis 前缀 SCAN 取消后停止派发 | `Bing.Caching.FreeRedis.Tests.PrefixScanCancellationTest.GetByPrefixStream_CancellationStopsFurtherScanCommands` | net6/net8 通过 |
| CSRedis 流式 MGET 取消后停止派发 | `Bing.Caching.CSRedis.Tests.CommandCancellationTest.GetAllStream_CancellationDoesNotDispatchSecondMGet` | net6/net8 通过 |
| CSRedis 前缀 SCAN 取消后停止派发 | `Bing.Caching.CSRedis.Tests.PrefixScanCancellationTest.GetByPrefixStream_CancellationStopsFurtherScanCommands` | net6/net8 通过 |
| FreeRedis/CSRedis List/Stream 一致性与第二批异常 | 各 Provider `BatchContractTest` | net6/net8 通过；100k 门控跳过 |
| EasyCaching mock 批次取消/异常/Hybrid 一致性 | `Bing.EasyCaching.Tests.CacheManagerTests`、`RedisCoordinatorIntegrationTest` | net6/net8 各 46/46 通过 |
