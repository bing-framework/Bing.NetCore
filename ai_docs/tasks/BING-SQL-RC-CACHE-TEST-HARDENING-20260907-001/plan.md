# Bing.Data.Sql RC 缓存测试硬化计划

## 1. 任务信息

- Task ID：`BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001`
- 计划日期：2026-09-07
- 基线分支：`dev_v6.0-refactor-sqlquery`
- 基线提交：`57fd298648eb69d8d44cddb9cdad71545c0c1301`
- 任务类型：Release Candidate 发布前测试硬化
- 实施边界：以职责级单元测试、可追溯矩阵和必要的最小缺陷修复为主；不扩展 SQL 功能，不改 Public API，不自动执行 Git commit、push 或创建 PR。

## 2. 目标与范围

本任务针对 `Bing.Data.Sql` 当前三类缓存建立可审计的发布前合同：

1. `DefaultEntityMappingResolver` 的原始模型元数据缓存与最终实体映射缓存；
2. `BoundedLazyCache`、`SqlMutationPlanCache`、`SqlMutationPlanCaches` 的 Mutation Plan 与属性 Getter 缓存；
3. `SqlQuery` 与 `SqlQueryOperationAccessor` 的 SQL 文本缓存、版本失效和重复渲染行为。

目标不是重写缓存，而是证明缓存键不会串用、命中与未命中符合合同、容量与淘汰策略可预测、并发和异常不会留下错误条目、查询 mutation 只在成功时失效缓存。如果新增测试暴露真实错误，才允许修改对应内部实现，并同步补齐基准场景或记录不适用依据。

以下内容不在本任务范围：

- Provider Capability、外部数据库 Integration、Dapper 执行器和连接生命周期；
- SQL 语法或完整 SQL 输出规则的功能扩展；
- Public API、PublicAPI baseline、RS0026 治理；
- CI、runsettings、数据库配置和发布证据框架；
- 正式性能调优或无基准支持的缓存结构替换。

## 3. 已核验现状与完成度

### 3.1 已真正实现

| 生产职责 | 已确认行为 | 源码证据 | 现有直接证据 |
| --- | --- | --- | --- |
| 最终实体映射缓存 | 键包含实体类型、DbKey、DatabaseType、MappingProfile、TableRouteKey 和最终 Database/Schema/Table；字符串维度在解析器入口规范化。 | `Metadata/EntityMappingCacheKey.cs`；`Metadata/DefaultEntityMappingResolver.cs` | `EntityMappingCacheIsolationTest`、`DatabaseRoutingAndMappingTest`、`CrossDatabaseSyntaxTest` |
| 映射缓存容量 | `null` 无上限；`0` 旁路；正数默认 AdmissionOnly，也可显式 LRU；公开内部统计包括 hit/miss/bypass/eviction/count/capacity/policy。 | `DefaultEntityMappingResolver.cs`；`Configs/SqlMetadataOptions.cs`；`EntityMappingCacheStatistics.cs` | 已覆盖无上限统计、零容量、满容量旁路、LRU、负容量、未知策略和高基数并发上限。 |
| 原始模型元数据缓存 | 按 `RuntimeTypeHandle` 保存 `Lazy<EntityModelMetadata>`，同实体并发首次解析只访问一次 provider。 | `DefaultEntityMappingResolver.GetCachedModelMetadata` | `Resolve_WhenMappingIsCached_ShouldNotCallModelMetadataProviderAgain`；`Resolve_WhenSameEntityIsResolvedConcurrently_ShouldLoadModelMetadataOnce` |
| Mutation Plan 键 | 包含实体类型、Provider、最终对象名、MappingProfile、TableRouteKey、操作、Include/Exclude 签名；字符串大小写和集合顺序被规范化。 | `Builders/Mutations/SqlMutationPlanCacheKey.cs` | 当前主要由 `DefaultSqlMutationBuilderPlanTest` 间接覆盖 Include/Exclude 等价性、不同 resolver 分区和操作维度。 |
| Mutation 有界缓存 | `BoundedLazyCache` 使用锁保护 Dictionary + LRU 链表；支持无上限、零容量、正容量 LRU、统计和当前 Lazy 的条件移除。 | `BoundedLazyCache.cs`；`SqlMutationPlanCache.cs` | Plan 已覆盖同键并发单次创建、异常重试、零容量、LRU、高基数并发；Getter 已覆盖重复命中、失败清理和 LRU。 |
| Mutation 分区 | `ConditionalWeakTable<IEntityMappingResolver, SqlMutationPlanCache>` 按 resolver 实例弱引用分区，容量由分区首次创建时的 options 固定。 | `SqlMutationPlanCaches.cs` | 已证明不同 resolver 不共享 Plan；尚未直接证明同 resolver 重用、Getter 分区以及“首次 options 固定”的配置合同。 |
| 查询 SQL 缓存 | `SqlQuery` 以 `_shapeVersion`、`_cachedVersion`、`_cachedSql` 管理缓存；需要 execution snapshot 时跳过缓存；`Mutate`/`MutateBuilder` 成功后统一 `MarkChanged`。 | `Queries/SqlQuery.cs`；`Queries/SqlQueryOperationAccessor.cs` | `SqlQueryLifecycleTest` 已覆盖命中、Where/Union/CTE/参数变更、空操作、失败原子性、环境隔离、SplitOn、Clone 和重复终端执行。 |
| 性能基线 | 已有映射 cache hit/cold miss/满容量/LRU、重复渲染、Clone，以及 PostgreSQL batch Getter 热路径。 | `Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`；`SqlMutationBenchmarks.cs` | `ai_docs/sql-metadata-test-traceability.md` 记录过正式基线及适用结论。 |

