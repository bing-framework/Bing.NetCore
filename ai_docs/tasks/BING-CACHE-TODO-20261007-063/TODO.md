# 本轮 TODO

## 已完成

- [x] 修复 EasyCaching Redis/Hybrid 前缀流丢弃 `CacheValue<T>(null, true)` 的问题。
- [x] 对扫描后已消失的键产出 `Hit=false`，保持 `CacheEntry<T>` 的命中语义完整。
- [x] 新增 null 命中、缺失值和普通键顺序回归测试。
- [x] EasyCaching.Tests 双 TFM 完整回归和 netstandard2.0 生产构建通过。

## 未完成

- [ ] Redis/Hybrid 实机、Cluster、客户端分片和独立总线回归仍需外部环境。
- [ ] 正式性能预算、100,000 项矩阵和三方 baseline 沿用权威索引状态，本轮不重复运行。

## 状态分类

- `OPEN_ACTIONABLE`：无。
- `BLOCKED_EXTERNAL`：真实 Redis/Cluster/分片/总线门禁。
- `VERIFIED_BOUNDARY`：正式性能预算和 100,000 项超时边界。
