<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261021-merged-code-audit
AI_EXECUTION_FINISHED_AT: 2026-10-20T18:20:26.880Z

# 实施执行报告

> 本实施阶段已完成。最终 fresh `V-FINAL` 与独立 Review 由父会话后续执行。

## 执行结论

- 状态：实施阶段完成，I01–I07 共 7/7 项。最终 fresh `V-FINAL` 由父会话后续独立验收，本报告不宣称已通过最终独立验收。
- 基线候选：`a99a16c4b55c407cc81d771822667044e2e9e9b1`。
- 证据目录：`artifacts/implementation/`；Astra 初审证据保留在原有 `artifacts/` 路径。

## 任务信息

- Task ID：`20261021-merged-code-audit`
- 执行器：Codex / Luna 实现阶段
- 执行开始：`2026-10-20T16:40:56.815Z`（由仓库 task-state 注册器记录）
- 冻结范围：I01–I07。R-SQL/F008、退款/全局规则、OAuth/文化候选不在实施范围。

## 基线与既有改动

- 初始 `git status --short`：仅 `?? ai_docs/tasks/20261021-merged-code-audit/`。该目录在本轮开始前已含 `audit.md`、`plan.md`、`backlog.md` 与初审 `artifacts/`；保留原文件，不覆盖初审证据。
- 初始 `git diff --stat` 与 `git diff --cached --stat` 均为空；没有初始 tracked 或 staged 改动。
- 初始 HEAD：`a99a16c4b55c407cc81d771822667044e2e9e9b1`。
- 初始环境和 Git 证据见 [baseline.md](artifacts/implementation/baseline.md)。

## 计划执行情况

| 项目 | 状态 | 实际证据 |
|---|---|---|
| I01 | COMPLETED | 两个真实 TFM 的 Validation.Tests 各 186/186 通过；AOP 兼容/验证用例双 TFM 各包含在 25/25 通过集中 |
| I02 | COMPLETED | 双 TFM Release build 通过；助手回归用例 net8.0、net6.0 各 12/12 通过 |
| I03 | COMPLETED | restore 与 Release build 双 TFM 通过；无数据库装配测试双 TFM 各 1/1 通过 |
| I04 | COMPLETED | 四工程双 TFM 隔离 Release build 通过；8 个 fixture 与 3 个 probe 输出齐全；Core/AOP/FreeSQL 本地回归 134/134；主方案 Release build 通过 |
| I05 | COMPLETED | 十个遗漏测试项目全部加入并核对 IsTestProject/TFM/Release Build.0；主方案 Release build 通过 |
| I06 | COMPLETED | Bing.Data.Sql netstandard2.0 与 Bing.Permissions net6.0 Release build 通过；计划指定 XML 成功解析，Permissions 重复 typeparam 已核对 |
| I07 | COMPLETED | 根 README 与派生页、包/测试指南及 domain/events/data-access 文档已按源码与本次证据更新；同步检查、文档 lint 和 diff check 通过 |

## 变更影响与验证策略

测试前按项记录 `ChangedFiles`、`ChangedProjects`、公开契约、运行路径、Provider、TFM、构建/打包图、Benchmark harness、文档影响、受影响依赖及风险等级。先运行 L0/L1 精准检查；阶段检查点再按计划扩展。所有命令保存 stdout、stderr、退出码、候选、TFM 与限时结果到 `artifacts/implementation/`。不运行 R-SQL，不使用生产/外部数据库，不提交或推送。

### I01 阶段验证影响分析

- ChangedFiles：删除 `framework/src/Bing.Aop.AspectCore/Bing/Aspects/ValidAttribute.cs`。
- ChangedProjects：`Bing.Aop.AspectCore`；受影响消费者为 AOP 与 Validation 测试项目。
- ChangedPublicContracts：移除重复定义，由 `Bing.Validation.Abstractions` 的同名中立类型及 AOP 类型转发维持旧限定名解析。
- ChangedRuntimePaths：验证标记仍由 `BingParameterInterceptorSelector` 适配为内部参数拦截器；不更改执行器。
- ChangedProviders：无。
- ChangedTFMs：生产 AOP 项目实际声明 net6.0，Validation.Abstractions 实际声明 netstandard2.0；AOP/Validation 测试工程声明 net8.0 与 net6.0。
- ChangedBuildPackaging：程序集类型来源与转发边界；不改包版本或 API baseline。
- ChangedBenchmarkHarness：否；不涉及运行热路径。
- ChangedDocsOnly：否。
- AffectedDependents：AOP 代理消费者、旧程序集限定名消费者、Validation 测试。
- RiskLevel：MEDIUM（旧程序集类型兼容由转发器保障，需运行既有旧消费者和参数验证用例）。
- 阶段策略：先按真实项目声明做 AOP net6.0 与 Validation.Abstractions netstandard2.0 L0 build；I04 修复跨 TFM 复制目标后，再做 AOP/Validation 测试的 net8.0、net6.0 测试及旧消费者测试。
- 计划澄清：冻结计划中的“双 TFM/实际声明 TFM”按真实项目文件解释；生产 AOP 与 Validation.Abstractions 并非双目标项目，测试工程才声明双 TFM。不改项目 TFM。

### I02 实施影响分析

- ChangedFiles：`tests/Bing.Tests.Samples/Bing.Tests.Samples.csproj`、`Bing/Tests/XUnitHelpers/AssertHelper.cs`、新增 `Bing/Tests/XUnitHelpers/AssertHelperTest.cs`。
- ChangedProjects：`Bing.Tests.Samples`。
- ChangedPublicContracts：只改变现有测试助手内部断言执行策略，无生产 API 变化。
- ChangedRuntimePaths：本地测试断言助手调用路径；同步/异步异常捕获为单次 action 调用。
- ChangedProviders：无。
- ChangedTFMs：测试项目 net6.0、net8.0。
- ChangedBuildPackaging：测试项目增加对现用 `AspectCore.Core` 3.0.0 的直接依赖，无版本升级。
- ChangedBenchmarkHarness：否。
- ChangedDocsOnly：否。
- AffectedDependents：使用测试助手的 `Bing.Tests.Samples` 聚合测试；只运行新增助手用例，不执行其他可能访问外部服务的聚合用例。
- RiskLevel：LOW（测试工具代码局部修改，无生产调用路径）。
- 阶段策略：双 TFM build 后仅运行 `AssertHelperTest` 精准用例。

