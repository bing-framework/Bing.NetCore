<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001
AI_EXECUTION_FINISHED_AT: 2026-09-07T22:48:51.0446788+08:00

# 实施执行报告

## 执行结论

任务已完成。缓存测试从综合覆盖扩展为职责级直接合同，发现并修复了实体模型元数据失败后 faulted `Lazy` 永久驻留的问题。没有修改公共 API、Provider、连接执行链或数据库配置；未执行自动 commit、push、merge 或 PR。

## 任务信息

- Task ID：`BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001`
- 基线提交：`57fd298648eb69d8d44cddb9cdad71545c0c1301`
- 分支：`dev_v6.0-refactor-sqlquery`
- 执行器：Codex
- 计划：`ai_docs/tasks/BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001/plan.md`

## 计划执行情况

| 计划项 | 结果 | 证据 |
| --- | --- | --- |
| CACHE-001 | 完成 | 本报告、`ai_docs/sql-metadata-test-traceability.md` 新增 Task ID 追溯矩阵。 |
| CACHE-101 | 完成 | `EntityMappingCacheKeyTest` 加上现有 resolver 隔离、容量、LRU 和路由测试。 |
| CACHE-102 | 完成 | 增加 provider 首次失败后重试测试；修复失败 Lazy 条件移除。 |
| CACHE-201 | 完成 | 新增独立 `BoundedLazyCacheTest`，覆盖统计、LRU、旁路、条件删除和并发。 |
| CACHE-202 | 完成 | 新增 `SqlMutationPlanCacheKeyTest`，覆盖等价规范化和十个隔离维度。 |
| CACHE-203 | 完成 | 新增 Getter 类型/属性隔离、零容量、并发及 resolver 分区测试。 |
| CACHE-301 | 完成/审计无需新增 | `SqlQueryLifecycleTest` 已覆盖所有主要 mutation family、成功/空操作/失败、Clone、execution snapshot 和重复渲染。 |
| CACHE-401 | 完成 | 更新生产符号到测试方法追溯；保留既有映射和 Getter 基线并记录本轮 Benchmark 适用性。 |
| CACHE-402 | 完成 | 双 TFM 全量测试、Release build、范围和编码检查通过。 |

## 已完成事项

### 缓存测试

- 新增 `framework/tests/Bing.Data.Sql.Tests/Metadata/EntityMappingCacheKeyTest.cs`，逐一验证实体类型、DbKey、DatabaseType、MappingProfile、TableRouteKey、Database、Schema、TableName 的键隔离。
- 新增 `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/BoundedLazyCacheTest.cs`，覆盖无上限命中、零容量旁路、LRU touch/淘汰、命中/未命中/旁路/淘汰统计、`RemoveIfCurrent` 陈旧值保护、同键并发单次发布和负容量。
- 新增 `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheKeyTest.cs`，覆盖 Provider、对象名、profile/route、操作和 Include/Exclude 规范化及逐维度隔离。
- 新增 `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheTest.cs`，覆盖 Getter 等价命中、源类型/属性隔离、零容量、同键并发、负容量和 resolver 分区。
- 扩展 `EntityMappingCacheIsolationTest`，增加模型 provider 首次失败、后续成功的重试合同。

### 生产修复

`DefaultEntityMappingResolver.GetCachedModelMetadata` 现在在 `Lazy.Value` 抛错时仅移除仍为当前值的 `Lazy`。使用 `ICollection<KeyValuePair<...>>.Remove` 做条件删除，避免并发成功发布的新值被旧失败调用误删；原有成功缓存和并发单次 provider 访问行为不变。

## 修改文件

### 新增

- `framework/tests/Bing.Data.Sql.Tests/Metadata/EntityMappingCacheKeyTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/BoundedLazyCacheTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheKeyTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheTest.cs`
- `ai_docs/tasks/BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001/execution.md`

