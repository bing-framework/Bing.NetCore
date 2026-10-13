# 生产符号到性能探针映射

| 生产符号 | 行为 | 探针 | 状态 |
| --- | --- | --- | --- |
| `FreeRedisCacheManager.DeserializeValues<T>` | 默认标量批量解码分配与延迟 | `CacheBench freeredis`：`batchRead`、`scan`、`allocation-components`；`GetAllAsync_ScalarBatchNullUsesDefaultWithoutLosingHit` | 方向性完成；契约 PASS；无量化分配收益 |
| `CSRedisCacheManager.DeserializeValues<T>` | 默认标量批量解码分配与延迟 | `CacheBench csredis`：`batchRead`、`scan`、`allocation-components`；`GetAllAsync_ScalarBatchNullUsesDefaultWithoutLosingHit` | 方向性完成；契约 PASS；无量化分配收益 |