### I03 阶段验证影响分析

- ChangedFiles：`framework/tests/Bing.Dapper.MySql.Tests.Integration/Bing.Dapper.MySql.Tests.Integration.csproj`、`Infrastructure/MySqlIntegrationDatabaseFixture.cs`。
- ChangedProjects：`Bing.Dapper.MySql.Tests.Integration`。
- ChangedPublicContracts：无生产 API 变化。
- ChangedRuntimePaths：MySQL 测试服务装配；测试仅解析服务，不打开数据库连接。
- ChangedProviders：MySQL Provider 工程引用；外部 MySQL fixture 仍由 `RUN_INTEGRATION_TESTS=0` 门控。
- ChangedTFMs：集成项目 net8.0、net6.0。
- ChangedBuildPackaging：增加仓库 `Bing.Aop.AspectCore` ProjectReference；移除失效的 AspectCore.Extensions.DependencyInjection using，不更改 NuGet 版本。
- ChangedBenchmarkHarness：否。
- ChangedDocsOnly：否。
- AffectedDependents：该项目内 MySQL 测试；精准运行无数据库 `Infrastructure.MySqlIntegrationDatabaseFixtureTest.ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection`。
- RiskLevel：MEDIUM（数据库 Provider 集成项目装配变化；运行器会清理连接/密钥环境变量并关闭外部门控）。
- 阶段策略：双 TFM Release build，再只筛选无数据库服务注册用例。

### I04 阶段验证影响分析

- ChangedFiles：Core、CSRedis、AOP、FreeSQL 四个测试 csproj；`Bing.All.sln`。
- ChangedProjects：四个主测试工程、Core 的 8 个 fixture 工程、AOP CycleProbe、CSRedis RateLimitProbe、Events OrderingProbe。
- ChangedPublicContracts：无生产 API 变化。
- ChangedRuntimePaths：测试构建后的插件 fixture、AOP cycle、Redis rate-limit、Events ordering 探针复制路径。
- ChangedProviders：Redis/MySQL 相关探针仅构建；外部服务测试维持门控关闭。
- ChangedTFMs：四个测试工程按 net8.0/net6.0 目标编译，子工程继承其声明目标。
- ChangedBuildPackaging：构建/solution 图；内层 TFM 与 Configuration 传播；无包版本变化。
- ChangedBenchmarkHarness：否。
- ChangedDocsOnly：否。
- AffectedDependents：AOP、Core 插件加载、CSRedis 外部限流、FreeSQL/Events ordering 测试消费者。
- RiskLevel：HIGH（跨目标 MSBuild target 与 solution 配置变化，旧产物可能掩盖 Release/TFM 错误）。
- 阶段策略：先在无 `bin`/`obj` 的候选隔离副本恢复；四工程逐项全 TFM Release build，检查输出复制路径；只运行本地插件/cycle/ordering 用例。I05 的十个测试工程在 I04 solution 修改与验收后再单独串行增加；主 solution build 在完整工程图检查点执行。

### I05 阶段验证影响分析

- ChangedFiles：仅 `Bing.All.sln`，增加计划列明的十个有效测试工程与 solution 配置映射。
- ChangedProjects：Validation、Uow、TextTemplating、ExceptionHandling、EasyCaching、Caching、Security 七个框架测试工程，Samples.WebApi 一个样例测试工程，Admin.FreeSQL 与 Admin.EFCore 两个测试工程及其生产依赖，共十个计划指定工程。
- ChangedPublicContracts：无生产 API 修改；变化是默认 solution 验证图覆盖面。
- ChangedRuntimePaths：测试工程发现、restore/build 和 CI 主入口。
- ChangedProviders：EasyCaching/Caching、Admin.FreeSQL、Admin.EFCore、WebApi；阶段 build 不执行外部服务用例，统一关闭集成门控。
- ChangedTFMs：读取十个工程声明并逐项核对；不修改任何 TFM。
- ChangedBuildPackaging：`Bing.All.sln` 项目与 Debug/Release 配置映射，85 个生产工程原覆盖需保持。
- ChangedBenchmarkHarness：否。
- ChangedDocsOnly：否。
- AffectedDependents：主方案 restore/build 与后续本地测试选择范围。
- RiskLevel：HIGH（项目配置映射扩大默认工程图；外部集成不能误运行）。
- 阶段策略：I04 solution edit 与隔离 targeted 检查已先完成；现在仅串行增加 I05 十个项目，再执行 solution list、IsTestProject/TargetFrameworks/Build.0 核对、主方案 restore/build。该主方案 build 同时收口 I04 和 I05 的工程图检查点，避免在同一候选上重复构建整张图；不替代父会话的 V-FINAL。

### I06 阶段验证影响分析

- ChangedFiles：IFrom/IJoin/MutationClause/DatabaseContextSnapshot/ICte/IUnion/SelectClause/SqlConditionGroup 八个 Bing.Data.Sql 注释文件；Permissions `ApplicationBase.cs`、`RoleBase.cs`、`UserBase.cs` 三个 partial 类型注释文件。
- ChangedProjects：`Bing.Data.Sql` 与 `Bing.Permissions`。
- ChangedPublicContracts：仅修复生成 XML 文档结构与重复泛型文档标签；不改变 API、成员签名或描述的业务能力。
- ChangedRuntimePaths：无。
- ChangedProviders：无。
- ChangedTFMs：MSBuild 实际求值为 Bing.Data.Sql `netstandard2.0`、Bing.Permissions `net6.0`；按声明 TFM 编译，不改 TFM。
- ChangedBuildPackaging：只影响生成的 XML 文档文件。
- ChangedBenchmarkHarness：否；不涉及 SQL 运行时、Builder、缓存或输出。
- ChangedDocsOnly：是；源码 XML 注释元数据。
- AffectedDependents：使用两项目生成 API XML 的消费者。
- RiskLevel：LOW（只改注释标签）。
- 阶段策略：两项目 Release build；检查计划列明的 CS1570/CS1710 不再出现并解析新生成 XML。SQL 语义、映射与 SQL 字符串不变，因此不新增测试、不重跑 benchmark。

