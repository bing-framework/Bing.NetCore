# 合并后修复与验证计划

Task：`20261021-merged-code-audit`。基准候选：`a99a16c4b55c407cc81d771822667044e2e9e9b1`。日期：2026-10-21。

状态：**规划完成，修复未开始；冻结 7 项，已完成 0/7**。本计划供父会话确认后分派 GPT-6-Luna / max 实现、GPT-6.1-Sol / high 独立验证。本会话未自行调度模型、代理或会话。冻结表示范围明确，不表示已获未来实施授权或已通过验收。

## 依据与目标

当前主方案 restore 成功、build 失败；本次可运行部分 10,191 通过 / 0 失败 / 162 跳过，另一个诊断探针复现 2 个 SQL 作用域错误。精确范围与原始证据见 [audit.md](audit.md)、[命令清单](artifacts/command-manifest.json)、[工程清单](artifacts/project-matrix.md)。不能把已运行部分通过视为全仓通过。

实际已实现 SQL 查询/执行/Provider、缓存操作、领域事件排空、动态 API 以及多数基础模块；实现深度与本次验证范围不同。事件排空有真实调用链与 Domain 单测，EFCore/FreeSQL 的当前集成验证仍被编译阻断。微信退款、全局领域验证规则为两个确定缺口；OAuth 工厂、租户文化为两个候选。不存在可据此计算的全产品完成百分比。

本计划先恢复构建与验证图，再修正明确文档错误。不重构公共 API 命名、返回形状或可见性，不批量删除兼容层；AOP 类型转发是需要保留的兼容层。性能 smoke 仅证明对应路径能运行；没有支持全面性能优化或性能达标的证据。

## 边界与执行约束

- 先重新读取 AGENTS、适用作用域规则、`$CODEX_HOME/GOAL_RULES.md`（未设置则 `~/.codex/GOAL_RULES.md`），记录当前 HEAD、用户改动与本计划基准差异。保留用户修改，不覆盖初审原始日志。
- 全部文本 UTF-8。PowerShell 显式设置 InputEncoding、OutputEncoding、`$OutputEncoding`；日志必须保留 stdout/stderr、退出码、超时、候选、TFM 与环境。
- 禁止 git add/commit/push/tag/reset/clean/PR；不改包版本、TFM、API baseline、性能/容量阈值。I02 只增加对仓库现用版本的显式引用，不升级版本。
- 本地测试继续关闭外部门控，不继承生产连接串，不用删除 AOP、过滤器或断言绕过失败。不批量修改未关联的 148 个警告。
- 以下 R-SQL、R-G01、R-G02 和候选功能**不在冻结 7 项内**。新根因或范围扩大必须先补证据、分类；不可借本计划自动实现所有 TODO。
- 每项实现前输出 Change Impact Analysis：ChangedFiles/Projects/PublicContracts/RuntimePaths/Providers/TFMs/BuildPackaging/BenchmarkHarness/DocsOnly、AffectedDependents、RiskLevel。

## Phase A：恢复构建与测试装配

### I01 · P0 · 对应 F001：移除合并带回的重复验证特性

- **目标/证据**：消除 CS0729/CS0436，恢复 AOP 依赖图。`solution-build.log` 与 `merge-aop-cross-impact-confirmed.log` 已确认重复类型来源。
- **确定文件**：`framework/src/Bing.Aop.AspectCore/Bing/Aspects/ValidAttribute.cs`；核对 `Properties/TypeForwardedTypes.cs` 与 `framework/src/Bing.Validation.Abstractions/Bing/Validation/ValidAttribute.cs`。测试范围为 `Bing.Aop.AspectCore.Tests`、`Bing.Validation.Tests`。
- **步骤**：删除 AOP 内恢复的同名旧定义；保持中立 Attribute、转发器、参数验证选择器/适配器。检查旧程序集限定名解析与 LegacyConsumer 运行结果；已有断言足够时不添加重复测试。
- **依赖**：无；AOP 测试全 TFM 运行同时依赖 I04 的复制目标修复。
- **验证**：AOP 与 Validation.Abstractions 双 TFM/实际声明 TFM 构建；AOP 反射、旧消费者、参数验证测试；随后恢复 Events、应用 AspectCore、admin 的构建。
- **风险**：误删中立类型或转发器会破坏二进制兼容；不得改 public API baseline 消除报错。
- **验收**：唯一中立定义、旧类型名仍可加载、验证成功/失败/异常路径保持；主方案不再出现此类型冲突。

