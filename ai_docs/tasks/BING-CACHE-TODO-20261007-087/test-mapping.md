# BING-CACHE-TODO-20261007-087 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `ICacheKeyPageStreamProvider` | 页流接口可被自定义 Provider 实现并保持键页顺序 | `CacheManagerTests.GetByPrefixStream_UsesProviderKeyPageStreamCapabilityBeforeLegacyFallback` | 通过 net6/net8 |
| `CacheManager.EnumerateProviderKeysAsync` | `MaxScanPages` 在页边界阻止继续消费，并记录页级扫描 | `CacheManagerTests.GetByPrefixStream_PageProvider_EnforcesScanPageBudget` | 通过 net6/net8 |
| `CacheManager.EnumerateProviderKeysAsync` | 取消后停止请求下一页 | `CacheManagerTests.GetByPrefixStream_PageProvider_StopsOnCancellation` | 通过 net6/net8 |
| 旧 `ICacheKeyStreamProvider` / `GetAllKeysByPrefixAsync` 回退 | 未实现页流的行为保持兼容 | `CacheManagerTests.GetByPrefixStream_UsesProviderKeyStreamCapabilityBeforeLegacyFallback`、`CacheManagerTests.GetByPrefixStream_EnforcesScanKeyBudget` | 通过 net6/net8 |