### I03/I04 主方案构建阶段关联修复影响分析

- ChangedFiles：`framework/tests/Bing.Tests/Bing.Tests.csproj`、`framework/tests/Bing.Aop.AspectCore.Tests/Bing.Aop.AspectCore.Tests.csproj`。
- ChangedProjects：旧聚合 `Bing.Tests` 与 AOP 测试工程及其本地测试依赖。
- ChangedPublicContracts：无；只补测试工程对仓库内 AOP adapter 的直接引用与项目引用构建元数据。
- ChangedRuntimePaths：`Bing.Tests` 已有 AOP DI 测试的编译依赖；AOP LegacyConsumer 的配置/TFM 输出查找。
- ChangedProviders：无；外部集成门控保持关闭。
- ChangedTFMs：`Bing.Tests` net6.0/net8.0；LegacyConsumer 固定 net6.0，并由两个 AOP 测试 TFM 消费。
- ChangedBuildPackaging：测试工程 ProjectReference 与 Configuration/TargetFramework 传播；不改变包版本。
- ChangedBenchmarkHarness：否。
- ChangedDocsOnly：否。
- AffectedDependents：主方案 Release build、Bing.Tests 内调用 AspectCore DI 的既有测试、AOP LegacyConsumer 反射兼容测试。
- 根因/范围关联：主方案首轮 build 在 `Bing.Tests` 的三个文件报 6 个 CS0234，源码使用 AOP DI 扩展但 csproj 缺仓库 adapter 引用；按 I03 的直接依赖修复模式补 ProjectReference。AOP build 另报 2 个 MSB3030：LegacyConsumer 项目引用在主 solution 子图下输出 Debug，而复制目标按 Release/net6.0 查找；按 I04 配置传播范围给引用固定当前 Configuration 与其单一 net6.0 TFM。
- RiskLevel：LOW（测试项目引用/构建元数据，不改生产实现或测试语义）。
- 阶段策略：对两个修复项目做双 TFM Release build，再重跑一次受控主方案 Release build；若仍有其他项目错误，按原边界记录待评审，不继续扩大修复范围。

## 已完成事项

- I01：删除 AOP 内重复 `ValidAttribute` 定义；Validation.Abstractions/netstandard2.0、AOP/net6.0、Validation.Tests/net6.0+net8.0 Release build 通过。Validation.Tests 双 TFM 各 186/186 通过；AOP 兼容与参数验证类包含在 I04 的双 TFM 各 25/25 通过结果中。
- I02：`AssertHelper` 同步/异步断言只调用一次并增加 12 个本地回归用例；双 TFM build 通过，net6.0 与 net8.0 各 12/12 通过。
- I03：MySQL 集成工程添加直接 AOP ProjectReference，移除失效 using；双 TFM restore/build 通过。仅运行 `ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection`，net8.0 与 net6.0 各 1 通过、0 失败、0 跳过，fixture 外部门控保持关闭。
- I04：四个测试项目在隔离候选双 TFM Release build 均退出 0。构建前候选无 `bin`/`obj`；构建输出含 8 个 Core fixture（V2 两个程序集位于 `PluginFixtures/V2/`，AssemblyName 与基础版本相同），CycleProbe、RateLimitProbe、OrderingProbe 各自位于 test output 的目标子目录。Core 插件类 net6.0/net8.0 各 32/32、AOP cycle/旧消费者/属性类各 25/25、FreeSQL SQLite ordering 类各 10/10；TRX 合计 134/134，0 失败、0 跳过。外部 Redis/Ordering 服务测试未运行。
- I04/I05 工程图检查点：加入 10 个遗漏测试项目，逐项 MSBuild 求值均为 `IsTestProject=true`、声明目标非空、Release `Build.0=True`；七个框架测试工程与 Samples.WebApi 共八个项目为 net8.0/net6.0，Admin.FreeSQL 和 Admin.EFCore 实际仅声明 net6.0。另将 AOP 测试直接消费的 LegacyMarkerConsumer 加入主方案以取得 Release 配置映射。最终主方案 restore/build 均退出 0；I05 新增项目的测试留给独立 V-FINAL。
- 主方案首轮 build 错误处理：首轮 6 个 `Bing.Tests` CS0234 和 2 个 AOP LegacyConsumer MSB3030。为 `Bing.Tests` 补仓库 AOP adapter ProjectReference（与 I03 直接依赖缺失同根因）；为 LegacyConsumer 引用显式设置 Configuration/TargetFramework，并在第二轮仍显示未登记项目只产 Debug 后将它加入 solution。第二轮只剩 2 个 MSB3030；最终第三轮主方案 Release build 0 错误、124 警告。三轮原始日志均保留，不覆盖失败证据。
- I05 本地 SQLite 集成：net8.0 151 通过 / 0 失败 / 1 跳过；net6.0 同为 151 / 0 / 1。唯一跳过项为 `SqliteExecutionIntegrationTest.ProviderContract_WhenSqliteScenariosRun_ShouldRecordRealIntegrationEvidence`，没有 Provider 源身份环境证据时由其自定义 Fact 门控跳过。测试连接仅使用 fixture 在系统临时目录创建的 SQLite 文件库。
- I03 关联的 `Bing.Tests` adapter 用例：net8.0 与 net6.0 各 7 通过 / 0 失败 / 0 跳过，覆盖 `ServiceCollectionTest`、`ValidTest` 和 `IocTest` 中实际依赖 AOP DI adapter 的方法。
- I06：8 个 Bing.Data.Sql summary 与 3 个 Permissions partial 类型注释已修复；`Bing.Data.Sql` netstandard2.0、`Bing.Permissions` net6.0 Release build 均退出 0、0 warnings、0 errors。生成的 SQL/Permissions XML 文档均可解析；Permissions 3 个类型分别有 2/3/2 个唯一 typeparam 文档项。证据见 `artifacts/implementation/i06/build/` 与 `artifacts/implementation/i06/xml/`。一次 XML 检查脚本自身的 PowerShell 插值错误退出 1，原始日志保留；修复脚本后重试成功。