### I02 · P1 · 对应 F002：恢复旧测试助手编译与单次执行断言

- **目标/证据**：`tests/Bing.Tests.Samples` 两个 TFM 的 CS0234 来自 AssertHelper 的 AspectInvocationException。
- **确定文件**：`tests/Bing.Tests.Samples/Bing.Tests.Samples.csproj`；`Bing/Tests/XUnitHelpers/AssertHelper.cs`。候选新增测试文件限定同项目的助手回归测试。
- **步骤**：显式引用现用 `AspectCore.Core` 3.0.0；不在公共 props 扩散依赖。助手单次捕获 action 的异常，再检查直接异常或包装 InnerException；避免当前 catch 后再次执行 action。新增同步/异步、直接/包装、错误类型、无异常的本地测试，使用计数证明 action 一次调用，保留 keyword 断言语义。
- **依赖**：可独立编辑；完整聚合测试依赖 I01。
- **验证**：restore/build net6.0、net8.0；只运行上述本地助手用例。聚合项目其他用例先审查门控，不无过滤执行外部服务测试。
- **风险**：移除包装异常支持或重复调用 action 会掩盖真实失败。
- **验收**：缺失引用错误归零，直接/包装异常路径正确，不升级包或改变其它项目依赖。

### I03 · P1 · 对应 F003：修正 MySQL 集成夹具装配

- **目标/证据**：MySQL fixture 残留旧 using，且调用 EnableAop 所属工程未直接引用。
- **确定文件**：`framework/tests/Bing.Dapper.MySql.Tests.Integration/Bing.Dapper.MySql.Tests.Integration.csproj`、`Infrastructure/MySqlIntegrationDatabaseFixture.cs`；同项目 FixtureTest。
- **步骤**：删除失效的 `AspectCore.Extensions.DependencyInjection` using；直接 ProjectReference 仓库 `Bing.Aop.AspectCore`。保留实际 EnableAop 装配及所有外部门控。
- **依赖**：I01。
- **验证**：双 TFM restore/build；`ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection` 在外部门控关闭时执行；SQL/Provider 创建链的 Phase 检查点运行 SQLite 本地集成。
- **风险**：使用真实连接串启动 fixture、删掉 AOP 装配造成测试语义变化。
- **验收**：两个 TFM 编译通过，无数据库连接的装配测试通过；未授权外部 MySQL 测试依然跳过/不运行。

### I04 · P1 · 对应 F004：统一多目标复制目标与配置传播

- **目标/证据**：消除外层 GetTargetPath 的 MSB4057，以及 Release 查找 Debug 产物产生的 MSB3030。
- **确定文件**：`framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj`、`framework/tests/Bing.Caching.CSRedis.Tests.Integration/Bing.Caching.CSRedis.Tests.Integration.csproj`、`framework/tests/Bing.Aop.AspectCore.Tests/Bing.Aop.AspectCore.Tests.csproj`、`framework/tests/Bing.FreeSQL.Tests/Bing.FreeSQL.Tests.csproj`、`Bing.All.sln`。
- **关联工程**：Core `PluginFixtures/` 内现有 8 个工程、CycleProbe、RateLimitProbe、OrderingProbe；仅当配置修复确需时调整其 ProjectReference 元数据，不修改其业务逻辑。
- **步骤**：给后置复制 Target 增加内层 TFM 条件（TargetFramework 非空，排除 cross-targeting 外层）；确保子项目构建和 GetTargetPath 传递相同 Configuration/TargetFramework；将缺失的实际 fixture/probe 纳入 solution 配置并启用正确 Release Build.0。移除当前 Core 的重复 PluginRuntime ProjectReference 可作为此工程图修复的一部分，不扩为全仓清理。
- **依赖**：与 I01 收敛后运行 AOP/FreeSQL 验证；I05 必须在此项之后修改同一 solution。
- **验证**：四个测试工程单独全 TFM Release build；主方案 Release build；检查当前构建生成的 fixture/probe 路径，并运行 Core 插件加载、AOP cycle、FreeSQL ordering 的本地相关测试。外部 RateLimit/Ordering 服务仍门控。
- **风险**：现有 output/debug/release 文件可能掩盖构建图错误。
- **验收**：在新建隔离验证副本（不删除用户输出）或可靠隔离输出路径下，无预存 DLL 仍能完成构建和复制；不吞错、不手工复制、不要求先手动构建每个 fixture。隔离副本需包含当前修复内容，不用只含 HEAD 的副本冒充当前候选。