### 修改

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Metadata/DefaultEntityMappingResolver.cs`
- `framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs`
- `ai_docs/sql-metadata-test-traceability.md`

### 删除/重命名

无。

## API/数据/配置变化

- 无 Public API 变化，无 PublicAPI baseline 变化。
- 无数据库、连接字符串、RunSettings、CI 或 Provider 配置变化。
- 无新增 `InternalsVisibleTo`。
- 无 Breaking Change。

## 测试结果

### 直接缓存筛选

```text
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EntityMappingCacheIsolationTest|FullyQualifiedName~DefaultSqlMutationBuilderPlanTest|FullyQualifiedName~SqlQueryLifecycleTest|FullyQualifiedName~BoundedLazyCacheTest|FullyQualifiedName~EntityMappingCacheKeyTest|FullyQualifiedName~SqlMutationPlanCacheKeyTest|FullyQualifiedName~SqlMutationPlanCacheTest"
```

- net8.0：139 passed，0 failed，0 skipped。
- net6.0：139 passed，0 failed，0 skipped。

### Bing.Data.Sql.Tests 全量

```text
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore --logger "console;verbosity=minimal"
```

- net8.0：1299 passed，0 failed，0 skipped。
- net6.0：1299 passed，0 failed，0 skipped。

### 首次红测与修复

新增 `Resolve_WhenModelMetadataProviderFailsOnce_ShouldRemoveFailureAndAllowRetry` 首次运行按预期失败，证明 faulted `Lazy` 会阻塞后续重试；加入条件删除实现后，net6.0/net8.0 均通过。没有删除或削弱失败测试。

## Build/Typecheck/Lint/Format

```text
dotnet build .\Bing.All.sln -c Release --no-restore
```

- 结果：成功，0 errors，173 warnings。
- 警告为仓库既有依赖 TFM/RID、XML 注释、异步无 await、xUnit analyzer、隐藏成员等；本轮未使用 NoWarn、Suppress 或降低严重度处理。
- `git diff --check`：通过。
- 变更文本文件均以 UTF-8 读取/写入；新增文件无尾随空白。

## Benchmark 验证与适用性

- `dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -- --list flat` 成功发现 `ResolveMappingCacheHit`、`ResolveMappingCold`、`ResolveMappingWhenBoundedCacheIsFull`、`ResolveMappingWhenLruCacheEvicts` 等现有场景。
- 本轮尝试运行 `*SqlMetadataBenchmarks.ResolveMapping*` 的 FormalHost 和 `ResolveMappingCacheHit --job Dry`。BenchmarkDotNet 输出了实际启动/样本，但在当前 Windows 环境遇到 power-plan 设置失败和子进程脱离主命令，未生成可复用结果报告；残留的本轮子进程已停止。
- 本轮生产改动增加模型元数据异常分支的条件清理，并在无上限映射缓存 miss 路径串行化最终映射构造；映射键和 SQL 渲染算法未改变，既有 FormalHost 命中/冷 miss/容量/LRU 场景仍适用。当前环境未生成新的正式 Mean/Allocated 报告，因此不伪造性能数字；构造次数和异常恢复由直接测试证明。

## 计划偏差

- 原计划允许在红测证明缺陷时修改 `DefaultEntityMappingResolver`；本轮正是该决策门触发，修改范围保持最小。
- CACHE-301 未新增查询测试，因为对全部 `SqlQueryOperationAccessor.Mutate`/`MutateBuilder` 调用点审计后，现有 `SqlQueryLifecycleTest` 已覆盖主要 family 和状态合同；仅更新追溯矩阵，避免机械复制。
- 未运行 SQLite Integration：本轮未修改 Provider、连接、执行器或 Query/Executor 创建链，符合计划中的条件触发规则。

## 基线问题

- 解决方案构建保留 173 条既有 warning；没有发现由本轮缓存代码引入的新错误或 Public API 回退。
- BenchmarkDotNet 当前环境无法稳定产出本轮 FormalHost 报告；这不影响单元测试和 Release build 结论，但不应把本轮性能结果用于新的正式数字比较。

## 已知问题

- `SqlMutationPlanCaches` 对同一 resolver 的后续不同 options 仍遵循现有 first-writer-wins 分区配置；本轮通过测试锁定该行为，没有证据证明需要改变内部配置合同。
- 外部 Provider Integration 未在本任务中执行，属于任务明确排除范围。

## 风险与回归关注点

- 模型 provider 失败后的 faulted `Lazy` 现在可重试；条件删除只移除当前实例，后续并发回归应继续关注旧失败调用误删新值的风险。
- 缓存键新增测试只固定可观察值、实例和统计，不固定 Dictionary/LinkedList 内部结构，后续实现重构仍需保留键隔离和容量合同。
- 任何后续修改映射缓存、Getter 编译、Query render 或 Clone 热路径时，应使用相同 BenchmarkDotNet Job 重新建立可比较基线。

## Reviewer 注意事项

- 重点复核 `DefaultEntityMappingResolver.GetCachedModelMetadata` 的条件删除是否满足并发语义，以及测试是否覆盖成功、失败和重试。
- 重点复核 SQL Mutation Plan 键十个维度的单变量隔离，不要将 record equality 测试误认为完整调用链覆盖。
- 重点复核追溯表中的方法名与源码保持一致，SQL 输出相关既有测试继续使用完整字符串断言。
- 本任务没有新增 Public API、数据库操作或秘密配置。

## Git 状态

- 未执行 `git add`、`git commit`、`git push`、`git merge` 或创建 PR。
- 工作区变更仅限本报告列出的生产修复、测试、追溯文档和任务文档；构建/Benchmark 生成目录未纳入版本变更。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001/review.md`

