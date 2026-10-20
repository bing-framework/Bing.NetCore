# SQL 元数据映射缓存测试硬化摘要

本文件整合 BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001 的三份任务文档。对应原始 Markdown 已合并进本摘要；完整原始 Markdown 仅保留在本地备份 `artifacts/task-doc-audit/tasks-before-cleanup.zip`，删除清单见 [CLEANUP.md](../CLEANUP.md)。其余 SQL API、RC 和发布任务原文保持独立，不在此文件重复合并。以下测试数量、构建结果和 Benchmark 状态均为原任务记录，本文件未重新运行。

<a id="BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001"></a>

## 任务目标

将映射缓存和 SQL Mutation Plan 缓存从综合覆盖扩展为职责级直接合同，验证键隔离、容量策略、LRU、并发、失败恢复、Query 生命周期和追溯关系；修复实体模型元数据解析失败后 faulted Lazy 永久驻留的问题。任务不修改公共 API、Provider、连接执行链、数据库配置、CI 或友元程序集。

## 最终实现

### 模型元数据失败 Lazy 的条件删除

DefaultEntityMappingResolver.GetCachedModelMetadata 在 Lazy.Value 失败时，只移除仍等于当前失败实例的缓存项。通过 RemoveModelMetadataCacheEntryIfCurrent 进行键和值的条件删除，避免迟到的失败调用误删已经成功发布的新 Lazy；失败后后续解析可以重试。

无上限最终映射的 cold miss 采用准入锁和二次查找，保证同一映射在确定性并发场景下只构造一次；已有命中路径保持无锁。

### 映射键与 resolver 隔离

- EntityMappingCacheKey 覆盖实体类型、DbKey、DatabaseType、MappingProfile、TableRouteKey、Database、Schema 和 TableName。
- 对象名的 Database、Schema、TableName 分别变化时必须产生独立 lookup；大小写和有效空白规范化按现有合同执行。
- 不同 resolver 的缓存、Getter 状态和容量配置保持分区；同一 resolver 的 Plan/Getter 容量继续遵循已有 first-writer-wins 行为。

### BoundedLazyCache 与 Mutation Plan

- BoundedLazyCache 直接覆盖无上限、零容量旁路、AdmissionOnly、LRU touch/淘汰、命中/未命中/旁路/淘汰统计、异常和条件删除。
- SqlMutationPlanCacheKey 覆盖 Provider、物理对象名、profile/route、操作以及 Include/Exclude 规范化等十个隔离维度，并用真实 Builder 验证等价命中、单变量 miss、完整 SQL 和参数快照。
- Getter 缓存覆盖源类型/属性隔离、零容量、并发和 resolver 分区。
- Query 生命周期已有 SqlQueryLifecycleTest 覆盖主要 mutation family、成功/空操作/失败、Clone、execution snapshot 和重复渲染，因此没有机械复制 CACHE-301 测试。

## 关键源码与测试路径

- 生产修复：[DefaultEntityMappingResolver.cs](../../../framework/src/Bing.Data.Sql/Bing/Data/Sql/Metadata/DefaultEntityMappingResolver.cs)
- 映射缓存键和配置：[EntityMappingCacheKey.cs](../../../framework/src/Bing.Data.Sql/Bing/Data/Sql/Metadata/EntityMappingCacheKey.cs)、[SqlMetadataOptions.cs](../../../framework/src/Bing.Data.Sql/Bing/Data/Sql/Configs/SqlMetadataOptions.cs)
- 映射键/实体缓存测试：[EntityMappingCacheKeyTest.cs](../../../framework/tests/Bing.Data.Sql.Tests/Metadata/EntityMappingCacheKeyTest.cs)、[EntityMappingCacheIsolationTest.cs](../../../framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs)
- 有界 Lazy 测试：[BoundedLazyCacheTest.cs](../../../framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/BoundedLazyCacheTest.cs)
- Mutation Plan 测试：[SqlMutationPlanCacheKeyTest.cs](../../../framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheKeyTest.cs)、[SqlMutationPlanCacheTest.cs](../../../framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheTest.cs)
- Query/Builder 快照测试：[SqlQueryLifecycleTest.cs](../../../framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs)、[VersionedEntityMappingQueryTest.cs](../../../framework/tests/Bing.Dapper.Core.Tests/Metadata/VersionedEntityMappingQueryTest.cs)
- 性能场景入口：[SqlMetadataBenchmarks.cs](../../../framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs)
- 生产符号到测试方法追溯：[SQL 元数据测试追溯矩阵](../../sql-metadata-test-traceability.md)