### I05 · P1 · 对应 F005：主方案覆盖遗漏测试

- **目标/证据**：10 个 IsTestProject=true 的有效工程未列入当前主 SLN，默认验证图遗漏。
- **确定修改文件**：`Bing.All.sln`；既有 CI 使用主方案，优先只修该文件，不新增另一套相互分叉的项目名单。
- **加入的工程（路径相对于仓库）**：
  - `framework/tests/Bing.Validation.Tests/Bing.Validation.Tests.csproj`
  - `framework/tests/Bing.Uow.Tests/Bing.Uow.Tests.csproj`
  - `framework/tests/Bing.TextTemplating.Tests/Bing.TextTemplating.Tests.csproj`
  - `framework/tests/Bing.ExceptionHandling.Tests/Bing.ExceptionHandling.Tests.csproj`
  - `framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj`
  - `framework/tests/Bing.Caching.Tests/Bing.Caching.Tests.csproj`
  - `framework/tests/Bing.Security.Tests/Bing.Security.Tests.csproj`
  - `samples/Bing.Samples.WebApi.Tests/Bing.Samples.WebApi.Tests.csproj`
  - `modules/admin/tests/Bing.Admin.FreeSQL.Tests/Bing.Admin.FreeSQL.Tests.csproj`
  - `modules/admin/tests/Bing.Admin.EFCore.Tests/Bing.Admin.EFCore.Tests.csproj`
- **步骤**：添加工程与 Debug/Release 映射；核对各自依赖/probe 配置；重新比较有效测试工程集合与主验证图。Backup、历史 ai_docs 实验工程不加入。
- **依赖**：I04；WebApi/admin 可运行验证依赖 I01。
- **验证**：`dotnet sln Bing.All.sln list`、MSBuild IsTestProject/TargetFrameworks 求值、主方案 restore/build；最终检查点运行本地可执行用例和既有门控。
- **风险**：把外部集成误当本地测试，或主方案配置遗漏 Build.0。
- **验收**：10 项全部可从主入口发现，各 TFM 有明确执行/跳过/阻断归属；85 个生产项目仍全覆盖；不依靠人工临时补跑维持覆盖率。

## Phase B：修正公开文档

### I06 · P2 · 对应 F006：修复 XML 文档结构

- **目标/证据**：修复已定位的 CS1570/CS1710，避免生成 API 文档丢失。
- **确定文件**：`framework/src/Bing.Data.Sql/Bing/Data/Sql/` 下 `Extensions/Extensions.IFrom.cs`、`Extensions/Extensions.IJoin.cs`、`Extensions/MutationClauseExtensions.cs`、`Contexts/DatabaseContextSnapshot.cs`、`Extensions/Extensions.ICte.cs`、`Extensions/Extensions.IUnion.cs`、`Extensions/SelectClauseExtensions.cs`、`Queries/SqlConditionGroup.cs`；`framework/src/Bing.Permissions/Identity/Models/` 下 ApplicationBase.cs、RoleBase.cs、UserBase.cs。
- **步骤**：补齐 summary 闭合、合并重复 typeparam；保持中文契约准确，禁止改实现和成员签名。
- **依赖**：无业务依赖；在最终构建前完成。
- **验证**：两个受影响项目按全部声明 TFM 编译，检查指定告警消失，解析本次生成 XML；diff 确认为纯注释。
- **风险**：注释错误宣称未实现能力。只按真实代码描述。
- **验收**：指定 XML 告警归零、生成文档可解析、实现/API 不变。本项不改 SQL 输出、映射、缓存或 Builder，因此不新增镜像测试、不重跑 benchmark；符合仓库按实际影响选择验证的要求。

### I07 · P2 · 对应 F007：纠正当前实现与验证状态文档

- **目标/证据**：当前文档把已实现事件排空列为缺失，把 FreeSQL 兼容执行当 stub；Payments solution 状态和工程数量陈旧。
- **确定文件**：`docs/operations/文档与代码对照清单.md`、`docs/operations/测试指南.md`、`docs/modules/domain.md`、`docs/modules/events.md`、`docs/modules/data-access.md`、根 `README.md`。候选文件为这些当前页面直接引用且含同一错误的现行说明，需先定位再修改，不全库替换数字。
- **步骤**：分别记录已实现/受限/本次通过/构建阻断/外部待验收；统一 70 framework + 3 components 与 12 admin 的口径；保留两个确定缺口及两个候选的区别。历史有日期的 ai_docs 报告不回写。
- **依赖**：最终措辞依据 I01–I06 实际验证结果；如尚有阻断就如实保留。
- **验证**：`python eng/ci/sync-docs-index.py --check` 与 `python eng/ci/check-docs.py`；人工对照源码符号与执行证据。仅文档修改不重跑代码套件。
- **风险**：把“有代码”写成“已完整验收”，或把本审查的计数写成永远不变的仓库事实。
- **验收**：当前说明与实现一致、时间/候选明确、无伪造测试和性能结论。

