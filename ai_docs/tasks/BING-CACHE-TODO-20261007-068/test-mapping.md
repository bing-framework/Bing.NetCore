# 生产符号到验证方法

| 生产符号/文档契约 | 验证方法 | 结果 |
| --- | --- | --- |
| `CachingServiceCollectionExtensions.AddMemoryCaching` | `framework/src/Bing.EasyCaching/Bing/EasyCaching/CachingServiceCollectionExtensions.cs` 注册入口核对 | 通过 |
| `CachingServiceCollectionExtensions.AddRedisCaching` | Redis 注册入口和 README 示例核对 | 通过 |
| `CachingServiceCollectionExtensions.AddHybridCaching` | Hybrid 注册入口、总线和 README 示例核对 | 通过 |
| `CachingServiceCollectionExtensions.AddShardedRedisCaching` / `AddShardedHybridCaching` | 分片入口、`ExpectedShardCount` 约束和 README 示例核对 | 通过 |
| 当前文档中的缓存注册结论 | 当前文档静态搜索，无过时“整段注释/不可用”结论 | 通过 |
