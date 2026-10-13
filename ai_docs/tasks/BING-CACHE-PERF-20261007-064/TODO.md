# 本轮 TODO

## 已完成

- [x] `RedisLeaseCoordinator` 已增加批量覆盖写入和批量删除；单节点使用一次 Lua/删除命令，Cluster 按槽分组，并使用配置的最大并发派发异步命令。
- [x] `CacheManager` 的协调批量写入、异步写入、批量删除和扫描删除已接入批量协调路径，取消只阻止后续派发并等待已发命令。
- [x] Hybrid 已增加批量本地失效和包含多个键的总线失效消息；单键路径保持原有回调。
- [x] 已补充 `RedisLeaseCoordinatorBatchTest` 和 `CacheManagerBatchCoordinationTest`，覆盖租约键配对、Cluster 分槽、TTL 向上取整、批量失效、取消停止派发及同步/异步路径。
- [x] `Bing.EasyCaching` `netstandard2.0` 构建、EasyCaching.Tests net6.0/net8.0 完整门控、批量精准测试和 `git diff --check` 已完成。

## 未完成（继承边界）

- 三主三从 Cluster、双节点客户端分片、双进程回源和独立 Hybrid 总线仍需外部环境。
- 正式性能预算和 100,000 项矩阵沿用权威索引，不因本地结构优化自动宣称通过。