## 顺序与符号可追溯性

推荐顺序：I01 → I02/I03 → I04 → I05 → I06 → 最终构建/测试 → I07 文档收口。I02 与 I03 的编辑范围可分离，但本计划不自行调度；solution 的 I04/I05 必须串行集成。

| 生产符号 / 行为 | 测试项目与方法 / 验证入口 | 性质 |
|---|---|---|
| `Bing.Validation.ValidAttribute` 的程序集身份与转发 | Bing.Aop.AspectCore.Tests：`AttributeReflectionTest.NeutralAttributes_ShouldResolveThroughLegacyAspectCoreAssemblyName`、`LegacyConsumerAssemblyTest.LegacyConsumer_ShouldResolveForwardedAttributes` | 现有 |
| ValidAttribute 中立契约 | Bing.Aop.AspectCore.Tests：`ValidationAttributeContractTest.Attributes_ShouldInheritDirectlyFromAttribute`、`Attributes_ShouldTargetParametersOnly`、`Attributes_ShouldKeepPublicNames` | 现有 |
| 参数验证适配器的正常/异常路径 | Bing.Aop.AspectCore.Tests：`ValidAttributeTest.ProcessObject_WhenParameterValidateThrows_ShouldPropagateException`、`ProcessObject_WhenParameterIsValidIVerifyModel_ShouldCallValidate`、`ProcessObject_WhenParameterIsNotIVerifyModel_ShouldNotThrow` | 现有 |
| `EnableAop` 与 MySQL 服务装配 | Bing.Dapper.MySql.Tests.Integration：`ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection` | 现有；运行前用实际类名筛选 |
| XML 注释的公开成员契约 | SQL/Permissions 全 TFM 编译、生成 XML 解析、纯注释 diff | 无运行时变化，不新增业务测试 |
| 工程图与复制 Targets | solution/四工程全 TFM build、隔离产物、现有插件/probe 消费测试 | MSBuild 验收，不虚构生产符号 |

Executor 在 execution.md 更新为**最终实际符号 → 实际测试项目/类/方法 → 证据路径**；新增助手测试写明新增标记，不能把本计划提议的名称当已有测试或已经通过。

## R-SQL：兼容决策后才可进入实现的高优先级路线

对应 F008，P1，`BLOCKED_APPROVAL`。确认缺陷而非候选功能；不计入两项“未实现功能”。`artifacts/scope-probe/Program.cs` 已在真实 ServiceProvider 中无数据库复现。现有 Singleton 注册测试明确体现旧契约，不能默默改 Scope。

**建议决策**：使 Query/Executor/Multiple/Transaction factory 绑定调用 scope；无状态元数据/注册表继续 Singleton，后台任务显式创建 scope。维护者需要确认 Singleton 消费者的迁移方式和是否接受 DI 生命周期行为变更。不可选择把所有 IDataFilter 改 Singleton、关闭 scope validation 或隐藏根解析异常来“通过”。如果采用不改变生命周期的兼容方案，应先证明能可靠获得调用 scope，不能使用未定义的环境全局容器。

**决策后确定核查文件**：

- `framework/src/Bing.Dapper.Core/Bing/Data/Sql/DapperServiceCollectionExtensions.cs`、`SqlFactoryBase.cs`、Query/Executor/Multiple/Transaction 各工厂与 `SqlQueryBase.cs`。
- `framework/src/Bing.Data/Bing/Data/Filters/DataFilter.cs`、IDataFilter.cs 作为语义约束；默认不改其隔离定义。
- `framework/tests/Bing.Dapper.Core.Tests/DependencyInjection/DapperCoreServiceCollectionExtensionsTest.cs`、`Factories/SqlFactoryTest.cs`、SQLite 集成 fixture 及过滤测试；五种 Provider 的直接测试项目。
- 候选变更仅在实际调用链要求时进入：SqlBuilderServices、映射 resolver/provider、数据库上下文和 Builder clone。不得因本路线全面重写这些组件。

