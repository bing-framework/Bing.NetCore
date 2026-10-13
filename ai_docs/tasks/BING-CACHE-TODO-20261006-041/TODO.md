# 本轮 TODO

## Open Actionable

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算仍未闭环；本轮只处理 EasyCaching 异步 List 的流式中间对象。

## Completed

- [x] EasyCaching `GetAllAsync` 直接按批调用物理批量读取，不再通过 Stream 创建 `CacheEntry<T>`。
- [x] 保留重复键、缺失/default、输入顺序、单次枚举和取消语义。
- [x] EasyCaching net6.0/net8.0 精准及完整测试、生产 `netstandard2.0` Release 构建通过。
- [x] `git diff --check` 无错误。

## Blocked External

- [ ] 三主三从 Cluster、双节点客户端分片/Hybrid 故障矩阵和外部资源指标仍需专用环境。

## Deferred

- [ ] EasyCaching `GetByPrefixAsync` 的扫描 List 快路径、100,000 项正式性能矩阵、网络往返和 Provider 内部排队指标。