### 3.2 已完成、部分完成与待验证

- **已完成**：缓存主体不是接口、Stub 或 Mock；容量、LRU、查询失效和并发控制均进入真实内部调用链。相关三个测试类在本计划阶段以 Release、`--no-restore` 运行，`net6.0` 与 `net8.0` 各 123 passed、0 failed、0 skipped。
- **部分完成**：现有测试覆盖了主要 Happy Path 和若干边界，但 Mutation 缓存职责集中在大型综合测试类中；缓存键每个隔离维度、Getter 类型/属性隔离、零容量与并发、弱引用分区的同实例合同尚无清晰的一对一直接测试。
- **待验证**：映射缓存同键并发时 hit/miss 统计语义、最终映射构造是否允许竞争性重复计算；模型元数据 provider 首次失败后的重试合同；同一 resolver 被不同 `SqlMetadataOptions` 请求时的 first-writer-wins 行为是否应作为明确合同；这些不能仅凭实现推断为正确。
- **整体判断**：生产缓存能力已实现，基础回归通过；RC 所需的职责级、逐键维度、异常与并发可追溯证明仍是“部分完成”，完成本计划前不能宣称缓存测试闭环。

### 3.3 质量与设计评估

- 性能与资源：映射缓存的动态路由基数可配置控制；Mutation 缓存按 resolver 弱引用分区，避免静态强引用长期持有元数据；查询缓存避免无 execution snapshot 场景重复渲染。现阶段没有证据支持引入对象池、无锁结构或新缓存库。
- 复杂度与耦合：`BoundedLazyCache` 同时承载 Plan/Getter 的容量和 LRU 语义，适合建立独立测试类；`DefaultSqlMutationBuilderPlanTest` 同时验证 Builder SQL、Plan 和 Getter，职责过宽但本任务仅拆分缓存测试，不重构生产层级。
- API：相关缓存类型均为 `internal`，测试通过现有 `InternalsVisibleTo("Bing.Data.Sql.Tests")` 验证；本任务不新增公开诊断 API，不更改参数、返回值或可见性。
- 兼容与清理：不删除当前综合回归测试。迁移出重复断言时，只有新职责测试完整替代并保持可追溯后才允许去重；不得以减少测试数量为目标。
- Provider：缓存键包含 Provider/DatabaseType 维度，但本任务不改变 Provider SQL 规则，因此无需外部数据库集成；Provider 维度通过纯内存职责级测试证明。

## 4. 关键原则与决策门

1. 先写可以失败的合同测试，再决定是否修改生产代码；不得为了让测试通过而降低断言或只断言 `Contains`。
2. 缓存键测试必须逐维度验证：等价输入命中，同一时刻只改变一个隔离维度必须未命中；禁止一个综合用例同时改变多个字段后笼统宣称键完整。
3. 统计断言必须覆盖 hit、miss、bypass、eviction 和 entry count 的相互关系；并发统计若未定义，先在测试名和文档中明确语义，再固定数值。
4. 并发用例必须使用同步栅栏或可计数工厂制造真实竞争，不能依赖 `Task.Run` 恰好并发。
5. 异常用例必须证明：失败项不被永久缓存、后续重试可成功、旧失败调用不能删除已经重建的当前项。
6. 仅测试变更不要求新增 Benchmark；如果生产映射缓存、`BoundedLazyCache`、Getter 编译、Query 渲染或 Clone 热路径发生变化，则必须更新相应 Benchmark 场景并给出前后数据。若只修统计且不改变热路径，需要在 execution 文档中说明 Benchmark 不适用原因。
7. 不增加生产 `InternalsVisibleTo`，不暴露缓存内容或路由敏感信息，不引入第二套缓存统计模型。