**依赖顺序**：批准生命周期/迁移契约 → 先落失败回归 → 注册/创建链实现 → 五 Provider 直接契约测试 → SQLite 实际结果 → 精准性能 probe → 更新文档与最终 fresh gate。

**必须满足的 SQL 测试门槛**：

1. 新建独立 scope 契约测试类；ValidateScopes=true/false 均覆盖。同 scope 共享过滤状态，不同 scope 隔离；Disable/Enable 嵌套恢复、异常释放、跨 await、并发独立 scope 均测试。禁止仅从根容器同时取 filter/factory。
2. Query、Executor、Multiple、Transaction 四个创建入口各有直接测试；Provider 选择、显式执行上下文优先级、默认上下文、命中/未命中以及重复渲染覆盖，不能只依赖一个综合集成用例。
3. MySQL/PostgreSQL/SQLServer/Oracle/SQLite 每个受影响分支：成功及明确失败场景，所有生成 SQL 用完整字符串 Assert.Equal；不得只 Contains。SQLite 还要断言真实软删除数据查询结果与 scope 开关一致。
4. 若改变 IEntityMappingResolver、IEntityModelMetadataProvider、表命名/架构/对象名格式化/单表或跨库校验：接口契约、每个默认实现的独立测试类、每个受影响 Provider 分支均直接覆盖。变更缓存键必须新增隔离、命中、未命中；不得用“集成已经绿”省略。
5. Clone/延迟渲染涉及可变状态时，原件与副本隔离、反复 ToSql 一致、上下文切换行为都要验证。资源释放覆盖 scope dispose、查询异常、连接/事务所有权；不承诺同一 mutable Query 可跨线程共享。
6. Bing.Data.Sql.Benchmarks：先做热路径影响分析。若映射缓存/格式化/Builder clone 改变，更新对应基线**场景或说明不适用原因**，不得覆盖既有 baseline 数据或改预算。仅工厂生命周期改变时至少有 scope 创建/首次查询的有界 probe；正式性能与 smoke 结论分开。

**建议新增映射（目前不存在，决策后实现并替换为实际名称）**：

| 最终生产符号/责任 | 拟增测试类.方法 | 项目 |
|---|---|---|
| AddSqlCore 生命周期 | SqlFactoryScopeContractTest.Create_WithScopeValidation_ShouldResolveScopedFilter | Bing.Dapper.Core.Tests |
| SqlFactoryBase 创建上下文 | SqlFactoryScopeContractTest.Factories_ShouldUseCallingScope / ExplicitContext_ShouldTakePrecedence | Bing.Dapper.Core.Tests |
| SqlQueryBase Builder 服务 | SqlFactoryScopeContractTest.DisableSoftDelete_ShouldChangeCompleteSql / SeparateScopes_ShouldIsolateState | Bing.Dapper.Core.Tests |
| 渲染/Clone | SqlFactoryScopeContractTest.RepeatedRender_ShouldPreserveScope / Clone_ShouldNotShareMutableQueryState | Bing.Dapper.Core.Tests |
| 五 Provider 语义 | 对应 ProviderScopeContractTest 的正常过滤、禁用过滤、非法上下文完整 SQL 用例 | 五个 Bing.Dapper.*.Tests |
| SQLite 真正执行 | SqliteScopeFilterTest.DisableSoftDelete_InRequestScope_ShouldReturnDeletedRows / ScopeDisposal_ShouldRestoreIsolation | Bing.Dapper.Sqlite.Tests.Integration |

**验收**：诊断两场景转绿、上述直接测试及 SQLite 全套双 TFM 通过，契约迁移说明获确认，未引入新的 scope/连接泄漏。未批准前本路线维持 BLOCKED_APPROVAL，不扩大冻结分母。

## 新功能路线与外部工作

- R-G01 微信退款、R-G02 全局领域验证：详细设计决策/里程碑见 [backlog.md](backlog.md)。先完成产品/协议/契约决定，再冻结单独实现任务。本轮不承诺全部实现。
- C01 OAuth 统一工厂、C02 租户文化自动选择：先决定是否属于产品支持面；不能由空接口或 TODO 推定必须新增 API。
- 外部门禁 F011–F013 与 CodeSmith 工具链记录独立状态。先恢复本地构建，再由维护者提供隔离环境执行已有 Provider/admin/缓存命令；不接生产库，不通过放宽阈值结束任务。

## 最终 Fresh 检查点 V-FINAL（不计为第 8 个实现项）

