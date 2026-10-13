# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试或证据 |
| --- | --- | --- |
| `CacheKeyFormat.HashContext.ComputeHashTag` | 哈希标签长度、十六进制格式和结果独立性 | `CacheConfigurationAndKeyFormatTest.PhysicalKeys_BatchResultsRemainStableAfterLaterGeneration`；`PhysicalKey_RemainsByteForByteCompatibleWithOriginalV2Format` |
| `CacheKeyFormat.FormatPhysicalKey` | 单键/批量格式一致，命名空间与特殊字符行为不变 | `PhysicalKeys_BatchMatchesSingleKeyFormat`、`PhysicalKey_Isolated_AndRoundTripsSpecialCharacters`、`InputLimits_ApplyToBatchKeysAndLeaseKeys` |
| `CacheKeyFormat.GetLeaseKey` | 租约键仍由同一物理数据键追加 `:lease` 生成 | `PhysicalKey_Isolated_AndRoundTripsSpecialCharacters` |
