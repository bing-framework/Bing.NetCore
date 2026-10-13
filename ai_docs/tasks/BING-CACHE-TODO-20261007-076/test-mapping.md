# BING-CACHE-TODO-20261007-076 生产符号到测试方法映射

| 生产符号/行为 | 测试方法 | 测试项目 | 状态 |
| --- | --- | --- | --- |
| `RunShardScansAsync` 首错取消窗口 | `RemoveByPatternAsync_FailureCancelsOtherIssuedShardPromptly` | `Bing.EasyCaching.Tests` | CLOSED |
| 已派发任务观察和原始异常传播 | `RemoveByPatternAsync_PropagatesFailureAfterObservingIssuedShards` | `Bing.EasyCaching.Tests` | CLOSED |
| 有界并发、取消停止后续派发 | `ShardedCleanupConcurrencyUnitTest` 相关测试 | `Bing.EasyCaching.Tests` | REUSE |
