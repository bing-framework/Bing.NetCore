# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试项目 | 测试方法 |
| --- | --- | --- | --- |
| `CacheInputValidator.ValidateWellFormedUtf16` | 共享拒绝孤立代理项 | `Bing.Caching.Tests` | `Configuration_RejectsUnpairedNamespaceAndProviderSurrogates`、`PrefixAndPattern_RejectUnpairedSurrogatesBeforeEncoding`、`PhysicalKey_RejectsUnpairedSurrogateWithoutReturningPhysicalKey` |
| `CacheConfiguration` 构造函数 | namespace/providerName 合法性、异常参数名和派生前置校验 | `Bing.Caching.Tests` | `Configuration_RejectsUnpairedNamespaceAndProviderSurrogates`、`Configuration_AllowsWellFormedSurrogatePairs_AndKeepsPhysicalKeyIsolation` |
| `CacheConfiguration.PhysicalKeyPrefix` | 合法代理对与替换字符配置不碰撞 | `Bing.Caching.Tests` | `Configuration_AllowsWellFormedSurrogatePairs_AndKeepsPhysicalKeyIsolation` |
| `CacheConfiguration.HashInputPrefix` | 合法代理对保留哈希隔离 | `Bing.Caching.Tests` | `Configuration_AllowsWellFormedSurrogatePairs_AndKeepsPhysicalKeyIsolation`、`PhysicalKey_RemainsByteForByteCompatibleWithOriginalV2Format` |

## 验证身份

- TFM：net6.0、net8.0
- 构建：`Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` 的 netstandard2.0 Release
- 环境：本地工作区；未宣称真实 Redis 拓扑证据
