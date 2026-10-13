# 缓存命令探针分类与基准观测计划

## 目标

为当前已经接入的 Redis 批量读和扫描探针增加逻辑操作分类，并将分类结果写入基准阶段报告，提升批量/扫描性能问题的归因能力。该能力只记录缓存适配器已包裹的逻辑客户端调用，不宣称真实网络往返、重试、重定向或客户端内部排队。

## 实施范围

1. 在 `Bing.Caching` 内部命令探针增加 `Unknown`、`SingleRead`、`BatchRead`、`Scan` 分类及按分类计数；保留现有 `InvokeAsync(Func<Task<T>>)` 兼容入口。
2. FreeRedis、CSRedis、EasyCaching 的现有探针调用点显式标注分类，不改变命令派发、取消或批处理行为。
3. CacheBench 的阶段原始结果和汇总结果增加分类计数，旧基准字段保持兼容；分类为空时不填充伪造值。
4. 增加探针分类单元测试和当前基准工程构建验证，更新执行记录及生产符号映射。

## 验收标准

- 相同异步调用的总调用数、最大并发和异常传播语义保持不变。
- FreeRedis/CSRedis 的 MGET、单键 Cluster 读取和 SCAN 分别落到对应分类；EasyCaching 原生批量 Redis 读取落到 `BatchRead`。
- 基准报告保留 `networkRequests=-1`，分类字段明确标注为逻辑客户端调用，不替代网络指标。
- `Bing.Caching.Tests` 双 TFM、受影响 Provider 生产构建、CacheBench 当前工程构建和 `git diff --check` 通过。

## 不在本轮范围

- 不新增网络抓包或客户端内部队列计数。
- 不修改批大小、并发上限、缓存键、租约或公共接口。
- 不重跑历史三方性能基线或 100k 多拓扑矩阵。
