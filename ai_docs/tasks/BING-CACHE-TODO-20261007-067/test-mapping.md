# EasyCaching 前缀流回退路径生产符号到测试映射

| 生产符号 | 关键行为 | 测试方法 | 状态 |
| --- | --- | --- | --- |
| `CacheManager.ReadPhysicalBatchAsync<T>` | 逐键回退对每个扫描候选键产出条目，区分 null 命中和缺失 | `CacheManagerTests.GetByPrefixStream_FallbackReportsNullHitAndMissingValue` | net6/net8 通过 |
| `CacheManager.GetByPrefixStreamCoreAsync<T>` | 回退结果保持扫描顺序和批次消费语义 | 同上及既有 `GetByPrefixStream_*` 测试 | net6/net8 通过 |
| EasyCaching Provider 回退能力 | 批量读取返回 null 时使用逐键读取，避免隐式丢项 | 同上，验证批量读取一次探测、逐键读取三次 | net6/net8 通过 |
