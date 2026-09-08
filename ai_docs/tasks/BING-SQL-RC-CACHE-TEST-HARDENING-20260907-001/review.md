<!-- AI_REVIEW_STATUS: PASS -->
AI_TASK_ID: BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001
AI_REVIEWED_AT: 2026-09-08T08:34:00.4013373+08:00

# 第三轮独立代码审查报告

## 审查结论

`PASS`。

第三轮对照 `plan.md`、`execution.md`、真实源码、测试和 Git Diff 复核后，上一轮 `FIX-004`、`FIX-005` 均已闭环，未发现新的 `MUST_FIX` 或 `SHOULD_FIX`。Mutation Plan 十个键维度已按同一 baseline 做真实缓存单变量隔离；实体映射最终 Database、Schema、TableName 已有独立 lookup 与统计证明；模型元数据缓存的陈旧 Lazy 条件删除及同键最终映射单次构造也已形成直接、确定性的测试合同。

## Findings

无。

## 上一轮 FIX 复核

| 原 FIX | 结果 | 第三轮证据 |
| --- | --- | --- |
| FIX-004 | PASS | `GetOrAdd_WhenEachPlanDimensionChanges_ShouldMissAndKeepEquivalentPlanHit` 的每个 variation 显式保留其余 baseline 字段，并逐次断言 miss；`Resolve_WhenFinalObjectNameDimensionChangesIndependently_ShouldMissEachTime` 分别只改变最终 Database、Schema、TableName。 |
| FIX-005 | PASS | `RemoveModelMetadataCacheEntryIfCurrent_WhenValueIsStale_ShouldKeepCurrentValue` 直接证明陈旧 Lazy 不删除当前值；`Resolve_WhenSameMappingIsResolvedConcurrently_ShouldCreateFinalMappingOnce` 使用确定性起跑并断言最终映射仅构造一次。无上限 miss 路径在准入锁内二次查找，命中路径保持无锁。 |

## 计划验收矩阵

| 计划项 | 结果 | 审查说明 |
| --- | --- | --- |
| CACHE-001 | PASS | 最终生产符号、关键行为、测试项目和方法名已在追溯文档中维护。 |
| CACHE-101 | PASS | 映射键规范化、八个隔离维度、最终对象名和 hit/miss/entry count 均有真实 resolver 证据。 |
| CACHE-102 | PASS | 成功、失败、共享失败、重试、陈旧条件删除及同键并发构造合同均已覆盖。 |
| CACHE-201 | PASS | `BoundedLazyCacheTest` 直接覆盖容量、旁路、LRU、统计、条件删除、异常和并发。 |
| CACHE-202 | PASS | Mutation Plan 十维单变量隔离、等价命中以及真实 Builder 完整 SQL/参数隔离已覆盖。 |
| CACHE-203 | PASS | Getter 键、容量、并发、异常恢复、resolver 分区及 first-writer-wins 行为均有直接测试。 |
| CACHE-301 | DEVIATED_OK | Query 生产路径未变；既有生命周期测试已覆盖缓存命中、失效、失败原子性和重复渲染，未机械新增重复用例。 |
| CACHE-401 | PASS | 追溯矩阵已同步；生产映射构造路径的最小修复沿用现有 Benchmark 场景，execution 已如实记录本环境未生成新的正式性能数字。 |
| CACHE-402 | PASS | 专项、并发三轮、全量双 TFM、Release build 和 diff 检查均有完成证据。 |

## Reviewer 独立验证

执行：

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EntityMappingCacheIsolationTest|FullyQualifiedName~SqlMutationPlanCacheTest|FullyQualifiedName~BoundedLazyCacheTest" --logger "console;verbosity=minimal"
```

结果：

- net8.0：37 passed，0 failed，0 skipped。
- net6.0：37 passed，0 failed，0 skipped。
- Review Fix round 2 已记录：缓存/Query 专项两个 TFM 各 147 passed；并发筛选连续 3 轮各 12 passed；全量两个 TFM 各 1307 passed；Release build 0 errors、117 个既有 warning。
- `git diff --check`：通过。
- 未运行外部 Provider Integration；本任务未修改 Provider、连接、执行器或 Query/Executor 创建链，符合计划范围和门控规则。

## 正向结论

- `RemoveModelMetadataCacheEntryIfCurrent` 通过键和值的原子条件删除，避免迟到的失败调用移除已经发布的新 Lazy。
- 无上限最终映射缓存仅串行化 cold miss 的构造与发布；首次 lookup 命中仍不进入准入锁，符合本任务的正确性与热路径边界。
- 新增测试使用 `CountdownEvent`、启动事件和 `LongRunning` worker 构造真实竞争，不依赖线程池偶然调度。
- SQL 输出相关行为测试继续断言完整 SQL 和参数快照，没有以 `Contains` 片段替代合同。
- 未新增 Public API、数据库配置、CI 配置或生产 friend assembly。

## 剩余风险

- 当前环境没有产出新的 BenchmarkDotNet FormalHost Mean/Allocated 报告，因此本次审查不建立新的正式性能数字；现有映射 cache hit/cold miss/容量/LRU 基准场景仍适用。
- 无上限缓存对不同键的并发 cold miss 共用准入锁，可能降低极端高基数首次加载的并行度；该路径不影响 cache hit，且当前没有回归或基准证据表明需要扩大为性能重构。
- 外部数据库 Provider Integration 属于本任务明确排除范围，后续 Provider、连接或执行链发生变化时仍需按环境变量门控执行。

## Git 状态

- 本次第三轮 Review 仅更新 `review.md`，未修改业务代码、测试、计划或 execution。
- 未执行 git add、commit、push、merge 或创建 PR。
