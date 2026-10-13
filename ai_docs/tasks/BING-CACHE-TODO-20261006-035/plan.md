# 批量数组复用方向性性能验证计划

## 目标

验证任务 033 中新增的数组复用是否降低 FreeRedis/CSRedis 单节点 `GetAllAsync` 批量读取的分配和延迟。该实验针对“已有物理键数组被再次复制”这一新根因，与历史 MGET 返回类型、键格式和 JSON 序列化候选不同。

## 实验条件

- 端点：本机隔离 Redis `127.0.0.1:6379`。
- 运行时：当前 `CacheBench`、net8.0、同一工作树。
- 规模：1,000 项；预热 1 轮、正式样本 3 轮、请求并发 8。
- Provider：FreeRedis、CSRedis；开启 `allocation-components` 以记录结构性分配指标。
- 对照：复用任务 031 的同端点/同参数当前版方向性结果；不重建固定历史 baseline，不把小样本方向性结果当作预算验收。

## Change Impact Analysis

ChangedFiles: 仅本任务计划、原始 benchmark JSON、TODO、执行记录和测试映射。
ChangedProjects: 当前 benchmark 工程运行验证；无生产代码变更。
ChangedPublicContracts: 无。
ChangedRuntimePaths: 两个 Provider 单节点 List 批量读探针。
RiskLevel: LOW；随机 benchmark 命名空间，轮次结束由探针清理。

## 验收

- 两个 Provider benchmark 进程正常结束并生成 `status=COMPLETE` JSON。
- 记录 batchRead/scan P95、平均分配和 allocation components；说明与任务 031 的可比性及限制。
- 运行受影响生产构建、共享精准测试和 `git diff --check`。
- FIX-007 只有在完整正式矩阵达标后才能关闭；本轮无论结果如何都保留正式预算 TODO。
