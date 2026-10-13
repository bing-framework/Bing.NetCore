# 生产符号到性能探针映射

| 生产符号 | 行为 | 探针 | 状态 |
| --- | --- | --- | --- |
| `FreeRedisCacheManager.ReadValuesBatchAsync` | 单节点 List 批量读数组复用后的延迟/分配 | `CacheBench` `freeredis`，`batchRead`、`scan`、`allocationComponents` | 方向性已完成；未形成预算收益 |
| `CSRedisCacheManager.ReadValuesBatchAsync` | 单节点 List 批量读数组复用后的延迟/分配 | `CacheBench` `csredis`，`batchRead`、`scan`、`allocationComponents` | 方向性已完成；未形成预算收益 |
