# 槽位计算生产符号到测试映射

| 生产符号/路径 | 关键行为 | 测试/证据 | 状态 |
| --- | --- | --- | --- |
| `CacheClusterTopology.GetHashSlot` | Redis hash-tag、ASCII/Unicode/代理项 UTF-8 编码和 CRC16 槽位 | `framework/tests/Bing.Caching.Tests/CacheClusterTopologyTest.cs`：`GetHashSlot_UsesRedisHashTagAndKnownCrcValue`、`GetHashSlot_MatchesUtf8ReferenceForUnicodeAndInvalidUtf16` | net6/net8 各 12/12 通过 |
| `FreeRedisCacheManager.Stream.ClusterGroups` | Cluster 跨槽批量分组使用共享槽位规则 | `framework/src/Bing.Caching.FreeRedis/.../FreeRedisCacheManager.Stream.cs` 与共享拓扑实现；Provider 精准/集成测试沿用权威索引 | 代码完成，真实 Cluster 外部门禁 |
| `FreeRedisCacheManager.Coordination.GroupPhysicalDeleteKeys` | 租约键删除按数据键槽位分组 | `framework/src/Bing.Caching.FreeRedis/.../FreeRedisCacheManager.Coordination.cs`；共享槽位等价性测试 | 代码完成，真实 Cluster 外部门禁 |
| `CSRedisCacheManager` 分片默认路由和删除分组 | 客户端分片路由、Cluster 删除分组使用共享槽位规则 | `framework/src/Bing.Caching.CSRedis/.../CSRedisCacheManager.cs`；共享槽位等价性测试 | 代码完成，双节点实机外部门禁 |
| `CSRedisCacheManager.Stream.ClusterGroups` | Cluster 跨槽批量分组使用共享槽位规则 | `framework/src/Bing.Caching.CSRedis/.../CSRedisCacheManager.Stream.cs`；共享槽位等价性测试 | 代码完成，真实 Cluster 外部门禁 |
| 三个受影响生产项目 | netstandard2.0 编译兼容性 | `dotnet build` Bing.Caching、Bing.Caching.FreeRedis、Bing.Caching.CSRedis | 0 警告/0 错误 |
