<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-CACHE-TODO-20261007-070
AI_EXECUTION_FINISHED_AT: 2026-10-07T10:10:00+08:00
AI_EXECUTION_STARTED_AT: 2026-10-07T00:32:15.121Z

# 实施执行报告

## 执行结论

已完成 Hybrid 失效消息的独立字节预算和有界拆分。`MaxInvalidationMessageBytes=0` 保持旧的单条消息行为；启用正值后，适配器使用实际 EasyCaching serializer 计算消息载荷，按物理键顺序拆分，同一物理键无法容纳时在发布前 fail fast。

## Change Impact Analysis

```text
ChangedFiles: CacheResourceOptions/exception; HybridInvalidationMessageBatcher; HybridCacheManager; sharded Hybrid registration; tests; README and task evidence
ChangedProjects: Bing.Caching, Bing.EasyCaching and their tests
ChangedPublicContracts: CacheResourceOptions adds optional MaxInvalidationMessageBytes; new public message-budget exception
ChangedRuntimePaths: Hybrid coordinator sync/async single-key and batch invalidation callbacks
ChangedProviders: EasyCaching Redis/Hybrid and client-sharded Hybrid registration
ChangedTFMs: production netstandard2.0; tests net6.0/net8.0
ChangedBuildPackaging: no
ChangedBenchmarkHarness: no
ChangedDocsOnly: no
AffectedDependents: callers configuring CacheResourceOptions; callers catching InvalidOperationException for bus/message failures
RiskLevel: MEDIUM
```

## 实现项

- `CacheResourceOptions` 新增末尾可选参数 `MaxInvalidationMessageBytes`，默认 `0`，负数拒绝。
- 新增 `CacheInvalidationMessageLimitExceededException`，报告预算和实际序列化载荷。
- `HybridInvalidationMessageBatcher` 使用真实 serializer 输出计算每条消息大小，保留顺序和重复键。
- 普通 Hybrid 和客户端分片 Hybrid 的同步/异步协调回调统一接入拆分器。
- 文档明确该预算不等价于代理网络帧大小，不提供重试、背压或 outbox。

## 验证结果

- L0：相关调用点静态检查通过；所有 Hybrid 失效发布入口均经过拆分器；改动文件 UTF-8 解码和 `git diff --check` 通过。
- L1：公共资源测试 net6.0/net8.0 各 14/14；Hybrid 拆分器精准测试 net6.0/net8.0 各 5/5。
- L2：`Bing.Caching.Tests` net6.0/net8.0 各 202/202；`Bing.EasyCaching.Tests` net6.0/net8.0 各 92 通过、54 个 Redis/Cluster 门控跳过。
- L3：Bing.Caching、Bing.EasyCaching netstandard2.0 构建均 0 警告、0 错误。测试输出仍有现存 NU1801/NU1603 包源/依赖提示，不影响本轮测试结果。
- L4：未运行解决方案全量回归；本轮公共 API 和 EasyCaching 运行路径已由受影响项目构建及双 TFM 完整单元覆盖。
- L5：未运行容量/性能基准；本轮只限制失效消息载荷，不改变缓存命中或 Redis 数据路径。

## 状态分类

- Completed：独立消息字节预算、实际 serializer 计量、普通/分片 Hybrid 同步异步接入、确定性边界测试和文档。
- Open Actionable：无。
- Blocked Approval：无。本轮采用默认关闭的可选配置，不改变默认消息行为。
- Blocked External：真实 Redis/独立总线拓扑回归仍受环境门禁影响，完整套件中的 54 个相关测试已按既有门控跳过。
- Deferred：消息代理背压、重试、队列上限、outbox 和崩溃恢复；这些会改变失败语义，需另行定义。
- Accepted Limitations：已成功发布的消息不回滚；拆分只限制 serializer 载荷，不承诺网络帧大小或严格一次交付。

## 任务状态

Implementation Status: PASS
Release Status: BLOCKED_EXTERNAL (真实 Redis/总线拓扑未提供)
Open Actionable: none
Next Action: STOP；待提供非生产总线/Redis 后运行跨实例失效回归
