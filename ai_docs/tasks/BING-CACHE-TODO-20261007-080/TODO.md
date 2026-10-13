# BING-CACHE-TODO-20261007-080 TODO

## 本轮

- [x] 新增 FreeRedis 原生 SCAN 页预算真实 Redis 回归。
- [x] 新增 CSRedis 原生 SCAN 页预算真实 Redis 回归。
- [x] 在 net6.0/net8.0 验证同步、异步、流式、删除和 Clear 路径。
- [x] 清理临时 Redis，确认原有 Redis 服务未受影响。
- [x] 更新执行报告、测试映射和权威 TODO 索引。

## 明确不在本轮

- [~] EasyCaching `Keys/KeysAsync` 隐藏分页仍不纳入 `MaxScanPages`，保持 `MaxScanKeys` 保护和 `DEFERRED` 状态。
- [~] 正式性能预算、100,000 项矩阵、双进程回源和独立总线故障不属于本轮。
