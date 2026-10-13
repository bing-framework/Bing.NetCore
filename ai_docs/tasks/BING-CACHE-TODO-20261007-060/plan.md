# 流式批量 TODO 证据收口计划

## 目标

验证当前工作区已经存在的流式批量测试，确认消费第一批后取消不会派发后续 Redis/Provider 批次，并确认 List/Stream 大规模顺序、重复、缺失和 null-hit 一致性、第二批失败传播以及 EasyCaching 流式路径的等价行为。若测试实现与当前源码不一致，只做实现该计划所需的最小修复。

## 范围

- FreeRedis：`GetAllStreamAsync`、`GetByPrefixStreamAsync` 中途取消、1,000/10,000 项 List/Stream 对照、第二批反序列化/读取失败。
- CSRedis：同上，包含单节点集成回归。
- EasyCaching Redis/Hybrid：批量流式中途取消、前缀扫描取消和 Provider 批次数断言；继续使用现有门控环境。
- 更新仓库级 `ai_docs/cache-todo-index.md`，仅在测试证据真实通过后关闭对应 TODO。

## 非目标

- 不重复正式性能矩阵、MONITOR/RESP 删除闸门或已通过的单节点正确性实验。
- 不修改命名空间、物理键、租约、默认 TTL、性能预算和拓扑适配。
- 不把 Cluster、客户端分片、双进程或独立总线的 Skip 当成通过。

## 交付物

- 测试通过后更新 `ai_docs/cache-todo-index.md`。
- 本任务 `TODO.md`、`test-mapping.md`、`execution.md`。
- 若发现真实代码缺陷，附最小源码修复和回归证据；否则只更新文档状态。

## 验收标准

1. FreeRedis、CSRedis 的中途取消测试能观察到首批 MGET/SCAN，取消后没有下一批目标命令。
2. EasyCaching mock 测试在第一批之后取消时仅调用一次批量读取；Redis/Hybrid 取消测试保持明确门控。
3. List/Stream 一致性和第二批失败传播测试在可用目标框架通过。
4. 所有结论引用实际测试方法和运行结果；外部环境缺失保留为 `BLOCKED_EXTERNAL`。
5. `git diff --check` 通过，UTF-8 文档可读；不自动提交、推送或发布。

## 变更影响分析

```text
ChangedFiles: 现有测试证据（若需修复）和本任务 Markdown；仓库级 TODO 索引
ChangedProjects: Bing.Caching.FreeRedis.Tests.Integration、Bing.Caching.CSRedis.Tests.Integration、Bing.EasyCaching.Tests（仅测试）
ChangedPublicContracts: 无
ChangedRuntimePaths: 无（除非测试揭示必须修复的既有流式取消缺陷）
ChangedProviders: 无
ChangedTFMs: 无
ChangedBuildPackaging: 否
ChangedBenchmarkHarness: 否
ChangedDocsOnly: 以验证结果为准
RiskLevel: LOW
```

## 验证顺序

1. 先检查测试源码和目标框架是否可构建。
2. 运行 FreeRedis、CSRedis 精准流式测试。
3. 运行 EasyCaching mock 精准测试；若门控变量可用，再运行 Redis/Hybrid 取消测试。
4. 更新索引、任务 TODO、测试映射和执行报告。
5. `git diff --check`，再执行任务状态收口。
