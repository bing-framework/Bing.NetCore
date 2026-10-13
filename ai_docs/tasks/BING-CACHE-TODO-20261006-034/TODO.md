# 本轮 TODO

## Open Actionable

- [ ] FIX-007：批量读取和扫描 P95、分配量性能预算仍未闭环；本轮不运行正式性能矩阵。

## Completed

- [x] 使用当前源码和本机隔离 Redis 重跑三个 Provider 的双进程回源矩阵（结果见 execution.md）。
- [x] 正常合并、异常持有者接管、超时旧回源拒写、失租拒写和等待超时均符合预期；T02/T03 当前源码证据缺口关闭。

## Blocked External / Deferred

- [ ] 三主三从 Cluster、双节点分片故障矩阵仍需专用外部环境。
- [ ] 100,000 项跨拓扑一致性、网络排队指标和严格三方性能基线仍未完成。
