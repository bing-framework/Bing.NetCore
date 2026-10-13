# 本轮 TODO

## Open Actionable

- none；本轮容量预分配实现和精准回归已完成。

## Blocked External

- [ ] EasyCaching 52800 专用端点 MONITOR、完整 Cluster/分片/独立总线矩阵。
- [ ] 可重建的旧版/中间版/优化版同构性能基线。

## Inherited Open TODO

- [ ] PERF-001：正式批量读/扫描 P95 与分配预算仍未全部达标；下一轮需要新的结构性热点证据和同构复测，不能重复已终止的超时矩阵。
- [ ] 100,000 项 FreeRedis/CSRedis 完整 List/Stream 一致性测试仍受大数据门控和资源边界约束。
- [ ] T02/T03 后的跨进程回源协调回归证据尚未重新生成。

## Deferred

- [ ] 真实网络往返、客户端内部队列和连接池指标。
- [ ] 历史三方性能预算仍不能由本轮短基准关闭；当前绝对观测只用于后续同构对照。
