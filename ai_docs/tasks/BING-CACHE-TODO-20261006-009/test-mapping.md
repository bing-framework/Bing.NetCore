# 生产符号到测试映射

| 生产符号/行为 | 测试 |
| --- | --- |
| `NullCache` 全部非流式异步入口的预取消语义及回源委托不执行 | `Bing.Caching.Tests.NullCacheComprehensiveTest.AsyncOperations_WithPreCanceledToken_ShouldReturnCanceledTasksAndSkipRetrievers` |
| `NullCache.GetAllStreamAsync` 两个重载和 `GetByPrefixStreamAsync` 的预取消语义 | `Bing.Caching.Tests.NullCacheComprehensiveTest.StreamOperations_WithPreCanceledToken_ShouldThrowBeforeFirstMoveNext` |
| NullCache 未取消默认空值行为 | `Bing.Caching.Tests.NullCacheComprehensiveTest` 现有异步默认行为测试 |
| 批量响应超预算文档与实现边界 | `CacheBatchAdaptive` 代码审查及迁移文档人工一致性检查 |
