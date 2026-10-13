# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试或证据 |
| --- | --- | --- |
| `CacheKeyFormat.TryGetLogicalKey` ASCII 分支 | 规范 ASCII 物理键可 round-trip，后续生成不改变结果 | `CacheConfigurationAndKeyFormatTest.TryGetLogicalKey_RoundTripsCanonicalAsciiWithoutChangingPhysicalFormat` |
| `CacheKeyFormat.TryGetLogicalKey` canonical 校验 | 小写百分号、过度编码、特殊字符和 Unicode 保持原有接受/拒绝语义 | `TryGetLogicalKey_RequiresCanonicalHashTagAndEscaping`、`PhysicalKey_Isolated_AndRoundTripsSpecialCharacters` |
| `CacheKeyFormat.TryGetLogicalKey` 隔离 | 相似命名空间及租约键不被还原为数据键 | `TryGetLogicalKey_RejectsSimilarNamespaceAndLeaseKeys` |
