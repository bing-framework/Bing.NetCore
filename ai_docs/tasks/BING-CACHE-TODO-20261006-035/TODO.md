# 本轮 TODO

## Verified Boundary

- [ ] FIX-007：正式批量读取/扫描 P95 与分配预算仍未闭环；数组复用方向性探针没有带来可测分配收益，不能关闭该项，也不再重复同类实验。

## Completed

- [x] 针对任务 033 数组复用改动完成同参数 FreeRedis/CSRedis 方向性探针。
- [x] 记录原始 JSON、结构性分配指标和与任务 031 的可比性边界。
- [x] 确认批量分配主成本仍在 MGET/类型解码阶段；不再重复数组复用候选。

## Blocked External / Deferred

- [ ] 正式 2 轮预热/10 轮样本的 1k/10k/100k 矩阵、Cluster/分片和严格三方 baseline 仍未完成；在没有新根因或新拓扑前不重复运行。
