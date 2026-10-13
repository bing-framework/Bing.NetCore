# 生产符号到测试与性能探针映射

| 生产符号 | 行为 | 验证 | 状态 |
| --- | --- | --- | --- |
| `FreeRedisCacheManager.GetByPrefixAsync<T>` | 扫描 List 批次不再为完整批次复制数组 | `CacheBench freeredis` 的 `scan`；100,000 项 List/Stream 一致性测试 | 方向性完成；无量化收益 |
| `FreeRedisCacheManager.GetAllStreamAsync<T>` | 输入流完整批次数组复用 | `CacheBench freeredis` 的 `batchReadStream`；100,000 项一致性测试 | 方向性完成；无量化收益 |
| `FreeRedisCacheManager.GetByPrefixStreamAsync<T>` | 前缀流完整批次数组复用 | `CacheBench freeredis` 的 `scanStream`；100,000 项一致性测试 | 方向性完成；无量化收益 |
| `CSRedisCacheManager.GetByPrefixAsync<T>` | 扫描 List 批次不再为完整批次复制数组 | `CacheBench csredis` 的 `scan`；100,000 项 List/Stream 一致性测试 | 方向性完成；无量化收益 |
| `CSRedisCacheManager.GetAllStreamAsync<T>` | 输入流完整批次数组复用 | `CacheBench csredis` 的 `batchReadStream`；100,000 项一致性测试 | 方向性完成；无量化收益 |
| `CSRedisCacheManager.GetByPrefixStreamAsync<T>` | 前缀流完整批次数组复用 | `CacheBench csredis` 的 `scanStream`；100,000 项一致性测试 | 方向性完成；无量化收益 |
