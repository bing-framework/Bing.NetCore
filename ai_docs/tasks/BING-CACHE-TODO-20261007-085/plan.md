# BING-CACHE-TODO-20261007-085：EasyCaching 双分片与独立总线组合故障门禁

## 目标

使用三个隔离 Redis 7.4 单节点，验证 EasyCaching 客户端分片 Hybrid 在独立总线暂停期间两个数据分片同时重启时的失败传播、身份变化检测、资源清理和恢复后跨实例失效。

## 范围

- 数据分片一：`127.0.0.1:6385`。
- 数据分片二：`127.0.0.1:6386`。
- 独立总线：`127.0.0.1:6387`。
- 数据容器使用 `bing-cache-triple-085-0` 和 `bing-cache-triple-085-1`，以满足测试门控的隔离命名检查；总线使用同前缀的独立容器。
- 运行 `EasyShardedHybridTwoNodeBusFaultIntegrationTest`，在 net6.0/net8.0 各运行一次。
- 不修改生产源码或测试逻辑；组合故障失败时保留诊断并停止收口。

## 验收

- `ShardedHybrid_BusPauseOverlapsBothShardRestarts_RejectsPendingUpdateAndRecovers` 在两个目标框架通过。
- 观测到总线暂停期间分布式层提交但发布任务失败、两个分片 `run_id` 变化、旧管理器报告身份错误、重建后失效通知恢复。
- 临时三容器清理完成，现有 Redis/数据库服务不受影响。
- 更新执行报告、测试映射、TODO 和权威索引；任务收口为 `COMPLETED`。

## Change Impact Analysis

- ChangedFiles：085 任务文档和缓存 TODO 权威索引；生产源码不变。
- ChangedProjects：`Bing.EasyCaching.Tests` 集成门禁运行时引用。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching ClientSharded Hybrid、两个数据分片重启、独立总线暂停和重建路径。
- ChangedProviders：EasyCaching Hybrid。
- ChangedTFMs：net6.0、net8.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：任务报告、测试映射和权威索引。
- AffectedDependents：无生产消费者；仅影响集成测试证据分类。
- RiskLevel：HIGH。测试会主动重启任务专用 Redis 容器，但不会访问既有服务。
