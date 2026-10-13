# 本轮 TODO

## Open Actionable

- [ ] FIX-007：批量/扫描 P95 与分配预算仍未闭环；本轮只处理单节点 List 批次的临时数组复制，不足以关闭正式性能验收。

## Completed

- [x] 计划、Change Impact Analysis 和无进展边界已落盘。
- [x] FreeRedis/CSRedis 单节点 `GetAllAsync` 在已有 `string[]` 输入时复用数组，List/Stream 结果契约保持不变。
- [x] 受影响 Provider 构建、精准批量契约和共享测试通过（集成测试按门控跳过，详见 execution.md）。

## Blocked External / Deferred

- [ ] 三主三从 Cluster、双节点分片、跨进程回源和正式性能矩阵仍依赖外部环境或新的实质优化证据。
