# 本轮 TODO

## 已完成

- [x] EasyCaching 协调删除已接入 `MaxBatchCommandBytes` 预算切分，并在 Cluster 中先分槽再切分。
- [x] 已补同步/异步预算、Cluster 分组和单键超限测试；双 TFM 精准测试各 9/9 通过。
- [x] EasyCaching README 已同步批量协调删除和 Hybrid 批量失效语义。
- [x] `Bing.EasyCaching` `netstandard2.0` 构建和完整 EasyCaching.Tests 双 TFM 回归通过。

## 未完成（继承边界）

## 继承边界

- 三主三从 Cluster、双节点客户端分片、双进程回源和独立 Hybrid 总线仍需外部环境。
- 正式旧版/当前版/优化版性能预算和 100,000 项矩阵沿用权威索引，不因本轮资源切分修复自动宣称通过。