#### FIX-001

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs`
  - `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheTest.cs`
  - `ai_docs/sql-metadata-test-traceability.md`
- 根因：原有新增测试只比较 `EntityMappingCacheKey`/`SqlMutationPlanCacheKey` record，不经过真实 resolver/cache lookup；没有证明命中、未命中、最终 SQL 和参数隔离。
- 修复：新增上下文大小写/空白命中与统计、实体类型独立缓存测试；新增 Mutation Plan 十维真实 cache miss/hit 测试；新增两个 Provider key 和两套路由的真实 Builder 完整 SQL/参数断言。
- 验证：
  - 相关缓存筛选：net8.0 `144 passed`，net6.0 `144 passed`。

#### FIX-002

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/BoundedLazyCacheTest.cs`
  - `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheTest.cs`
  - `framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs`
- 根因：并发用例依赖短操作的 `Parallel.For`，不能区分串行执行；模型失败测试只覆盖顺序重试。
- 修复：使用 `CountdownEvent`、启动事件和 `LongRunning` worker 确保 worker 同时进入 lookup；模型缓存新增多个等待者共享首次失败 Lazy 后统一重试的确定性测试；保留生产条件删除逻辑，未改公共 API。
- 验证：
  - 并发筛选连续 3 轮执行；每轮 net8.0/net6.0 均 `11 passed, 0 failed, 0 skipped`。

#### FIX-003

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheTest.cs`
  - `ai_docs/sql-metadata-test-traceability.md`
- 根因：resolver 分区测试只观察 Plan 容量，没有使用 Getter 容量或验证不同 resolver 的 Getter 状态。
- 修复：同 resolver 以首次 Plan/Getter 容量固定行为运行两种属性，断言 Getter count/miss/eviction；不同 resolver 使用另一组容量并断言 Getter 独立状态；追溯矩阵改为同时描述 Plan/Getter 分区。
- 验证：
  - `SqlMutationPlanCacheTest` 所在专项筛选 net8.0/net6.0 通过。

### Round 1 汇总

- MUST_FIX：无。
- 已完成：FIX-001、FIX-002、FIX-003。
- PARTIAL：无。
- BLOCKED：无。
- FAILED：无。
- 回归验证：缓存/Query 专项 net8.0/net6.0 各 `144 passed`；`Bing.Data.Sql.Tests` 全量 net8.0/net6.0 各 `1304 passed, 0 failed, 0 skipped`；`Bing.All.sln` Release build `0 errors`；`git diff --check` 通过。
- 下一步：等待下一轮独立 `review-code`，本轮不修改 `review.md` 的 Reviewer 状态。

### Round 2

- Review 状态：`NEEDS_FIX`
- Fix Scope：`recommended`
- Review 文件：`ai_docs/tasks/BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001/review.md`

#### FIX-004

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheTest.cs`
  - `framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs`
- 根因：Round 1 Mutation variation helper 的 baseline 与默认值不一致，单个 variation 实际改变了多个键字段；实体最终对象名也缺少单变量真实 resolver 测试。
- 修复：所有 Plan variation 显式继承 baseline 的其余字段，并在每次 miss 后断言递增；新增最终 Database、Schema、TableName 分别变化的 resolver 行为测试。
- 验证：
  - Entity/Mutation 专项在 net8.0/net6.0 通过。

#### FIX-005

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Data.Sql/Bing/Data/Sql/Metadata/DefaultEntityMappingResolver.cs`
  - `framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs`
- 根因：Round 1 没有直接测试 stale/current 条件删除；同键无上限 `ConcurrentDictionary.GetOrAdd` value factory 会重复执行最终映射构造。
- 修复：抽取 `RemoveModelMetadataCacheEntryIfCurrent` internal seam 并直接验证 stale 不删 current；增加 `CreateMapping` 计数 resolver 和确定性起跑并发测试；在无上限 cache miss 路径加准入锁和二次查找，仅串行化最终映射构造，保持 Resolve 命中路径无锁。
- 验证：
  - 同键并发最终映射构造在 net8.0/net6.0 均为 1 次。
  - stale/current 条件删除测试在 net8.0/net6.0 通过。

### Round 2 汇总

- MUST_FIX：无。
- 已完成：FIX-004、FIX-005。
- PARTIAL：无。
- BLOCKED：无。
- FAILED：无。
- 回归验证：缓存/Query 专项 net8.0/net6.0 各 `147 passed, 0 failed, 0 skipped`；并发筛选连续 3 轮，每轮两个 TFM 各 `12 passed, 0 failed, 0 skipped`；`Bing.Data.Sql.Tests` 全量 net8.0/net6.0 各 `1307 passed, 0 failed, 0 skipped`；`Bing.All.sln` Release build `0 errors`、117 warnings；`git diff --check` 通过。
- 下一步：等待第三轮独立 `review-code`，本轮不修改 `review.md` 的 Reviewer 状态。