## 5. 分阶段实施计划

### Phase 0：锁定基线与覆盖矩阵

#### CACHE-001 建立缓存合同清单和红绿基线

**目标**

把每个最终生产符号、关键行为和现有/待补测试建立一对一矩阵，避免重复测试或漏掉键维度。

**现状/证据**

`ai_docs/sql-metadata-test-traceability.md` 已记录 Mutation 容量、映射容量和 Query mutation，但 `SqlMutationPlanCacheKey`、`SqlMutationGetterCacheKey`、`SqlMutationPlanCaches.Get` 与 `BoundedLazyCache.RemoveIfCurrent` 尚未形成逐符号追溯。当前筛选测试两个 TFM 均 123/123 通过。

**修改范围**

- 已确认：`ai_docs/sql-metadata-test-traceability.md`
- 已确认：本任务后续新增/更新的缓存职责测试
- 不修改生产代码。

**实施步骤**

1. 列出映射键 8 个维度、Mutation Plan 键 10 个维度和 Getter 键 2 个维度。
2. 将行为标记为“现有直接覆盖、现有间接覆盖、缺失、待决策”。
3. 记录现有测试方法名，预先为缺口分配唯一测试方法名。
4. 保存开始实施时的完整测试数与筛选测试数，避免只报告新增用例。

**依赖**

无。

**验证**

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EntityMappingCacheIsolationTest|FullyQualifiedName~DefaultSqlMutationBuilderPlanTest|FullyQualifiedName~SqlQueryLifecycleTest" --logger "console;verbosity=minimal"
```

**风险**

追溯文档历史内容较长；更新必须追加当前任务的权威小节，并明确其优先级，不能改写无关历史结论。

**验收标准**

每个生产符号和键维度均有明确测试方法或待补项；两个目标框架的基线可复现。

### Phase 1：实体映射缓存职责级硬化

#### CACHE-101 补齐 `EntityMappingCacheKey` 隔离、规范化与最终路由测试

**目标**

直接证明最终映射缓存对等价上下文命中，并对每个会影响映射结果的维度隔离。

**现状/证据**

已有 DbKey、DatabaseType/Provider、MappingProfile、TableRouteKey 和派生 resolver 最终 Schema/Table 的分散测试；尚缺以统一表驱动方式逐一核对实体类型、最终 Database、Schema、TableName，以及空白/大小写规范化的 hit/miss 与统计变化。

**修改范围**

- 已确认：`framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs`
- 候选新增：`framework/tests/Bing.Data.Sql.Tests/Metadata/EntityMappingCacheKeyTest.cs`
- 仅红测需要时：`framework/src/Bing.Data.Sql/Bing/Data/Sql/Metadata/EntityMappingCacheKey.cs`
- 仅红测需要时：`framework/src/Bing.Data.Sql/Bing/Data/Sql/Metadata/DefaultEntityMappingResolver.cs`

**实施步骤**

1. 对等价大小写/首尾空白输入断言相同实例、1 miss + 后续 hit。
2. 对实体类型、DbKey、DatabaseType、MappingProfile、TableRouteKey、最终 Database、Schema、TableName 逐维度构造仅一处变化，断言独立实例和独立最终对象名。
3. 保留“未配置路由不把 TenantId 纳入键”的负向合同，避免高基数租户造成无意义分裂。
4. 对容量 `null`、`0`、正数下相同键与不同键分别断言 hit/miss/bypass/entry count。

**依赖**

`CACHE-001`。

**验证**

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~EntityMappingCache" --logger "console;verbosity=minimal"
```

**风险**

直接构造 internal record 只能证明值相等，不能证明 resolver 使用了最终路由值；因此必须保留经 `Resolve` 的行为测试，不能用纯 record 测试替代调用链。

**验收标准**

映射键所有维度均有单变量隔离用例；命中与未命中统计同时被断言；不同上下文不会返回错误映射。

#### CACHE-102 固化模型缓存、同键并发与异常恢复合同

**目标**

证明并发首次解析不会放大昂贵 provider/映射构造，并明确首次失败后的行为。

**现状/证据**

