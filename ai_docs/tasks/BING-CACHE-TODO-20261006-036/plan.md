# BING-CACHE-TODO-20261006-036：单节点 100,000 项一致性门控

## 目标

关闭当前缓存 TODO 中仍可在本机非生产 Redis 验证的单节点大数据一致性缺口。运行 FreeRedis 与 CSRedis 的 `GetAllAndGetByPrefix_100000Items_ListAndStreamRemainConsistent`，确认当前源码在 100,000 项输入下保持 List/Stream 顺序、重复键、缺失值和前缀结果一致。

本任务是功能证据补齐，不重新尝试已停止的性能微调，也不把大数据正确性结果当作 P95 或分配预算通过。

## 环境与命令约束

- Redis：`127.0.0.1:6379`，仅作为本机隔离测试端点，不执行 `FLUSHDB`。
- 环境门控：`RUN_INTEGRATION_TESTS=true`、`ConnectionStrings__RedisConnection=127.0.0.1:6379`、`BING_TEST_LARGE_CACHE_DATASET=true`、`BING_TEST_CACHE_LARGE_COUNT=100000`。
- TFM：先运行 net8.0；若资源和时间允许，再运行 net6.0。测试必须显示通过，Skip 不计为完成。
- 只筛选两个 Provider 的单个 100,000 项测试，避免无关完整套件和 Cluster/分片门控。

## Change Impact Analysis

- ChangedFiles：本任务计划、TODO、执行记录、测试映射和原始测试日志（不修改生产代码）。
- ChangedProjects：`Bing.Caching.FreeRedis.Tests.Integration`、`Bing.Caching.CSRedis.Tests.Integration`。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：单节点 List/Stream 大数据读取和前缀扫描。
- ChangedProviders：FreeRedis、CSRedis。
- ChangedTFMs：net8.0，必要时 net6.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：任务记录和证据文件。
- AffectedDependents：无生产消费者；仅受影响 Provider 集成测试。
- RiskLevel：MEDIUM，主要风险为本机 Redis 内存、测试耗时和残留键清理。

## 验收

1. 两个 Provider 的测试均以通过结束，不能以 Skip 或门控未启用作为成功。
2. 记录测试 TFM、环境变量、实际 item count、耗时、失败/跳过状态。
3. 测试结束后由测试自身删除唯一命名空间；不执行整库清理。
4. 更新 TODO 和生产符号到测试方法映射；正式性能预算、Cluster/分片和 Hybrid 外部矩阵继续保留原状态。