I05 主方案首轮 build 状态与 I03/I04 关联修复日志：`artifacts/implementation/i05/solution-build/`。恢复错误和具体修复范围见本节后续更新；该失败结果不删除或覆盖。

I01/I03 原始日志和元数据：`artifacts/implementation/i01/`、`artifacts/implementation/i03/`。
I02 原始日志和 TRX：`artifacts/implementation/i02/`。
I04 构建、TRX 与 fixture/probe 输出核对：`artifacts/implementation/i04/`；隔离候选：`artifacts/implementation/isolation/candidate-i04/`。I04/I05 主方案最终工程图证据：`artifacts/implementation/i04-i05/solution-build/after-legacy/`。
I06 build、生成 XML 与解析结果：`artifacts/implementation/i06/`。
I07 文档派生、同步检查、lint 与 diff 检查：`artifacts/implementation/i07/docs/`。第一次 lint 发现根 README 更新后派生页尚未同步；执行仓库生成脚本后复查全部通过，失败日志保留。早期 Windows Python batch shim 检查遗留一个无子进程的 `cmd.exe`，按命令行核实后定向结束；PID 清理复核通过，见 `artifacts/implementation/finalize/verify_document_check_process_cleanup_retry.*`。

## 部分/未完成事项

- 本实施阶段 7/7 项均已完成；父会话安排的最终 fresh `V-FINAL` 与独立 `review.md` 尚未执行，不在本报告中宣称通过。
- I05 新纳入主方案的十个测试项目已核对 `IsTestProject`、TFM、Release Build.0 并由主方案编译；除本报告明确列出的局部用例外，未逐项运行十个项目的全部测试。
- 外部数据库、Redis、CAP broker、OAuth/支付真实服务和正式性能预算均未运行；相关门控保持关闭，属于外部/后续验收，不计为通过或失败。
- 主方案最终阶段检查点有 124 warnings、0 errors；Warnings 及独立 CodeSmith/.NET Framework 4.5 环境外范围不由本阶段宣称清零。

## 修改文件

- I01：删除 `framework/src/Bing.Aop.AspectCore/Bing/Aspects/ValidAttribute.cs`。
- I02：修改 `tests/Bing.Tests.Samples/Bing.Tests.Samples.csproj` 与 `Bing/Tests/XUnitHelpers/AssertHelper.cs`；新增 `Bing/Tests/XUnitHelpers/AssertHelperTest.cs`。
- I03/主方案关联修复：修改 MySQL Integration csproj 与 `Infrastructure/MySqlIntegrationDatabaseFixture.cs`；为 `framework/tests/Bing.Tests/Bing.Tests.csproj` 增加 AOP adapter 引用。
- I04：修改 `Bing.Core.Tests.csproj`、`Bing.Caching.CSRedis.Tests.Integration.csproj`、`Bing.Aop.AspectCore.Tests.csproj`、`Bing.FreeSQL.Tests.csproj` 的 TFM/Release 复制配置；`Bing.All.sln` 登记相关 fixture/probe 与 LegacyMarkerConsumer。
- I05：在 `Bing.All.sln` 增加计划列明的十个测试项目。
- I06：修改 `framework/src/Bing.Data.Sql/Bing/Data/Sql/` 下八个 XML 注释文件，以及 `framework/src/Bing.Permissions/Identity/Models/ApplicationBase.cs`、`RoleBase.cs`、`UserBase.cs`。
- I07：更新根 `README.md`、生成页 `docs/index.md`、包索引、工程化文档、文档与代码对照清单、测试指南及 domain/events/data-access 模块页。
- 新增的 `ai_docs/tasks/20261021-merged-code-audit/artifacts/implementation/` 保存本阶段 baseline、隔离候选、stdout/stderr、命令元数据与 TRX；`execution.md` 与初审文档共存于任务目录，未覆盖 Astra 证据。

## API/数据/配置变化

- 没有更改生产方法签名、数据库 schema、版本号、TFM、API baseline、性能阈值或 SQL 运行时行为。
- I01 移除 AOP 程序集中的重复 `ValidAttribute` 类型定义；中立类型由 `Bing.Validation.Abstractions` 提供，并由 AOP 类型转发保留旧程序集限定名解析。
- I02 修正测试断言助手的执行次数与 AspectCore 异常展开；测试样例项目直接引用其实际使用的 AspectCore.Core 3.0.0。
- I03/I04/I05 仅调整测试项目依赖、生成物复制配置和主 solution 项目图；没有启用外部集成门控。
- I06 只改变 XML 文档注释标签；I07 只修正现行文档与派生索引。

## 测试结果

