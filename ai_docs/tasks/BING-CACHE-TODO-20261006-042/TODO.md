# 本轮 TODO

## Open Actionable

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算仍未闭环；本轮只处理 EasyCaching 前缀 List 中间对象。

## Completed

- [x] EasyCaching `GetByPrefixAsync` 使用物理前缀匹配和直接命中值批处理，不再通过 Stream 创建 `CacheEntry<T>`。
- [x] 保留命名空间/哈希校验、Cluster 主节点清单与末尾拓扑校验、Provider 键流、扫描预算和取消语义。
- [x] 新增前缀 List 精准测试，验证只返回匹配键的批量值、保留 null 命中，以及首批取消后不派发下一次读取。
- [x] EasyCaching net6.0/net8.0 精准测试各 `3 passed`，完整测试各 `67 passed / 52 skipped / 0 failed`，生产 `netstandard2.0` Release 构建通过。
- [x] `git diff --check` 无错误。

## Blocked External

- [ ] 三主三从 Cluster、双节点客户端分片/Hybrid 故障矩阵和外部资源指标仍需专用环境。

## Deferred

- [ ] EasyCaching 100,000 项正式性能矩阵、网络往返、连接池和 Provider 内部排队指标。
