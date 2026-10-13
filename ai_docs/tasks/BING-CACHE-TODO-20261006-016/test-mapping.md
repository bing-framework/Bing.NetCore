# 生产符号到测试方法

| 生产符号 | 测试方法 |
| --- | --- |
| `ICache.GetAllStreamAsync` | `Bing.Caching.ContractTests.CacheContractAssertions.VerifyAsync`（由三套 `Provider_SatisfiesSharedContract` 调用） |
| `ICache.GetByPrefixStreamAsync` | `Bing.Caching.ContractTests.CacheContractAssertions.VerifyAsync`（由三套 `Provider_SatisfiesSharedContract` 调用） |
