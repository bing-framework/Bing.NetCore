# 本轮 TODO

## Open Actionable

- 无。

## Completed

- [x] Hybrid 原子读取和 Redis 批量读取改用显式循环生成 `RedisKey[]` 和原始值数组。
- [x] 批量负载预算增加索引选择器循环，移除 `Enumerable.Range(...).Select(...)` 校验迭代器。
- [x] EasyCaching `CacheManagerTests` net6.0/net8.0 各 27 项通过；预算边界测试双 TFM 各 4 项通过。
- [x] `Bing.EasyCaching` `netstandard2.0` 生产构建和差异检查通过。

## Blocked External

- [ ] Redis MONITOR、Cluster/分片、独立总线和完整性能基准仍需对应非生产环境。
- [ ] 旧版/当前版/优化版严格三方基线仍缺可重建历史源码。

## Inherited Open TODO

- [ ] 正式批量读/扫描 P95 与分配预算尚未全部达标。
- [ ] 100,000 项完整 List/Stream 一致性与 T02/T03 后跨进程回源回归仍未重新生成。

## Deferred

- [ ] 真实网络往返、连接池排队和客户端内部重试指标。
- [ ] EasyCaching Hybrid 跨实例批量实机矩阵和独立总线故障恢复。
- [ ] 流式中途取消后的服务端命令停止与 Cluster/分片流式故障证据。