`_modelCache` 使用 `Lazy`，现有测试证明成功路径 provider 只调用一次；无上限最终映射使用 `ConcurrentDictionary.GetOrAdd`，其 value factory 可能竞争性执行；模型 provider 抛错时 faulted Lazy 当前会保留，尚无明确合同。

**修改范围**

- 已确认：`framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs`
- 候选：`DefaultEntityMappingResolver.cs`
- 候选：映射测试用可计数/可阻塞 provider 或派生 resolver，仅放在测试工程。

**实施步骤**

1. 使用栅栏同步同一实体、同一最终键的并发 miss，分别统计模型 provider 与最终映射构造次数。
2. 明确统计规则：每个外部 lookup miss 计数，还是每个实际创建项计数；按照既有字段命名和文档选择一种并固定测试。
3. 让 provider 首次抛错、第二次返回有效元数据，验证当前 faulted Lazy 行为；若永久缓存异常不符合 RC 可恢复性要求，以 `RemoveIfCurrent` 等最小并发安全方式修复并测试旧失败项不会删除新项。
4. 在 AdmissionOnly 与 LRU 容量模式下重复同键并发测试，证明容量和访问顺序索引不分叉。

**依赖**

`CACHE-101`。

**验证**

同 `CACHE-101`，并至少连续执行筛选用例 3 次以排除调度偶然性。

**风险**

把“所有调用均观察到同一发布实例”误写成“任何内部纯函数绝不重复执行”可能导致不必要的全局锁。只有可观察副作用、明显性能成本或统计合同要求单次创建时才改实现。

**验收标准**

成功、失败、重试和同键并发均有确定合同；无死锁、永久 faulted 条目或容量超限；统计语义可追溯。

### Phase 2：Mutation Plan / Getter 缓存直接测试硬化

#### CACHE-201 为 `BoundedLazyCache` 建立独立测试类

**目标**

从 Builder 综合测试中分离共享内部缓存的基础合同，直接验证其所有公开内部行为。

**现状/证据**

Plan 和 Getter 间接覆盖了多数分支，但没有 `BoundedLazyCacheTest`；`RemoveIfCurrent` 的“值相同才删除”和陈旧 Lazy 不得删除当前项尚未直接验证。

**修改范围**

- 候选新增：`framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/BoundedLazyCacheTest.cs`
- 仅红测需要时：`framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/BoundedLazyCache.cs`

**实施步骤**

1. 独立覆盖负容量拒绝、无上限命中/未命中、零容量旁路。
2. 覆盖 LRU touch 后淘汰正确键及全部统计。
3. 覆盖 `RemoveIfCurrent` 对当前值删除、对陈旧/不同 Lazy 不删除。
4. 通过同步栅栏覆盖同键并发、不同键超过容量和统计一致性。
5. 保留上层 Plan/Getter 用例作为组合证明，避免只测试容器而遗漏调用方异常处理。

**依赖**

`CACHE-001`。

**验证**

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~BoundedLazyCacheTest" --logger "console;verbosity=minimal"
```

**风险**

测试内部实现细节会增加重构成本；断言应聚焦可观察的缓存合同和统计，不固定 Dictionary/LinkedList 的具体组织方式。

**验收标准**

每个条件分支和四类统计均有直接测试；陈旧失败调用无法误删当前条目；并发测试稳定通过。

#### CACHE-202 逐维度验证 Mutation Plan 缓存键

**目标**

证明 Plan 不会跨实体、Provider、最终表路由、操作或列过滤复用，并证明等价输入稳定命中。

**现状/证据**

`SqlMutationPlanCacheKey.Create` 已实现规范化和顺序无关签名；当前测试主要证明相同 Insert、不同 include/exclude 和不同 resolver，不足以审计全部字段。

**修改范围**

- 候选新增：`framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheKeyTest.cs`
- 候选更新：`DefaultSqlMutationBuilderPlanTest.cs`
- 仅红测需要时：`SqlMutationPlanCacheKey.cs`、`SqlMutationPlanCache.cs`

**实施步骤**

1. 验证 Provider、Database、Schema、Table、MappingProfile、TableRouteKey 的大小写/空白规范化命中。
2. 逐一改变 EntityType、Provider、Database、Schema、Table、MappingProfile、TableRouteKey、Operation、IncludeSignature、ExcludeSignature，断言键不等及缓存 miss。
3. 验证 include/exclude 忽略顺序、大小写、重复和空白项，但 include 与 exclude 不能互换。
4. 经真实 `DefaultSqlEntityMutationCommandBuilder` 对至少两个 Provider key 和两套最终映射生成完整 SQL 断言，证明键差异进入最终命令而非仅 record equality。

**依赖**

`CACHE-201`。

**验证**

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SqlMutationPlanCacheKeyTest|FullyQualifiedName~DefaultSqlMutationBuilderPlanTest" --logger "console;verbosity=minimal"
```