修复候选冻结后，由独立验证方对明确 HEAD + 当前 diff 候选执行一次；此检查点不能只复用本次审查的旧二进制。若父会话另批准 R-SQL，合入同候选后再执行此最终检查点，避免两次无意义全量验证。

1. 保存 git status/diff、SDK/运行时、project/TFM/Release Build.0 清单，确认85个生产项目均在验证范围；新项目按实际增量登记。隔离构建目录/副本不覆盖用户文件。
2. `dotnet restore Bing.All.sln -v minimal -m:2 -nr:false`（900秒）。
3. `dotnet build Bing.All.sln -c Release --no-restore -v minimal -m:2 -nr:false`（900秒）。solution 外 WinForms 单独 restore/build（各300秒）；重新盘点若有 solution 外生产项目必须逐项补建。net45 CodeSmith 若环境不变直接引用 F012 环境阻断，禁止为了报告全绿省略。
4. 对全部当前本地单元项目及新纳入项目按其声明 TFM 运行 fresh test；沿用 artifacts/test-local.py 的安全选取原则，扩展 I01/I05 解锁的本地项目，不能照抄旧32项目名单遗漏新范围。典型命令：`dotnet test <project> -c Release --no-build --no-restore -p:RunSettingsFilePath= -m:1 -nr:false --logger trx --results-directory <新的候选证据目录>`。每项目300秒，Core/缓存确需时预先600秒。不能单凭 --no-build 使用未经本次build产出的 DLL。
5. 必跑 `framework/tests/Bing.Dapper.Sqlite.Tests.Integration/Bing.Dapper.Sqlite.Tests.Integration.csproj` 的全部本地 net6.0/net8.0；MySQL 装配用例仅选无数据库项；EFCore/FreeSQL/EventBus/admin/WebApi 先检查 fixture/门控，执行其本地单元/smoke，外部项保持显式未运行或既有跳过理由。旧聚合测试不允许未经门控审查直接全量运行。
6. 聚合 TRX：通过、失败、跳过、未运行项目分别列出；测试重复TFM不当功能数量。执行 git diff --check、文档两项检查；保留最终生产符号→方法映射。
7. I01–I07 未改运行热路径时不强制再次执行全量 benchmark；当前 Dry smoke 可作为未变化路径的 smoke 证据，但正式性能仍 BLOCKED_EXTERNAL。若批准且实施 R-SQL，则按其影响运行对应有界 probe，记录配置/实际测量/限制。
8. 独立 Reviewer 对照 plan、execution、真实代码与当前 diff，写 review.md，逐条更新状态。完整构建仍失败就不能宣称“完整编译”；新错误先定位再判断是否本地可执行，不自动调整版本/API。

所有执行命令需有限窗口、受控终止子进程树、原始日志与退出码。可复用 `artifacts/run.py` 的记录方式，但未来证据必须写入新目录/前缀，不能覆盖初审数据。

## 证据复用、停止与交付

- 中间轮仅当相关源码、测试/二进制或 harness、TFM/runtime、环境类别均未改变，才记 `REUSED_UNCHANGED_SCOPE` 并引用精确证据；不用整个仓库的无关文档 diff 强迫全量重跑。基于 git diff 和明确作用域足够时不新增无价值全量哈希流水线。
- 最终 fresh build/test 是本计划明确例外，必须针对最终候选运行一次。其后只改报告时复用该结果，只做文档/格式检查；新代码或项目图变化则按影响重新验证。
- 历史 Redis 性能报告不能当当前通过；没有同构 before 时标 NOT_APPLICABLE。未批准预算或缺外部环境分别 BLOCKED_APPROVAL/BLOCKED_EXTERNAL。
- 同一 Finding+Candidate+Workload+Environment+Hypothesis 最多诊断一次、正式确认一次；连续两轮没有根因/候选/证据变化停止；自动 Review→Fix→Review 最多3轮，并且只有 OPEN_ACTIONABLE>0 才能继续。
- Executor 输出 execution.md：当前候选、I01–I07完成数、变更影响、每条验收证据及未满足项。Reviewer 独立输出 review.md，不能改写审查原始事实。Completed/Open Actionable/Blocked Approval/Blocked External/Not Applicable/Accepted Limitations/Verified Boundaries/Deferred 分组输出。
- 冻结7项全部关闭也不等于可发布：F008、两项功能设计和外部/性能门禁需单独决定。没有可执行项时停止，交回父会话；不因 PARTIAL、NOT_VERIFIED 或 RELEASE_STATUS!=PASS 自动继续修复。
