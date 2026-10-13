# 本轮 TODO

## Open Actionable

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算仍未闭环，本轮只处理客户端分片 List 中间对象。

## Completed

- [x] `ShardedRedisCacheManager.GetAllAsync` 按分片直接读取值列表，不再通过 Stream 创建 `CacheEntry<T>`。
- [x] `ShardedRedisCacheManager.GetByPrefixAsync` 复用单分片带共享扫描预算的值列表入口，保留身份校验和取消语义。
- [x] 新增分片批量 List、前缀 List 精准回归测试；net6.0/net8.0 各 `8 passed`。
- [x] EasyCaching 完整测试 net6.0/net8.0 各 `69 passed / 52 skipped / 0 failed`，生产 `netstandard2.0` 构建 `0 warning / 0 error`。
- [x] `git diff --check` 无错误。

## Blocked External

- [ ] 三主三从 Cluster、双节点客户端分片/Hybrid 故障矩阵、历史同构 baseline 和真实资源指标需要专用环境。

## Deferred

- [ ] 100,000 项完整性能矩阵、网络往返、连接池和 Provider 内部排队指标。
