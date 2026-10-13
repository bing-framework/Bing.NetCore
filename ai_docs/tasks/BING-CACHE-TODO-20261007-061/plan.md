# P6/P7 缓存计划证据索引补全

## 目标

将原始缓存计划中的 P6 EasyCaching 接入和 P7 Core 集合缓存能力明确写入当前权威 TODO 索引，使用现有源码与精准测试证据确认其状态，避免后续审计把已完成能力误判为遗漏。

## 范围

- `SingleTypeLocalMemoryBase`/列表缓存的锁范围、快照、并发添加、更新和批量删除语义。
- EasyCaching Memory/Redis/Hybrid 注册、通用 `ClientSharded` 拒绝边界，以及专用分片注册入口。
- 更新 `ai_docs/cache-todo-index.md` 和本任务的 TODO、测试映射、执行记录。

## 非目标

- 不修改生产代码、公共 API、拓扑路由或性能阈值。
- 不重复 Redis 集成、Cluster/分片外部环境或正式性能矩阵。
- 不把通用入口拒绝 `ClientSharded` 解释为分片能力缺失；分片使用专用注册入口。

## 验收标准

1. 索引单独列出 P6/P7，并引用真实源码和测试方法。
2. Core `LocalMemoryCacheBaseTest` 的 net6/net8 结果被记录为 `CLOSED`。
3. EasyCaching 注册边界和已有 Memory/Redis/Hybrid/分片测试被记录为 `CLOSED` 或外部门禁。
4. Markdown 使用 UTF-8，`git diff --check` 通过；无代码测试重跑。

## 变更影响分析

```text
ChangedFiles: ai_docs/cache-todo-index.md；本任务 Markdown
ChangedProjects: 无
ChangedPublicContracts: 无
ChangedRuntimePaths: 无
ChangedProviders: 无
ChangedTFMs: 无
ChangedBuildPackaging: 否
ChangedBenchmarkHarness: 否
ChangedDocsOnly: 是
AffectedDependents: 后续缓存审计和 TODO/Fix 任务
RiskLevel: LOW
```