- I01 Validation.Tests：net8.0 186 通过 / 0 失败 / 0 跳过；net6.0 186 / 0 / 0。TRX：`artifacts/implementation/i01/validation/trx/net8.0/i01_validation_net8.trx`、`artifacts/implementation/i01/validation/trx/net6.0/i01_validation_net6.trx`。
- I02 `AssertHelperTest`：net8.0 12 通过 / 0 失败 / 0 跳过；net6.0 12 / 0 / 0。TRX：`artifacts/implementation/i02/trx/net8.0/i02_assert_helper_net8.trx`、`artifacts/implementation/i02/trx/net6.0/i02_assert_helper_net6.trx`。
- I03 MySQL 无数据库装配：net8.0 1 通过 / 0 失败 / 0 跳过；net6.0 1 / 0 / 0。TRX：`artifacts/implementation/i03/trx/net8.0/i03_mysql_net8.trx`、`artifacts/implementation/i03/trx/net6.0/i03_mysql_net6.trx`。
- I03 关联 `Bing.Tests` AOP DI adapter 精准用例：net8.0、net6.0 各 7 通过 / 0 失败 / 0 跳过；TRX：`artifacts/implementation/i03/bing-tests-refs/{net8.0,net6.0}/`。
- I04 Core：net8.0 32 通过 / 0 失败 / 0 跳过；net6.0 32 / 0 / 0。AOP：两个 TFM 各 25 / 0 / 0。FreeSQL SQLite ordering：两个 TFM 各 10 / 0 / 0。TRX 与运行元数据位于 `artifacts/implementation/i04/tests/{core,aop,freesql}/{net8.0,net6.0}/`。
- SQLite 集成：net8.0 151 通过 / 0 失败 / 1 跳过；net6.0 同为 151 / 0 / 1。TRX：`artifacts/implementation/sqlite/net8.0/i05_sqlite_integration_net8.trx`、`artifacts/implementation/sqlite/net6.0/i05_sqlite_integration_net6.trx`。

## Build/Typecheck/Lint/Format

- `Bing.Validation.Abstractions` Release build，netstandard2.0：退出码 0。
- `Bing.Aop.AspectCore` Release build，net6.0：退出码 0。
- MySQL Integration restore 与 Release build，net6.0/net8.0：退出码 0。
- Validation.Tests Release build，net6.0/net8.0：退出码 0。
- I04 Core.Tests、CSRedis Integration、AOP.AspectCore.Tests、FreeSQL.Tests 隔离 Release build，net6.0/net8.0：四工程均退出码 0；目标 MSB4057/MSB3030 不再出现。
- I04/I05 最终主方案 restore：退出码 0；Release build：退出码 0，124 warnings / 0 errors。两次早期 build 失败均保留在 `artifacts/implementation/i05/solution-build/` 与 `artifacts/implementation/i05/solution-build/after-associated-fixes/`。
- I06 `Bing.Data.Sql` Release build netstandard2.0 与 `Bing.Permissions` Release build net6.0：均退出码 0，0 warnings / 0 errors；生成 XML 的解析与类型文档标签计数验证退出码 0。
- I07 文档验证：`sync-docs-index.py --check` 退出码 0；`check-docs.py` 13 项全部通过（含 73/73 可发包清单、73 份 README 与徽章检查）；最终 execution.md 的 `git diff --check` 通过。各次检查的 stdout/stderr 与元数据均保留在 `artifacts/implementation/i07/docs/`。
- I04 build 日志另有非阻断 CS8632、NETSDK1138、xUnit2031 与若干 fixture AssemblyReference cache 的 MSB3101“already exists”警告；保留在原始 stdout 中供独立验收判断，未将其表述为 build 失败。

## 当前生产符号到验证方法映射

| 生产符号/责任 | 实际测试项目、类.方法 | 证据 |
|---|---|---|
| `Bing.Validation.ValidAttribute` 中立定义、旧类型名转发 | `Bing.Aop.AspectCore.Tests`：`AttributeReflectionTest.NeutralAttributes_ShouldResolveThroughLegacyAspectCoreAssemblyName`、`LegacyConsumerAssemblyTest.LegacyConsumer_ShouldResolveForwardedAttributes`、`ValidationAttributeContractTest.Attributes_ShouldInheritDirectlyFromAttribute`、`Attributes_ShouldTargetParametersOnly`、`Attributes_ShouldKeepPublicNames`、`ValidAttributeTest.ValidAttribute_ShouldBeContainerNeutralAttribute` | `artifacts/implementation/i04/tests/aop/{net8.0,net6.0}/` |
| `BingParameterInterceptorSelector` 参数验证适配 | `Bing.Aop.AspectCore.Tests`：`ValidAttributeTest.ProcessObject_WhenParameterValidateThrows_ShouldPropagateException`、`ProcessObject_WhenParameterIsNotIVerifyModel_ShouldNotThrow`、`ProcessObject_WhenParameterIsValidIVerifyModel_ShouldCallValidate`；`Bing.Validation.Tests` 全项目 186 项/TFM | `artifacts/implementation/i04/tests/aop/`、`artifacts/implementation/i01/validation/trx/` |
| `AssertHelper.Throws` 单次调用 | `Bing.Tests.Samples.AssertHelperTest`：`Throws_DirectException_CapturesOnceAndChecksKeyword`、`Throws_AspectInvocationException_CapturesInnerExceptionOnce`、`Throws_WrongDirectException_FailsAfterSingleInvocation`、`Throws_WrongWrappedException_FailsAfterSingleInvocation`、`Throws_NoException_FailsAfterSingleInvocation`、`Throws_KeywordDoesNotMatch_FailsAfterSingleInvocation` | `artifacts/implementation/i02/trx/{net8.0,net6.0}/` |
| `AssertHelper.ThrowsAsync` 单次调用 | `Bing.Tests.Samples.AssertHelperTest`：`ThrowsAsync_DirectException_CapturesOnceAndChecksKeyword`、`ThrowsAsync_AspectInvocationException_CapturesInnerExceptionOnce`、`ThrowsAsync_WrongDirectException_FailsAfterSingleInvocation`、`ThrowsAsync_WrongWrappedException_FailsAfterSingleInvocation`、`ThrowsAsync_NoException_FailsAfterSingleInvocation`、`ThrowsAsync_KeywordDoesNotMatch_FailsAfterSingleInvocation` | `artifacts/implementation/i02/trx/{net8.0,net6.0}/` |
| MySQL fixture 服务注册与 AOP 拦截依赖 | `Bing.Dapper.MySql.Tests.Integration`：`MySqlIntegrationDatabaseFixtureTest.ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection` | `artifacts/implementation/i03/trx/{net8.0,net6.0}/` |
| Core 插件 fixture 构建/复制目标 | `Bing.Core.Tests`：PluginLoading、PluginLifecycle、PluginPrivateDependency、PluginDependencyGraph 测试类；8 个实际 DLL 路径另以隔离构建输出核验 | `artifacts/implementation/i04/tests/core/` 与 `artifacts/implementation/isolation/candidate-i04/framework/tests/Bing.Core.Tests/bin/Release/` |
| AOP CycleProbe 复制目标 | `Bing.Aop.AspectCore.Tests`：`AutowiredCycleProcessTest.ResolveCycle_ShouldRemainInsideBoundedChildProcess`（7 个模式） | `artifacts/implementation/i04/tests/aop/` |
| FreeSQL OrderingProbe 复制目标 | `Bing.FreeSQL.Tests`：`FreeSqlOrderedEventTransactionFactoryTest` SQLite 本地测试类 | `artifacts/implementation/i04/tests/freesql/`；probe DLL 在隔离输出目录 |
| CSRedis RateLimitProbe 复制目标 | 当前阶段仅核验双 TFM Release 输出中的 probe DLL；RateLimit 外部服务测试按授权保持未运行 | `artifacts/implementation/i04/builds/` 与隔离 test output |
| `Bing.Tests` 既有 AOP DI adapter 引用 | `Bing.Tests`：`ServiceCollectionTest.Test_Add`、`ValidTest.Test_Add_Invalid`/`Test_Add_Valid`、`IocTest.TestCreate`/`TestCollection`/`TestCreateList`/`TestCreateList_2` | `artifacts/implementation/i03/bing-tests-refs/net8.0/i03_bing_tests_refs_net8.trx`、`net6.0/i03_bing_tests_refs_net6.trx`；各 7/7 通过 |
| SQL 与 Permissions XML 文档契约 | 无运行时测试；`Bing.Data.Sql`/`Bing.Permissions` 受影响 TFM Release build 和生成 XML 解析 | `artifacts/implementation/i06/build/`、`artifacts/implementation/i06/xml/` |