**风险**

完整 SQL 测试不得使用 `Contains`；不同 Provider 若需要大量 fixture，应复用现有测试 Provider，不扩大为全 Provider 重复矩阵。

**验收标准**

10 个键维度均有单变量命中/隔离证明；至少一组行为测试断言完整 SQL 与参数快照；错误计划不会跨路由或 Provider 泄漏。

#### CACHE-203 补齐 Getter 缓存及 resolver 分区合同

**目标**

覆盖 Getter 键隔离、零容量、并发创建、配置校验和 `ConditionalWeakTable` 分区行为。

**现状/证据**

现有 Getter 用例覆盖重复读取、缺失属性失败清理和容量 1 的 LRU；未覆盖 `MutationGetterCacheCapacity=0/-1`、不同 SourceType/PropertyName、大小写等价、同键并发，以及同 resolver 获取缓存时 options 固定规则。

**修改范围**

- 候选新增：`framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheTest.cs`
- 候选更新：`DefaultSqlMutationBuilderPlanTest.cs`
- 仅红测需要时：`SqlMutationGetterCacheKey.cs`、`SqlMutationPlanCache.cs`、`SqlMutationPlanCaches.cs`

**实施步骤**

1. 对同类型同属性重复访问断言 hit，对属性名大小写/空白等价断言复用。
2. 对不同 SourceType、不同 PropertyName 断言独立 Getter，避免相同属性名跨类型串用。
3. 覆盖 Getter 容量 `null`、`0`、正数 LRU 和负数拒绝，并断言 hit/miss/bypass/eviction/count。
4. 同键并发读取时证明所有值正确，并在可观测范围内证明 Getter 只编译一次；失败与成功重建竞争时证明失败 Lazy 不会删除新条目。
5. 直接验证 `SqlMutationPlanCaches.Get`：同 resolver 返回同分区、不同 resolver 返回不同分区；明确同 resolver 第二次传入不同 options 时容量不变的 first-writer-wins 合同，或在红测与设计评审证明该行为危险时改为拒绝冲突配置。
6. 不以强引用 GC 测试证明 `ConditionalWeakTable` 回收时机；弱引用设计只做结构和无静态强引用审查，避免不稳定测试。

**依赖**

`CACHE-201`、`CACHE-202`。

