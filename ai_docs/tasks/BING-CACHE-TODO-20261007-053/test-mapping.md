# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试项目 | 测试方法 |
| --- | --- | --- | --- |
| `CacheInputValidator.ValidatePrefix` | 合法代理对通过，孤立代理项以 `prefix` 参数失败 | `Bing.Caching.Tests` | `PrefixAndPattern_AllowWellFormedSurrogatePair`、`PrefixAndPattern_RejectUnpairedSurrogatesBeforeEncoding` |
| `CacheInputValidator.ValidatePattern` | 合法代理对通过，孤立代理项以 `pattern` 参数失败 | `Bing.Caching.Tests` | `PrefixAndPattern_AllowWellFormedSurrogatePair`、`PrefixAndPattern_RejectUnpairedSurrogatesBeforeEncoding` |
| `CacheKeyFormat.GetPrefixPattern` | 合法 Unicode 前缀保留规范 URI 编码 | `Bing.Caching.Tests` | `PrefixPattern_UsesLiteralEncodedPrefixAndKeepsHashValidationHint`、`PrefixAndPattern_AllowWellFormedSurrogatePair` |
| `CachePattern.CreateMatcher` | 模式字面前缀和通配符语义保持不变 | `Bing.Caching.Tests` | `PatternMatcher_ExtractsOnlySafeLiteralPrefix`、`PatternMatcher_IdentifiesLiteralPatterns`、`PrefixAndPattern_AllowWellFormedSurrogatePair` |
| 逻辑键统一边界 | 键、前缀、模式共享 UTF-16 规则 | `Bing.Caching.Tests` | `PhysicalKey_RejectsUnpairedSurrogateWithoutReturningPhysicalKey`、`PrefixAndPattern_RejectUnpairedSurrogatesBeforeEncoding` |

## 验证身份

- TFM：net6.0、net8.0
- 构建：`Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` 的 netstandard2.0 Release
- 环境：本地工作区；未宣称真实 Redis 拓扑证据