## 计划偏差

未扩展到冻结范围以外的功能。主方案构建暴露两项与计划直接相关的测试依赖/构建图问题：为 `Bing.Tests` 补齐被源码实际使用的仓库 AOP adapter 引用（关联 I03），并将 AOP LegacyMarkerConsumer 纳入 solution 以取得 Release 配置（关联 I04）。其根因、失败轮次和修复均记录在前述影响分析及 `artifacts/implementation/i05/solution-build/`；最终主方案 checkpoint 通过。

## 基线问题

初审基线的主方案 Release build 失败；I04/I05 实施期间定位并修复本地测试依赖与 LegacyConsumer Release 输出问题。最终 I04/I05 主方案 restore/build 均成功，build 为 124 warnings、0 errors。两次中间失败和最终通过日志均保留；该阶段构建不等于最终 fresh `V-FINAL`。

## 已知问题

最终 fresh `V-FINAL` 与独立 `review.md` 尚待父会话调度；MySQL 除无数据库装配用例外、Redis、外部 Provider、broker、真实服务和性能预算均按计划未运行。主方案 124 个 warnings 原样保留，供独立 Reviewer 评估。

## 风险与回归关注点

保持 AOP 旧程序集类型转发、异常断言单次执行、外部集成门控、Release/TFM 传播、测试工程覆盖和 XML 注释纯文档性质。

## Reviewer 注意事项

本报告只记录实施与阶段验证。独立 Reviewer 应对冻结计划、真实 diff、隔离复制证据、实际测试结果及符号到测试方法映射另行验收。

## Git 状态

- 工作区有本次未提交改动，未暂存；初始已存在的整个 `ai_docs/tasks/20261021-merged-code-audit/` 未跟踪目录被保留，并新增 `AssertHelperTest.cs` 未跟踪文件。
- 不执行 `git add`、`commit`、`push`、`tag`、`reset`、`clean` 或 PR 操作；未自动创建 PR。

## Review 修复记录

### Round 1

- Review 状态：`NEEDS_FIX`；Fix Scope：`recommended`；处理 FIX-001–FIX-006（3 个 MUST_FIX、3 个 SHOULD_FIX）。
- 状态：Round 1 实施与阶段验证完成；由 `task-finish.mjs` 收口。`review.md` 保持 Reviewer 原始 NEEDS_FIX 与六条 OPEN_ACTIONABLE，待独立 Sol 复审后更新分类。
- Review 文件：`ai_docs/tasks/20261021-merged-code-audit/review.md`（只读）；原始独立报告与证据未覆盖。
- Round 1 证据目录：`ai_docs/tasks/20261021-merged-code-audit/artifacts/fix-round-1/`；起始状态见 `baseline.md`。
- 候选：HEAD `a99a16c4b55c407cc81d771822667044e2e9e9b1` + 原 I01–I07 工作区差异 + 本轮 FIX-001–FIX-006；`candidate-v2` 是本轮最终隔离候选，构建前清单、文件数和排除目录见 `artifacts/fix-round-1/candidate.json`。
- 候选准备：首份新副本在复制仓库根级历史 `artifacts/` 长路径时失败，未删除/覆盖原文件；不完整目录保留但不参与验证。`candidate-v2` 排除根级 `artifacts`、任务 `ai_docs`、`.git` 与旧输出后从当前工作树重新复制，4,025 个文件，新增 `AssertHelperTest.cs` 及本轮全部源文件均存在，构建前没有 `bin/obj/output`。身份见 `artifacts/fix-round-1/candidate.json`。
- No-Progress / Experiment Limit：当前为首轮 Fix，已由 Reviewer 在新候选发现具体根因；每个固定工作负载仅进行本轮候选的一次确认，若构建/测试给出新根因才允许调整，不只延长 timeout 重复实验。最多自动 Review→Fix→Review 3 轮。

#### 测试前 Change Impact Analysis

