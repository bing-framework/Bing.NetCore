# 生产符号到测试方法

| 生产符号 | 测试方法 |
| --- | --- |
| `CacheInputLimits`、`CacheConfiguration.InputLimits/WithInputLimits` | `Bing.Caching.Tests.CacheConfigurationAndKeyFormatTest.InputLimits_DefaultToUnlimited_AndCanBeAppliedWithoutChangingConfigurationIdentity`、`InputLimits_RejectNegativeValuesAndNullReplacement` |
| `CacheKeyFormat.GetPhysicalKey/GetPhysicalKeys/GetLeaseKey` | `Bing.Caching.Tests.CacheConfigurationAndKeyFormatTest.InputLimits_CountUtf16CodeUnits_AndAllowTheExactBoundary`、`InputLimits_ApplyToBatchKeysAndLeaseKeys`、`InputLimits_DefaultCompatibilityAllowsLongLogicalKeys` |
| FreeRedis 前缀、模式和流式入口 | `Bing.Caching.FreeRedis.Tests.NamespaceAndCoordinationTest.InputLimits_RejectOverlongKeyPrefixAndPatternBeforeRedisCommands`（net6.0/net8.0，单节点 Redis） |
| CSRedis 前缀、模式和流式入口 | `Bing.Caching.CSRedis.Tests.NamespaceIsolationTest.InputLimits_RejectOverlongKeyPrefixAndPatternBeforeRedisCommands`（net6.0/net8.0，单节点 Redis） |
| EasyCaching Memory/Redis/Hybrid 前缀、模式和流式入口 | `Bing.EasyCaching.Tests.CacheManagerTests.InputLimits_RejectOverlongPrefixAndPatternAcrossCacheManagerEntrypoints`（net6.0/net8.0） |
| EasyCaching 客户端分片配置克隆 | `Bing.EasyCaching.Tests.EasyShardedIntegrationTest` 现有分片配置回归；实现检查确认 `BuildShards` 和混合分片工厂均调用 `WithInputLimits` |
