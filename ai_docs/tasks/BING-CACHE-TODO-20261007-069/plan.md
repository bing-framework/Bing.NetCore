# Redis 扫描工作量预算计划

## 目标

补齐扫描资源保护：现有 `MaxScanKeys` 只限制通过物理键校验后接受的候选数量，无法限制原生 Redis `SCAN` 已经返回的分页数量。增加可选的原生扫描页预算，并在 FreeRedis、CSRedis 的同步、异步、前缀流和删除/清理扫描路径统一执行。

## 设计

- `CacheResourceOptions` 增加 `MaxScanPages`，默认 `0` 表示不限制，保持已有构造调用兼容。
- `CacheScanBudget` 同时记录候选键和扫描页；任一预算超限都抛出 `CacheScanLimitExceededException`，异常明确指出限制类型。
- 原生 Redis `SCAN` 每收到一页结果就消耗一个页预算，取消令牌和拓扑完整性校验保持不变。
- EasyCaching 的 `IDatabase.Keys/KeysAsync` 和自定义 Provider 键流没有公开底层分页边界，本轮不伪造页数；继续由 `MaxScanKeys` 保护已接受候选，隐藏分页观测另列 Deferred。

## 非目标

- 不改变默认扫描行为、命名空间过滤、批大小、Cluster/分片路由或 Clear 非原子语义。
- 不把页预算误报为网络请求数，也不修改 EasyCaching Provider API。

## 验收

1. `MaxScanPages=0` 保持现有行为；负数配置被拒绝。
2. FreeRedis/CSRedis 原生同步和异步扫描在页预算耗尽前不发送下一页 SCAN 命令。
3. 前缀流、模式/命名空间清理和批量删除共享同一页预算，部分结果和异常类型明确。
4. 现有 `MaxScanKeys` 测试和所有受影响 Provider 测试保持通过。
5. netstandard2.0 构建、UTF-8、`git diff --check` 通过。