**验证**

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SqlMutationPlanCacheTest|FullyQualifiedName~DefaultSqlMutationBuilderPlanTest" --logger "console;verbosity=minimal"
```

**风险**

Expression 编译次数当前不可直接观察；优先通过 internal seam 或缓存统计证明，不为测试增加生产公开接口。GC 回收断言具非确定性，明确禁止纳入稳定门禁。

**验收标准**

Getter 的命中、隔离、零容量、LRU、异常和并发合同完整；两个容量配置都校验负数；resolver 分区配置规则被明确且测试锁定。

### Phase 3：查询 SQL 缓存 mutation 矩阵收口

#### CACHE-301 审计所有 mutation 入口的成功、空操作与失败语义

**目标**

确保每个进入 `SqlQueryOperationAccessor.Mutate`/`MutateBuilder` 的公开 Fluent 入口都满足：成功恰好失效一次、空操作不失效、异常不失效且不污染 SQL/参数。

**现状/证据**

`SqlQueryLifecycleTest` 已对 Where、WhereIfNotEmpty、Union、CTE、Add/Clear 参数、SplitOn 以及多类失败建立高质量测试；需要以调用点清单核对尚未覆盖的 mutation family，而非机械复制每个扩展重载。

**修改范围**

- 已确认：`framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`
- 候选：对应未覆盖扩展的职责级现有测试文件
- 仅红测需要时：`framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryOperationAccessor.cs`
- 仅红测需要时：相关 `Extensions/*.cs`

**实施步骤**

1. `rg` 枚举所有 `SqlQueryOperationAccessor.Mutate`、`MutateBuilder` 和直接 `MarkChanged` 调用点，按 mutation family 建表。
2. 每个 family 至少选一个代表性成功用例，断言完整 SQL、参数快照、shape/cached version 与 render count。
3. 对具有条件短路或空集合语义的 family 增加 no-op 用例；对 clone、参数上限、alias/语义校验可能失败的 family 增加原子性用例。
4. 验证 `RequiresExecutionSnapshot=true` 时不错误复用 SQL 文本，普通重复 `ToSql` 才命中缓存。
5. 验证 Clone 与原查询拥有独立版本和 SQL 缓存，执行上下文或数据过滤状态变化不跨实例泄漏。

**依赖**

`CACHE-001`。

**验证**

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SqlQueryLifecycleTest" --logger "console;verbosity=minimal"
```

**风险**

逐重载复制测试会增加维护成本；应按共享 mutation 边界和差异行为选代表用例。SQL 输出发生变化时必须断言完整 SQL，不能只验证版本号。

**验收标准**

所有 mutation family 均映射到成功/no-op/失败中的适用场景；重复渲染、执行快照、Clone 和环境隔离无遗漏；完整 SQL 与参数断言一致。

### Phase 4：性能、文档与发布门禁闭环

#### CACHE-401 更新基准适用性与生产符号追溯

**目标**

让最终代码、测试方法、基准和 RC 结论表达同一事实。

**现状/证据**

现有基准已覆盖映射 hit/miss/容量/LRU、重复渲染和 Getter 热路径；当前追溯文档只覆盖部分缓存符号与方法。

**修改范围**

- 已确认：`ai_docs/sql-metadata-test-traceability.md`
- 仅生产热路径改变时：`framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`
- 仅 Getter/Plan 热路径改变时：`framework/tests/Bing.Data.Sql.Benchmarks/SqlMutationBenchmarks.cs`
- 执行阶段文档：`ai_docs/tasks/BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001/execution.md`

**实施步骤**

1. 更新“最终生产符号 -> 关键行为 -> 测试项目/类/方法”矩阵，列出本任务新增和保留测试。
2. 如果只新增测试，记录现有基准继续适用且无需制造新性能数据的理由。
3. 如果修改映射缓存、`BoundedLazyCache`、Getter 编译、Query render 或 Clone 热路径，补充/调整对应 BenchmarkDotNet 场景，并在相同 Job/Runtime 下执行可比较前后基准。
4. 对性能结果记录 Mean、Allocated、Gen0/1/2 和环境；不得把单次 ShortRun 宣称为正式 RC 基线。

**依赖**

`CACHE-101` 至 `CACHE-301`。

**验证**

```powershell
dotnet run --project .\framework\tests\Bing.Data.Sql.Benchmarks\Bing.Data.Sql.Benchmarks.csproj -c Release --no-restore -- --list flat
```

只有生产热路径改变时再运行受影响的正式 BenchmarkDotNet filter；纯测试变更以 `--list flat` 验证场景仍可发现。

**风险**

不同机器或 Job 的绝对数字不可直接比较；正式性能结论必须使用相同配置和环境元数据。

**验收标准**

所有最终生产符号都有精确测试方法追溯；Benchmark 更新或“不适用”依据明确；没有无数据支持的性能声明。

#### CACHE-402 执行双目标框架回归与范围审计

**目标**

证明新增缓存合同没有破坏 `Bing.Data.Sql` 全量行为，并确认变更范围符合计划。

**现状/证据**

测试工程实际覆盖 `net6.0` 与 `net8.0`。本任务不触及连接、执行器或查询工厂路径，因此 SQLite Integration 默认不要求；若实施中越界修改到这些路径，则必须追加 SQLite 本地集成。

**修改范围**

- 不新增生产范围；仅执行验证并记录结果。

**实施步骤**

1. 运行各缓存职责筛选测试，连续运行并发用例，确保无偶发失败。
2. 运行 `Bing.Data.Sql.Tests` 全量双 TFM 测试。
3. 运行解决方案 Release build，确认 Analyzer 与 Public API gate 未回退。
4. 若触及 Provider、连接、执行器或 Query/Executor 工厂，运行 SQLite 本地集成；外部数据库仍由安全环境变量门控，不计为本任务通过条件。
5. 审计 Git diff，只允许计划确认文件和由红测证明必要的候选文件；记录新增、修改、删除、重命名分类。

**依赖**

`CACHE-401`。

**验证**

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release --no-restore --logger "console;verbosity=minimal"
dotnet build .\Bing.All.sln -c Release --no-restore
git diff --check
git status --short
```

条件触发的 SQLite 验证命令应从其真实项目配置和仓库 Integration 文档中重新确认后执行，不在未触发时伪造运行结果。

**风险**

全量构建可能暴露与本任务无关的既有 warning；execution 文档需区分新增回退和既有基线，不能通过 suppress 处理。

**验收标准**

缓存筛选测试和全量测试在 net6.0/net8.0 均 0 failed；Release build 成功；`git diff --check` 通过；无未解释的范围扩张。

## 6. 文件范围汇总

### 6.1 已确认会修改

- `framework/tests/Bing.Data.Sql.Tests/EntityMappingCacheIsolationTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/SqlQueryLifecycleTest.cs`（仅审计发现代表性缺口时添加用例；不得机械复制）
- `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/DefaultSqlMutationBuilderPlanTest.cs`
- `ai_docs/sql-metadata-test-traceability.md`
- `ai_docs/tasks/BING-SQL-RC-CACHE-TEST-HARDENING-20260907-001/execution.md`

### 6.2 候选新增测试文件

- `framework/tests/Bing.Data.Sql.Tests/Metadata/EntityMappingCacheKeyTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/BoundedLazyCacheTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheKeyTest.cs`
- `framework/tests/Bing.Data.Sql.Tests/Builders/Mutations/SqlMutationPlanCacheTest.cs`

最终可根据职责聚合相邻用例，但必须保留独立 `BoundedLazyCacheTest`，且不得重新集中到单一大型综合类。

### 6.3 仅红测证明缺陷时修改的生产文件

- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Metadata/DefaultEntityMappingResolver.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Metadata/EntityMappingCacheKey.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/BoundedLazyCache.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/SqlMutationPlanCacheKey.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/SqlMutationGetterCacheKey.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/SqlMutationPlanCache.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Builders/Mutations/SqlMutationPlanCaches.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQuery.cs`
- `framework/src/Bing.Data.Sql/Bing/Data/Sql/Queries/SqlQueryOperationAccessor.cs`
- 由 mutation 调用点审计定位到的具体 `Extensions/*.cs`

### 6.4 条件候选 Benchmark 文件

- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMetadataBenchmarks.cs`
- `framework/tests/Bing.Data.Sql.Benchmarks/SqlMutationBenchmarks.cs`

### 6.5 明确不修改

- PublicAPI baseline、Provider 项目、Dapper 执行层、Integration 配置、CI 脚本、数据库和旧任务文档。

## 7. 依赖关系

```text
CACHE-001
  |-- CACHE-101 --> CACHE-102
  |-- CACHE-201 --> CACHE-202 --> CACHE-203
  `-- CACHE-301

CACHE-102 + CACHE-203 + CACHE-301
              |
              v
          CACHE-401 --> CACHE-402
```

Phase 1、Phase 2 和 Phase 3 可在覆盖矩阵完成后独立推进，但同一测试文件避免并行修改。任何生产修复均必须引用先行失败的测试方法和失败证据。

## 8. Breaking Change 与兼容策略

本计划预期 **无 Breaking Change**，不改变公共类型、方法、默认配置或 SQL 输出。

如果测试证明 `SqlMutationPlanCaches` 的冲突 options 行为必须从 first-writer-wins 改为显式拒绝，这仍属于 internal 行为，但可能暴露现有 DI 配置错误。实施时应：

1. 保持 `SqlMetadataOptions` 公开属性和默认值不变；
2. 提供包含 resolver 和冲突配置名称的明确异常，不输出路由或连接敏感数据；
3. 增加同配置成功、冲突配置失败和不同 resolver 隔离测试；
4. 在 execution 文档中记录兼容影响。若需要改 Public API，则超出本任务范围，停止并重新规划。

## 9. 测试策略

- 单元测试层：直接测试 internal 缓存、键、统计、分区和 Query mutation 边界。
- 行为测试层：通过真实 resolver、mutation builder 和 query builder 断言最终映射、完整 SQL 与参数快照。
- 边界：`null`、`0`、正容量、负容量、大小写、空白、重复、顺序变化、高基数。
- 异常：factory/provider/属性解析失败，失败项移除和成功重试。
- 并发：同键单次发布、不同键容量上限、LRU 索引一致性和陈旧失败项竞争。
- 隔离：实体、Provider、数据库、架构、表、profile、route、operation、include/exclude、source type、property、resolver、query clone 和环境上下文。
- 重复渲染：未变更命中；成功 mutation 失效；no-op/失败保留；execution snapshot 绕过；Clone 独立。
- Integration：默认不适用。只有实施触及 Provider、连接、执行器或 Query/Executor 创建链时才追加 SQLite 本地集成；外部数据库继续受安全环境变量门控。
- Benchmark：纯测试/文档变更不新增运行；生产热路径变更必须更新对应基线或说明仅统计路径不适用。

## 10. 风险清单与缓解

| 风险 | 影响 | 缓解措施 |
| --- | --- | --- |
| 将并发调度偶然性当作缓存合同 | 测试偶发失败或未制造真实竞争 | 使用 Barrier/ManualResetEventSlim 与计数工厂，重复运行筛选测试。 |
| 过度固定内部结构 | 合法重构导致无意义测试失败 | 断言值、实例、统计和容量等可观察合同，不反射 Dictionary/链表。 |
| 缓存键漏维度 | 跨租户、Provider 或路由返回错误元数据/SQL | 单变量键矩阵加真实调用链测试，命中与未命中同时断言。 |
| 等价输入未规范化 | 高基数缓存膨胀 | 覆盖大小写、首尾空白、集合顺序和重复项。 |
| faulted Lazy 永久驻留或误删新值 | 持续失败或并发正确值丢失 | 失败重试与 `RemoveIfCurrent` 竞争合同测试。 |
| first-writer-wins 配置未显式 | 同 resolver 的后续容量配置被静默忽略 | 固化合同或显式拒绝冲突，不静默改变公共默认值。 |
| 为测试增加公开接口 | Public API 污染和 RS0026 回退 | 复用现有 friend assembly，只增加 internal seam。 |
| 无证据性能优化 | 复杂度与分配反而上升 | 生产热路径修改前后使用同一 BenchmarkDotNet Job 比较。 |
| 追溯文档与最终方法名不一致 | RC 证据不可审计 | 最终从源码提取方法名并逐项回填矩阵。 |

## 11. 文档策略

本任务无需修改用户 SQL 使用文档，因为不改变公共配置和行为。必须更新：

- `ai_docs/sql-metadata-test-traceability.md`：新增本 Task ID 的权威追溯小节；
- `execution.md`：记录红测、生产修复（如有）、双 TFM 结果、Benchmark 适用性、文件变更分类和遗留风险。

若实施改变内部错误语义或配置冲突检测，应在 execution 文档中记录迁移影响；只有公共可观察配置行为改变时才重新评估 `docs/sql/`，不得预先扩大范围。

## 12. 最终验收条件

1. 映射缓存键、Mutation Plan 键和 Getter 键的每个维度均有直接命中/未命中或隔离测试。
2. `BoundedLazyCache` 有独立测试类，覆盖无上限、零容量、LRU、统计、条件移除、异常上层处理和并发。
3. 映射模型缓存、最终映射缓存、Plan、Getter、resolver 分区均覆盖成功、失败、重试和适用的并发场景。
4. Query mutation 调用点清单完整；成功、no-op、失败、execution snapshot、Clone 和重复渲染合同均有职责级测试。
5. SQL 输出相关测试断言完整字符串及参数快照，不使用片段 `Contains` 代替完整合同。
6. `Bing.Data.Sql.Tests` 在 net6.0、net8.0 均 0 failed，Release build 成功，Analyzer/Public API gate 无新增回退。
7. 若生产热路径变化，Benchmark 已更新并完成同环境可比较验证；否则 execution 文档明确说明不适用原因。
8. `ai_docs/sql-metadata-test-traceability.md` 完成“最终生产符号 -> 关键行为 -> 测试项目/类/方法”映射。
9. Git diff 仅包含本计划允许文件；无测试削弱、异常吞噬、缓存敏感信息暴露、生产 `InternalsVisibleTo`、CI/数据库越界修改。
10. 未执行自动 commit、push、merge 或 PR。

## 13. Definition of Done

- [ ] `CACHE-001` 至 `CACHE-402` 全部完成并在 `execution.md` 留有证据。
- [ ] 所有新测试先证明合同，任何生产修改均能追溯到明确红测。
- [ ] 缓存隔离、命中/未命中、容量、LRU、旁路、异常、并发、分区和重复渲染均形成闭环。
- [ ] 双目标框架全量测试与 Release build 通过。
- [ ] Benchmark 更新或不适用结论符合 `AGENTS.md` 门槛。
- [ ] 最终生产符号到测试方法追溯矩阵已维护。
- [ ] 变更文件按新增、修改、删除、重命名分类记录，所有范围扩张均有理由。
- [ ] 无 Breaking Change；如发现必须改变 Public API，任务停止并重新规划。
- [ ] 工作区未发生自动 Git 提交或远端操作。