| FIX | ChangedFiles / ChangedProjects | 契约与运行路径 | Providers / TFM / Build | Benchmark / Docs | 依赖 / 风险 |
|---|---|---|---|---|---|
| FIX-001 | AOP 测试 csproj 与 `IgnoreAspectPluginAssemblyResourceTest.cs`；AOP.Tests、PluginFixtures | 无生产 API；改变隔离 ALC 加载到当前测试配置的真实 fixture 路径，原调用/拦截/卸载断言保留 | 无 Provider；net6/net8 不变；按当前 Configuration/TFM 复制 fixture | 不改 benchmark 或文档 | Release test output 与插件引用；HIGH（加载上下文卸载） |
| FIX-002 | 两个 Admin 测试 csproj、`Bing.All.sln`；两个 Admin.Tests 与两个 InventoryProbe | 无生产 API；修复本地清理/诊断测试的 probe 产物消费，不运行真实数据库 probe 入口 | Admin 测试支撑图；net6 不变；显式传播 Release/net6 并校验非空输出 | 不改 benchmark 或文档 | solution ProjectReference/GetTargetPath 图；HIGH（构建图/子进程测试） |
| FIX-003 | 两个 `AdminHangfireRegistrationTest.cs`；两 Admin.Tests | 无生产 API；只补内存测试配置，使真实 Task/Consumer 角色注册断言可执行 | 无 Provider 行为变化；net6 不变；不启 Redis/CAP/数据库 | 不改 benchmark 或文档 | Admin Startup 本地注册用例；MEDIUM |
| FIX-004 | `AssertHelper.cs`、`AssertHelperTest.cs`；Bing.Tests.Samples | 测试助手直接与 AspectInvocationException.InnerException 统一精确类型；单次异常捕获、返回真实异常、keyword 语义不变 | 无 Provider/TFM/build graph 变化；net6/net8 不变 | 不改 benchmark 或文档 | 该助手的 16 个同步/异步回归；LOW |
| FIX-005 | `docs/operations/文档与代码对照清单.md`；文档 | 无运行路径或契约变化；只更正两条 FreeSQL 能力行及 Admin 参考宿主行 | 无 Provider、TFM 或 build 变化 | Docs-only；保留非原生异步、外部未验收和 2 个确定缺口/2 个候选分类 | 同页及模块页语义一致；LOW |
| FIX-006 | `Bing.Samples.WebApi.Tests.csproj`；WebApi.Tests | 无生产 API；让默认测试入口为两个已支持 TFM 求值并发现 smoke，测试仍断言宿主关闭后释放端口 | WebApi 测试图；net6/net8 与包版本不变；solution 成员不变 | 不改 benchmark 或文档 | 主方案 build/普通 dotnet test；HIGH（多目标 restore/build/discovery） |

#### FIX 处理记录

- FIX-001 COMPLETED：测试从 `AppContext.BaseDirectory/PluginFixtures/Bing.Core.PluginFixtures.dll` 加载当前配置和 TFM 的 fixture；AOP 全套 Release 测试 net8.0、net6.0 各 102/102。`IgnoreAspectPluginAssemblyResourceTest.ResolveIgnoredPluginService_ShouldInvokeAndReleaseAssemblyLoadContext` 在两 TFM 均通过，原插件调用、拦截与可收集加载上下文卸载断言保留。
- FIX-002 COMPLETED：两个 InventoryProbe 串行加入 solution 并具 Debug/Release ActiveCfg、Build.0；ProjectReference 传播 Configuration/TFM，空复制集合会明确报错。空输出 Release build 将两 DLL 生成到 `bin/Release/net6.0` 并复制到各 Admin 测试输出；EFCore 与 FreeSQL 的 `AdminInventoryProbeCleanupTest` 各 16/16 通过，没有调用真实数据库 probe 入口。
- FIX-003 COMPLETED：两套 Hangfire 最小内存配置均补独立 `Caching:Namespace`；EFCore Consumer、FreeSQL Consumer 与 Task 三条角色注册用例通过。未修改生产命名空间校验，未启 Redis、CAP 或数据库。
- FIX-004 COMPLETED：AssertHelper 直接异常和包装异常统一为精确 `Assert.IsType<TException>`；同步/异步共 16 个回归用例在每个 TFM 均通过，覆盖直接/包装精确异常、派生及无关类型拒绝、keyword 正负、无异常以及 action 单次调用和实际异常返回。
- FIX-005 COMPLETED：仅修改现行对照清单的三格能力说明；保留 FreeSQL 非原生异步、Admin 特定宿主外部未验收、2 项确定缺口与 2 项候选的分类。文档同步检查通过；完整工作区 `check-docs.py` 13/13 检查通过。
- FIX-006 COMPLETED：WebApi 测试 csproj 统一使用 `TargetFrameworks=net8.0;net6.0`，保留现有 TFM 与包版本。主方案空输出 Release build 产出两个 TFM DLL 和 test adapter；默认不带 `-f` 的 `dotnet test --no-build --no-restore` 每 TFM 实际发现并通过 1 条 smoke，宿主关闭后端口释放断言通过。

#### Round 1 验证

