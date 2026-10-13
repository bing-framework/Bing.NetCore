# P6/P7 生产符号到测试映射

| 能力 | 生产位置 | 测试/证据 | 状态 |
| --- | --- | --- | --- |
| 单键集合缓存锁和 TryGetValue 读取 | `framework/src/Bing.Core/Bing/Caching/SingleTypeLocalMemoryBase.cs` | `framework/tests/Bing.Core.Tests/Caching/LocalMemoryCacheBaseTest.cs`：`SingleType_AddSameKeyConcurrently_ShouldSucceedOnce`、`SingleType_Reader_ShouldReturnSnapshot` | net6/net8 通过 |
| 更新不存在键和批量删除返回值 | 同上 | `SingleType_SetUpdateAndRemoveArray_ShouldReportCorrectResults` | net6/net8 通过 |
| 列表快照、并发去重和顺序 | `framework/src/Bing.Core/Bing/Caching/ListLocalMemoryBase.cs` | `List_AddSameItemConcurrently_ShouldSucceedOnce`、`List_ReaderAllAndRemove_ShouldPreserveOrderAndIsolation` | net6/net8 通过 |
| Memory/Redis/Hybrid 注册 | `framework/src/Bing.EasyCaching/Bing/EasyCaching/CachingServiceCollectionExtensions.cs` | `framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs` 注册和路由测试 | 已覆盖 |
| 通用入口的 ClientSharded 边界 | 同上、`CacheManager.cs` | `ClientShardedTopology_FailsBeforeProviderRegistration` | 已覆盖 |
| 专用客户端分片 Redis/Hybrid 入口 | `AddShardedRedisCaching`、`AddShardedHybridCaching`、`ShardedRedisCacheManager` | `EasyShardedIntegrationTest.cs`、`EasyShardedHybridIntegrationTest.cs`；真实端点由外部门控 | 代码/测试存在，实机外部阻塞 |
