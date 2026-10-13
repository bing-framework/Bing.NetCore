# BING-CACHE-TODO-20261007-084：独立 EasyCaching 总线门禁与 TODO 状态校正

## 目标

补齐独立 EasyCaching Redis 总线的真实失效、暂停、重连和恢复证据，并依据现有任务 034 的当前源码双进程矩阵拆分权威索引中的混合状态。任务只运行既有门禁，不修改生产源码或测试逻辑。

## 范围

- 使用两个隔离 Redis 7.4 单节点：一个数据端、一个总线端，端口与现有 `redis:6379` 不重叠。
- 运行 `EasyHybridBusReconnectIntegrationTest`，覆盖订阅连接断开、总线暂停、发布失败传播、重连和恢复后的跨实例失效。
- 在 net6.0、net8.0 各运行一次；失败时保留环境日志并停止收口。
- 依据任务 `BING-CACHE-TODO-20261006-034` 的执行记录，将双进程回源与独立总线从混合 TODO 拆成独立状态。
- 保留需要两个数据分片同时重启的组合故障为外部环境边界，不用单数据节点结果替代。

## 验收

- 四个 Hybrid 总线重连/暂停/恢复测试在两个目标框架通过。
- 测试确实使用不同数据端和总线端，且没有执行 `FLUSHDB`。
- 临时 Redis 容器和网络资源清理完成，现有服务不受影响。
- 更新 `execution.md`、`test-mapping.md`、`TODO.md` 和权威索引；任务收口为 `COMPLETED`。

## Change Impact Analysis

- ChangedFiles：084 任务文档和缓存 TODO 权威索引；生产源码不变。
- ChangedProjects：`Bing.EasyCaching.Tests` 集成门禁运行时引用。
- ChangedPublicContracts：无。
- ChangedRuntimePaths：EasyCaching Hybrid 独立数据 Redis、独立 Redis 总线、订阅重连和发布失败路径。
- ChangedProviders：EasyCaching Hybrid。
- ChangedTFMs：net6.0、net8.0。
- ChangedBuildPackaging：无。
- ChangedBenchmarkHarness：无。
- ChangedDocsOnly：任务报告、测试映射和权威索引。
- AffectedDependents：无生产消费者；仅影响集成测试证据分类。
- RiskLevel：MEDIUM。
