# 缓存流式取消命令证据闭环计划

## 目标

把当前已经存在的流式取消测试转换为可复核的运行证据，重点确认消费第一批后取消不会继续派发下一批 MGET 或 SCAN。该轮不改变生产代码；若测试暴露本地可修复问题，再只修复对应路径。

## 实施范围

1. 在非生产单节点 Redis 上运行 FreeRedis 与 CSRedis 的 `CommandCancellationTest`、`PrefixScanCancellationTest`，由 Redis MONITOR 观察实际命令数量。
2. 运行 EasyCaching 的缓存管理器单元/契约测试，确认 mock 批次取消、异常传播、Hybrid 批量一致性和特殊键流式路径。
3. 受影响测试项目分别执行 net6.0/net8.0 精准构建或测试；集成环境不存在时明确记录 `BLOCKED_EXTERNAL`，不把跳过当成通过。
4. 更新执行记录、TODO 和生产符号到测试方法映射，保留性能预算、Cluster/分片和独立总线等外部 TODO。

## 验收标准

- FreeRedis、CSRedis MONITOR 测试若运行，取消后的同命名空间 MGET/SCAN 数量保持首批数量，不出现后续派发。
- EasyCaching 单元测试覆盖第一批后取消、第二批异常、Hybrid 批量一致性和特殊前缀流。
- 所有报告区分 `PASS`、`SKIP/NOT_VERIFIED` 和 `BLOCKED_EXTERNAL`；不以没有配置端点的跳过结果替代真实命令证据。
- `git diff --check` 通过，未进行提交、推送或发布。

## 不在本轮范围

- 不创建或重启 Redis Cluster、客户端分片或独立总线环境。
- 不重新跑历史三方性能基线、100k 性能矩阵或修改性能预算。
- 不修改公共缓存 API、键格式、租约语义或批处理实现。
