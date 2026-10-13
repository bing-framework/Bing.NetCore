# 生产符号到测试方法映射

| 生产符号 | 关键行为 | 测试项目 | 测试方法 |
| --- | --- | --- | --- |
| `CacheInputValidator.ValidateKey` | 空白、长度和 UTF-16 代理项边界 | `Bing.Caching.Tests` | `PhysicalKey_RejectsUnpairedSurrogateWithoutReturningPhysicalKey`、`PhysicalKeys_RejectsUnpairedSurrogateWithoutReturningPhysicalKeys`、`InputLimits_CountUtf16CodeUnits_AndAllowTheExactBoundary` |
| `CacheKeyFormat.GetPhysicalKey` | 合法代理对生成并可逆解析，非法代理项快速失败 | `Bing.Caching.Tests` | `PhysicalKey_AllowsWellFormedSurrogatePair`、`PhysicalKey_RejectsUnpairedSurrogateWithoutReturningPhysicalKey` |
| `CacheKeyFormat.GetPhysicalKeys` | 批量键保持顺序，非法输入不返回部分结果 | `Bing.Caching.Tests` | `PhysicalKeys_BatchMatchesSingleKeyFormat`、`PhysicalKeys_RejectsUnpairedSurrogateWithoutReturningPhysicalKeys` |
| `CacheKeyFormat.GetLeaseKey` | 租约键复用逻辑键校验 | `Bing.Caching.Tests` | `LeaseKey_RejectsUnpairedSurrogateBeforeFormatting`、`PhysicalKey_Isolated_AndRoundTripsSpecialCharacters` |
| 合法物理键格式 | 版本、命名空间、哈希和转义兼容 | `Bing.Caching.Tests` | `PhysicalKey_RemainsByteForByteCompatibleWithOriginalV2Format`、`TryGetLogicalKeyWithPrefix_UsesEncodedPrefixAndPreservesCanonicalValidation` |

## 验证身份

- TFM：net6.0、net8.0
- 构建：`Bing.Caching`、`Bing.Caching.FreeRedis`、`Bing.Caching.CSRedis`、`Bing.EasyCaching` 的 netstandard2.0 Release
- 环境：本地工作区；未宣称真实 Redis 拓扑证据