- L0：结构化 solution 配置检查和 WebApi MSBuild Release 求值通过；当前隔离候选主方案 restore exit 0，Release build exit 0、232 warnings / 0 errors。命令、stdout、stderr、耗时、退出码与候选见 `artifacts/fix-round-1/restore-main.*`、`build-main-release.*`、`static-probe-config-structured.*`、`static-webapi-eval.*`。该 solution 当前 156 个直接成员；本轮工程图 checkpoint 不替代最终 V-FINAL。
- 受影响项目完整测试：8 个 project/TFM runs 共 528 passed / 0 failed / 71 external-gated not executed（599 total）。AOP：net8.0 102/102、net6.0 102/102；AssertHelper：两个 TFM 各 16/16；Admin EFCore：140 passed / 0 failed / 36 gate skips；Admin FreeSQL：150 passed / 0 failed / 35 gate skips；WebApi：两个 TFM 各 1/1。逐项 TRX 与汇总见 `artifacts/fix-round-1/trx/`、`trx-summary-verified.json`；没有运行真实数据库 InventoryProbe 入口或启动外部服务。
- 测试符号映射：AOP fixture/lifecycle -> `Bing.Aop.AspectCore.Tests.IgnoreAspectPluginAssemblyResourceTest.ResolveIgnoredPluginService_ShouldInvokeAndReleaseAssemblyLoadContext`，两个 TFM 各通过，证据 `trx/aop/`；AssertHelper 同步 -> `Bing.Tests.Samples.AssertHelperTest.Throws_DirectException_CapturesOnceAndChecksKeyword`、`Throws_AspectInvocationException_CapturesInnerExceptionOnce`、`Throws_WrongDirectException_FailsAfterSingleInvocation`、`Throws_WrongWrappedException_FailsAfterSingleInvocation`、`Throws_DerivedDirectException_IsRejectedAfterSingleInvocation`、`Throws_DerivedWrappedException_IsRejectedAfterSingleInvocation`、`Throws_NoException_FailsAfterSingleInvocation`、`Throws_KeywordDoesNotMatch_FailsAfterSingleInvocation`；异步对应 `ThrowsAsync_...` 八个方法，所有双 TFM TRX 在 `trx/assert-helper/`。
- 测试符号映射续：Admin probe cleanup/脱敏 -> `Bing.Admin.EFCore.Tests.AdminInventoryProbeCleanupTest` 与 `Bing.Admin.FreeSQL.Tests.AdminInventoryProbeCleanupTest`，各 16 个实际用例通过，完整方法结果在 `trx-summary-verified.json` / `trx/admin-efcore/` / `trx/admin-freesql/`；Hangfire 角色 -> `Bing.Admin.EFCore.Tests.AdminHangfireRegistrationTest.Startup_WhenConsumerRole_ShouldNotRegisterHangfireModule`、`Bing.Admin.FreeSQL.Tests.AdminHangfireRegistrationTest.Startup_WhenConsumerRole_ShouldNotRegisterHangfireModule`、`Startup_WhenTaskRole_ShouldRegisterHangfireModule`；WebApi 默认 TFM 入口 -> `Bing.Samples.WebApi.Tests.SampleWebApiSmokeTest.LazyEndpoint_ShouldResolveBusinessValueAndReleasePortOnStop`，双 TFM 各通过，证据 `trx/webapi/`。FIX-005 是纯文档修正，无运行时测试；映射到 `docs-sync-check.*`、`docs-lint-worktree.*`。
- 文档检查边界：候选副本按规则排除 `ai_docs`，故其中 `check-docs.py` 对历史任务文档链接的失败保留在 `docs-lint-check.*`；完整工作区重新运行同一命令 exit 0，13 项通过、1,071 links reachable，证据 `docs-lint-worktree.*`。`sync-docs-index.py --check` exit 0，证据 `docs-sync-check.*`。
- Round 1 与实现阶段合计状态：I01–I07 实现仍为 7/7 完成；本轮 FIX-001–FIX-006 实施与阶段验证为 6/6 完成（3 MUST_FIX、3 SHOULD_FIX）。review.md 原有六条 OPEN_ACTIONABLE 是 Reviewer 的未复核状态，本报告不自行关闭或改写；待独立 Sol 复审确认。原始全矩阵 11,751 passed / 37 failed / 285 skipped 与本轮受影响子集计数分开记录，不能将局部结果表述为 V-FINAL。
- 本轮未解决/未执行：V-FINAL fresh 全验证与独立 review 未执行；完整 matrix 的既有 37 failures 保留原报告归因与状态，由独立 Sol 复查；外部数据库、Redis、CAP 等服务均未启用。没有新增已定位的实现阻塞。
- 本轮源码/工程图修改：`Bing.All.sln`；`framework/tests/Bing.Aop.AspectCore.Tests/Bing.Aop.AspectCore.Tests.csproj`、`IgnoreAspectPluginAssemblyResourceTest.cs`；`modules/admin/tests/Bing.Admin.EFCore.Tests/Bing.Admin.EFCore.Tests.csproj`、`AdminHangfireRegistrationTest.cs`；`modules/admin/tests/Bing.Admin.FreeSQL.Tests/Bing.Admin.FreeSQL.Tests.csproj`、`AdminHangfireRegistrationTest.cs`；`tests/Bing.Tests.Samples/Bing/Tests/XUnitHelpers/AssertHelper.cs`、`AssertHelperTest.cs`；`samples/Bing.Samples.WebApi.Tests/Bing.Samples.WebApi.Tests.csproj`；`docs/operations/文档与代码对照清单.md`。证据目录为 `artifacts/fix-round-1/`。未修改 `review.md` 或 `final-report.md`。
- AssertHelper 异步实际方法名：`ThrowsAsync_DirectException_CapturesOnceAndChecksKeyword`、`ThrowsAsync_AspectInvocationException_CapturesInnerExceptionOnce`、`ThrowsAsync_WrongDirectException_FailsAfterSingleInvocation`、`ThrowsAsync_WrongWrappedException_FailsAfterSingleInvocation`、`ThrowsAsync_DerivedDirectException_IsRejectedAfterSingleInvocation`、`ThrowsAsync_DerivedWrappedException_IsRejectedAfterSingleInvocation`、`ThrowsAsync_NoException_FailsAfterSingleInvocation`、`ThrowsAsync_KeywordDoesNotMatch_FailsAfterSingleInvocation`；均属于 `Bing.Tests.Samples.AssertHelperTest`，具体 TFM 结果见 `trx/assert-helper/`。
- 最终 `git diff --check`：exit 0；终态元数据后的第一次证据为 `artifacts/fix-round-1/final-diff-check-final.json`，最终收口检查为 `final-diff-check-closure.json` 及其 stdout/stderr。执行记录只改时间和证据路径，没有重跑代码测试。V-FINAL 留给后续独立 Sol 验收。