## Review 闭环

- 独立 Review 状态为 PASS，未发现新的 MUST_FIX 或 SHOULD_FIX。
- CACHE-001、CACHE-101、CACHE-102、CACHE-201、CACHE-202、CACHE-203、CACHE-401、CACHE-402 均 PASS。
- CACHE-301 标记为 DEVIATED_OK：Query 生产路径没有变化，既有生命周期测试已经覆盖缓存命中、失效、失败原子性和重复渲染。
- Round 1 的 FIX-001 至 FIX-003 和 Round 2 的 FIX-004、FIX-005 均已完成。Review 特别确认：
  - Mutation Plan 每个 variation 只改变一个维度并保留同一 baseline。
  - 最终 Database、Schema、TableName 各自变化均有独立 resolver lookup/统计测试。
  - stale Lazy 条件删除不会删除 current 值。
  - 确定性并发测试证明同一映射最终只构造一次。
  - SQL 输出合同继续断言完整 SQL 和参数，不以 Contains 片段代替。

## 历史验证记录

原 execution.md 记录的阶段性结果包括：

- 直接缓存筛选：net8.0 和 net6.0 各 139 passed。
- 全量 Bing.Data.Sql.Tests：net8.0 和 net6.0 各 1299 passed，0 failed，0 skipped。
- Release build：0 errors、173 warnings；警告为仓库已有依赖 TFM/RID、XML 注释、xUnit analyzer 等。
- Review 复用的最终专项筛选为两个 TFM 各 37 passed；Round 2 记录的缓存/Query 专项为各 147 passed，全量为各 1307 passed。

上述数字来自不同修复轮次的原始文档，不能作为本次摘要写入时的重新验证结果。原任务还记录了 git diff --check 通过，未执行提交、推送、合并或 PR。

## Benchmark 与外部验证边界

- BenchmarkDotNet 能发现 ResolveMappingCacheHit、ResolveMappingCold、ResolveMappingWhenBoundedCacheIsFull、ResolveMappingWhenLruCacheEvicts 等既有场景。
- 本任务环境未稳定生成新的 FormalHost Mean/Allocated 正式报告；原任务明确没有伪造新的性能数字。既有命中、冷解析、容量和 LRU 场景仍可作为后续回归入口。
- 未运行外部 Provider Integration，因为本任务未修改 Provider、连接、执行器或 Query/Executor 创建链。

## 已知限制与后续注意事项

- 同一 resolver 的后续不同 options 继续遵循现有 Plan/Getter first-writer-wins 分区行为；本任务通过测试锁定该合同，没有证据要求改变它。
- 无上限缓存对不同键的并发 cold miss 共用准入锁，极端高基数首次加载可能降低并行度；命中路径不进入该锁。
- 直接修改映射配置对象不再隐式生效，应使用显式发布/快照契约。
- 后续修改映射缓存、Getter 编译、Query render 或 Clone 热路径时，应在相同 BenchmarkDotNet Job 下重新建立可比较基线。
- 公共 API、数据库配置、Provider 配置和生产 friend assembly 均未新增。

## 计划验收追溯

- CACHE-001：生产符号、关键行为、测试项目和方法名已写入 [SQL 元数据测试追溯矩阵](../../sql-metadata-test-traceability.md)。
- CACHE-101/102：映射键隔离、失败 Lazy 重试、条件删除和同键并发构造。
- CACHE-201：BoundedLazyCache 的容量、LRU、统计、异常和并发。
- CACHE-202/203：Mutation Plan 十维键、Getter 缓存和 resolver 分区。
- CACHE-401/402：追溯同步、双 TFM 专项/全量测试、Release build 和差异检查。

## 原始文件清单

- `ai_docs/tasks/BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001/plan.md`：任务范围、CACHE-001 至 CACHE-402 验收矩阵和门控条件。
- `ai_docs/tasks/BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001/execution.md`：生产修复、测试、历史构建/Benchmark 结果、风险和 Round 1/2 执行记录。
- `ai_docs/tasks/BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001/review.md`：第三轮独立 Review、FIX-004/FIX-005 复核、最终 PASS 和剩余风险。

## 归档声明

本摘要只整理上述三份文档的已完成能力和明确限制。对应原始 Markdown 已合并，完整内容仅保留在 `artifacts/task-doc-audit/tasks-before-cleanup.zip`；删除清单见 [CLEANUP.md](../CLEANUP.md)。本次未重新运行测试或 Benchmark。
