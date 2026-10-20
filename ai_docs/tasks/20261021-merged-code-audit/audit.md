# 多分支合并后整体审查

日期：2026-10-21（Asia/Shanghai）。Task：`20261021-merged-code-audit`。审查对象：`a99a16c4b55c407cc81d771822667044e2e9e9b1`，分支 `dev_v6.0`。只审查与规划，未实施修复。

## 结论

**当前不能完整编译，也不能认定功能和性能已完整验收。** 主解决方案 restore 成功；Release build 失败：**12 个错误、148 个警告**。85 个生产项目全部在解决方案中且启用了 Release Build.0，69 个有本次成功程序集输出，16 个因 AOP 冲突及其依赖链未完成。不存在漏掉 solution 外生产项目的问题；另补建了 solution 外 WinForms，成功。旧 CodeSmith 解决方案因缺 .NET Framework 4.5 引用程序集失败。

本次 **32 个测试项目、62 个项目/TFM 运行：10,191 通过、0 失败、162 跳过**。这是实际 TRX 统计，不包括未构建/未运行项目，也不把双 TFM 当成独立功能数量。额外无数据库 SQL 作用域诊断复现 **2 个失败场景**，不混入 xUnit 计数。测试全绿的已运行部分因此不能推出整体无缺陷。

最重要的问题：合并重新带回已经解耦的 `ValidAttribute`，与类型转发冲突；SQL Singleton 工厂持有根容器，导致真实请求作用域的过滤状态无效。后者既可直接抛异常，也可在关闭作用域校验时静默忽略软删除过滤开关。

功能盘点确认 **2 项真实未实现能力**：微信退款、领域全局验证规则；另有 **2 项候选待产品确认**：OAuth 统一工厂、按租户自动选择文化。确定数量是当前有直接证据的下界，不是对所有业务场景的完整需求清单。领域事件排空已经有实现，不再计为缺失；FreeSQL 真异步、动态 API 返回形状等列为明确限制。

冻结 **7 个可直接实施项**：P0 × 1、P1 × 4、P2 × 2。SQL 作用域问题另列高优先级兼容决策：现有测试明确要求工厂 Singleton，不能在本轮悄悄承诺改为 Scoped 或把 Scoped 状态提升为 Singleton。详见 [plan.md](plan.md) 和 [backlog.md](backlog.md)。

```text
IMPLEMENTATION_STATUS: NEEDS_FIX (OPEN_ACTIONABLE=7)
TEST_STATUS: PARTIAL (executed xUnit PASS; scope diagnostic FAIL; blocked suites not run)
PERFORMANCE_EVIDENCE_STATUS: PARTIAL (current Dry smoke only)
RESOURCE_EVIDENCE_STATUS: NOT_VERIFIED (no heavy capacity matrix)
EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL
RELEASE_STATUS: BLOCKED
GOAL_STATUS: AUDIT_AND_PLAN_COMPLETED; IMPLEMENTATION_NOT_STARTED
```

没有创建 Goal 或启动自动 Fix。上述完成只指本次审查交付，不代表修复或发布完成。

## 规则、身份与范围

- 已读取根 `AGENTS.md`、作用域 `.github/copilot-instructions.md`（framework）、`C:/Users/jianx/.codex/GOAL_RULES.md`、`create-plan` 与 `stop-that-shit` 技能。仓库内未发现其它 AGENTS.md。
- 最初只读 `git status --short` 为空；原始 `initial-status.log`、暂存/未暂存 diff 均为空。最终工作区仅新增本任务目录。现有忽略的输出目录存在，故用本次构建日志识别产物，禁止把历史 DLL 直接作为当前测试对象。
- 本会话没有模型切换接口，未声称已切换 GPT-6-Astra；没有调用其它代理或会话。后续模型分派由父会话负责。
- 所有自建脚本文本读写显式 UTF-8；PowerShell 控制台按 UTF-8 设置。未运行 git add/commit/push/tag/reset/clean/PR，未更改包版本、API baseline 或阈值。
- `create-plan` 的结束通知脚本实际导入飞书通知组件。因本任务没有对外发消息授权，不执行该通知；不影响计划文件交付。
- 全量文件/项目盘点与入口抽查相结合：审阅公共 API、注册/调用链、异常清理、异步状态、Provider、缓存与合并交叉面。没有逐行证明全部业务源码正确，也没有把 README 或历史报告当运行证据。

### 工程与环境

| 项目 | 本次事实 |
|---|---|
| 主解决方案 | `Bing.All.sln`，132 个直接成员；restore 依赖图涉及 145 个项目 |
| 全仓库非历史 csproj | 162：生产 85、test-or-probe 71、sample 类 4（含 1 个测试项目）、工具 1、旧模板 1；两份 Backup csproj 保留但不构建 |
| 生产项目 | framework 70 + components 3 + admin 12；均在主 SLN、均启用 Release Build.0 |
| TFM | netstandard2.0、netstandard2.1、net6.0、net8.0、net6.0-windows；旧模板另为 .NET Framework v4.5 |
| SDK | global.json 锁定 8.0.424；MSBuild 17.11.48；本机另有 SDK 3.1/6/10，但没有替换锁定版本 |
| 运行环境 | Windows 10 x64；本次 .NET 8 使用 8.0.30，.NET 6 使用安装的 6.0.36；完整运行时清单见 sdk-info.log |
| 构建限制 | .NET 6 的 NETSDK1138、部分 RID 的 NETSDK1206；本次不调整版本/TFM。CodeSmith 缺 net45 targeting pack，不能由 dotnet 8 本次环境完整构建 |

精确到每个项目的 TFM、solution 配置与输出见 [project-matrix.md](artifacts/project-matrix.md)，原始 MSBuild 求值见 `eval-*.log` 和 [evaluated-projects.json](artifacts/evaluated-projects.json)。本任务 artifacts 下新建的诊断项目不属于初始 162 个工程。

## 本次验证

运行前先落盘 [影响分析](artifacts/impact-before-validation.md)。因这是整体合并候选检查点，执行一次主方案 restore/build 和有界本地套件；随后只补缺失范围及有具体假设的诊断。

| 验证 | 结果与准确含义 | 证据 |
|---|---|---|
| `dotnet restore Bing.All.sln -m:2 -nr:false` | exit 0，14.109 秒，窗口 900 秒 | solution-restore.log/json |
| `dotnet build Bing.All.sln -c Release --no-restore -m:2 -nr:false` | exit 1，30.860 秒，窗口 900 秒；12 errors / 148 warnings | solution-build.log/json、build-errors.txt |
| 9 个 solution 外项目补建 | 7 个本地测试项目 + WinForms 通过；WebApi.Tests 被 F001 阻断 | extra-restore/build-*.log/json |
| Core 单项目全 TFM 诊断 | exit 1，外层 GetTargetPath 失败；没有修 csproj | extra-build-Bing.Core.Tests.log |
| Core 明确 `-f net8.0` / `-f net6.0` | 两次构建及测试都通过；各 639 tests | core-net*-build、test-Bing.Core.Tests-net* |
| 32 项目本地测试 | 10,191 passed / 0 failed / 162 skipped，62 份 TRX | [汇总](artifacts/test-summary.md)、[逐用例跳过信息](artifacts/test-summary.json)、trx/ |
| SQLite 真实集成 | 每 TFM 151 passed / 0 failed / 1 skipped；合计 302/0/2 | test-Bing.Dapper.Sqlite.Tests.Integration.log、对应 TRX |
| SQL 请求作用域探针 | exit 1；2 个契约失败；未打开数据库连接 | sql-scope-probe.log、[程序](artifacts/scope-probe/Program.cs) |
| SQL CI / SQLite E2E Dry smoke | exit 0，各执行 1 / 3 个 case；详见性能节 | sql-ci-smoke、sql-e2e-smoke |
| 旧模板 build | exit 1，MSB3644 缺 net45 引用程序集 | legacy-template-build.log |
| 文档索引与一致性 | 两命令 exit 0，一致性脚本 13 检查通过 | docs-index-check、docs-consistency |

每条运行的命令、UTC 时间、实际耗时、退出码、预设窗口、子进程环境覆盖见 [command-manifest.json](artifacts/command-manifest.json)。stdout/stderr 保留在同名前缀 `.log`。本次没有超时，没有用增加窗口反复尝试相同实验。

### 测试边界

- `RUN_INTEGRATION_TESTS=false`；采证子进程移除继承的 `RUN_*`、`BING_TEST_*`、连接串与数据库 reset 授权变量。未启动数据库/Redis 容器，未连接生产库，未调用真实邮件、支付服务。
- 162 skips：EasyCaching 140、MailKit 10、Auditing 6、MVC 2、Payments 2、SQLite 2。SQLite 跳过的是要求受控 evidence 目录/session 变量的记录用例，不是 151 项真实执行测试被跳过。详细理由保留于 TRX。
- AOP、Events/EventBus、EFCore.Tests、FreeSQL.Tests、admin 两个套件、WebApi smoke、旧聚合/样例测试的完整范围受构建图阻断，未用旧二进制执行。外部 Provider 和 Redis 集成项目未执行；这些项目不计为“跳过 162”。
- Core 全项目目标仍失败；明确 TFM 的通过不能替代解决方案通过。net6 Core 命令中一个无效的 `RunSettingsFilePathPath` 属性拼写保留在原始 manifest；该项目无 RunSettingsFilePath 配置，不影响外部门控或测试结果，没有为此重复测试。
- 第一次 Git 父提交诊断因 shell 把 `HEAD^1` 传成 `HEAD1` 失败，保留 exit 128 日志；第二次用已记录的明确父提交 SHA 成功，后者才是合并来源证据。
- 文档 lint 验证链接/索引/符号，不验证“功能是否缺失”的语义；它通过不否定 F007。

## Findings

严重级别：P0 阻断主要构建；P1 关键运行/验证缺陷；P2 功能或文档问题；P3 已知边界/后续治理。每项恰有一个 GOAL_RULES 状态。

### F001 · P0 · OPEN_ACTIONABLE — 合并恢复了已迁出的 ValidAttribute

- 位置：`framework/src/Bing.Aop.AspectCore/Bing/Aspects/ValidAttribute.cs:13`、`Properties/TypeForwardedTypes.cs:9`；对照 `Bing.Validation.Abstractions/Bing/Validation/ValidAttribute.cs:9`。
- 根因：中立特性迁移后已有转发器及适配器；合并又把同命名空间的 ParameterInterceptorBase 子类带回 AOP 程序集，编译器报 CS0729。`merge-aop-cross-impact-confirmed.log` 证明相对第一父提交 a6a84b63，此文件作为新文件恢复。
- 影响：AOP 自身及 Events、应用 AspectCore、12 个 admin 项目等依赖图阻断；不是 SDK 缺失。
- 重现：本次 solution-build，原始错误见 build-errors.txt。
- 验收：保留现有中立特性身份、类型转发、参数验证适配器；移除重复定义；反射/旧消费者/实际参数验证测试通过，完整 Release 构建不再出现 CS0729/CS0436 对应冲突。不修改 API baseline。

### F002 · P1 · OPEN_ACTIONABLE — 旧样例断言助手缺依赖且可能重复执行操作

- 位置：`tests/Bing.Tests.Samples/Bing/Tests/XUnitHelpers/AssertHelper.cs:3` 与该 csproj。
- 根因：助手使用 AspectCore.DynamicProxy / AspectInvocationException，工程只导入 common.tests.props，未声明相应编译依赖；不能再依靠旧传递依赖。另 GetException/GetExceptionAsync 先执行 action 断言包装异常，catch 后再次执行 action 断言直接异常；直接异常场景会执行两次，可能重复副作用。
- 证据：net6/net8 各 CS0234；共享测试包配置已经拆分。重复执行由同文件 34/39、64/69 行控制流直接确认；因项目编译阻断，本次未声称运行过该回归测试。
- 验收：复用仓库已经使用的 AspectCore 3.0.0 依赖，不升级版本；单次捕获异常后区分直接/包装异常；两个 TFM 编译及同步/异步、直接/包装、错误类型/无异常断言测试通过，action 只执行一次。

### F003 · P1 · OPEN_ACTIONABLE — MySQL 夹具的 AOP 依赖不完整

- 位置：`framework/tests/Bing.Dapper.MySql.Tests.Integration/Infrastructure/MySqlIntegrationDatabaseFixture.cs:1,101` 和对应 csproj。
- 根因：保留旧 `AspectCore.Extensions.DependencyInjection` using；方法同时调用当前 `Bing.DependencyInjection.EnableAop`，其实现位于 Bing.Aop.AspectCore，但项目没有直接引用该适配器。
- 证据：两个 TFM CS0234；实际方法实现定位 `framework/src/Bing.Aop.AspectCore/Bing/DependencyInjection/AspectCoreExtensions.cs:36`。
- 验收：按实际调用补齐仓库内依赖、删除失效 using；不删除 AOP 装配来绕过语义；两个 TFM 构建通过，`ServiceRegistration_ShouldResolveSqlCoreServicesWithoutDatabaseConnection` 可在门控关闭时通过。真实 MySQL 用例继续受门控保护。

### F004 · P1 · OPEN_ACTIONABLE — 多 TFM 后置复制与 solution 配置传播不一致

- 位置：`framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj:40` 起；`Bing.Caching.CSRedis.Tests.Integration.csproj:25` 起。
- 根因一：AfterTargets=Build 的复制目标没有限定 TargetFramework 非空，外层多 TFM Build 对多目标 probe 调 GetTargetPath，报 MSB4057。Core 独立构建复现同类问题。
- 根因二：未列入 solution 的 Core fixture 依赖在 solution 构建日志中产到 output/debug，而复制目标按 Configuration=Release 查 output/release，产生 6 个 MSB3030。
- 同模式关联位置：`Bing.Aop.AspectCore.Tests.csproj` 的 CycleProbe、`Bing.FreeSQL.Tests.csproj` 的 OrderingProbe；当前受 F001 阻断，没有把静态同模式当本次新增运行错误。
- 验收：复制仅在相应内层 TFM 执行；构建和 GetTargetPath 使用同一配置；空输出目录的等价隔离工作区里 solution Release 和单项目全 TFM 均成功，产物齐全且可运行。不能用手工复制 DLL、吞掉缺文件错误或依赖现有 Debug 产物解决。

### F005 · P1 · OPEN_ACTIONABLE — 10 个有效测试项目未纳入主验证图

- 位置：`Bing.All.sln`；完整遗漏清单见 plan I05 和 evaluated-projects.json。
- 根因：新增模块测试未同步 solution，默认 `dotnet test Bing.All.sln` 看不到 Validation/Uow/缓存/Security 等套件。AppVeyor common lane 依赖默认主方案入口。
- 证据：本次 MSBuild 求值 IsTestProject=true 与主 solution 成员比较；7 个可独立运行的遗漏项目本次已补测，另 3 个被 AOP 链阻断。
- 验收：10 项均进入主方案或主验证入口的显式、不可遗漏清单；不纳入 Backup/历史实验工程；可审计地覆盖全部声明 TFM，外部门控不改变。

### F006 · P2 · OPEN_ACTIONABLE — 生成 API XML 的注释结构损坏

- 位置：SQL `Extensions.IFrom.cs:9`、`Extensions.IJoin.cs:9`、`MutationClauseExtensions.cs:15`、`Contexts/DatabaseContextSnapshot.cs:7`、`Extensions.ICte.cs:9`、`Extensions.IUnion.cs:9`、`SelectClauseExtensions.cs:8`、`Queries/SqlConditionGroup.cs:45`；另 Permissions 三个模型的 typeparam 重复。
- 根因：summary 缺闭合、泛型文档重复；分别 CS1570/CS1710。编译未拒绝，但公开文档 XML 不完整。
- 验收：仅修注释结构，保留 API/实现；这些明确告警归零，生成 XML 可解析。不把其它 148 个告警全部扩成此次整改范围。

### F007 · P2 · OPEN_ACTIONABLE — 当前功能文档仍沿用合并前缺口

- 位置：`docs/operations/文档与代码对照清单.md` 的“待实现/待确认清单”、`docs/modules/domain.md` / `events.md` / `data-access.md`、`docs/operations/测试指南.md`、README 当前数量描述。
- 根因：事件自动排空、FreeSQL 同步兼容包装等后续代码已落地，文档仍把它们称为未实现；测试指南还称 Payments 不在 SLN，而当前已在。
- 证据：EFCore `Core/UnitOfWorkBase.cs:784`、FreeSQL `Bing/Uow/UnitOfWorkBase.cs:145`、StoreBase 事件源登记、`DomainEventDispatchSession`；本次 Ddd.Domain 单元测试通过。FreeSqlAsyncQueryableProvider 已有 Execute 包装，并非返回空值的 stub。
- 验收：当前文档准确区分实现、已跑测试、构建阻断、外部验收；73 个框架/组件生产工程与 12 个 admin 的口径一致；历史有日期的任务报告保持历史原文，不改成当前通过记录。

### F008 · P1 · BLOCKED_APPROVAL — SQL Singleton 工厂捕获根容器，丢失调用方 Scoped 过滤状态

- 位置：`framework/src/Bing.Dapper.Core/Bing/Data/Sql/DapperServiceCollectionExtensions.cs:106,133,136,139,142`；`SqlFactoryBase.cs:157`；`SqlQueryBase.cs:621`。
- 根因：AddSqlCore 注册 IDataFilter 为 Scoped，Query/Executor/Multiple/Transaction factory 为 Singleton。工厂保存根 IServiceProvider，并把它传给运行时 Query；创建 Builder 时从根解析 IDataFilter。默认 DataFilter 的 AsyncLocal 字段属于实例，不能跨不同 Scoped 实例自动共享。
- 本次重现：scope-probe 从真实 CreateScope 解析 ISqlQueryFactory。ValidateScopes=true：`Cannot resolve scoped service 'Bing.Data.Filters.IDataFilter' from root provider`；false：Disable<ISoftDelete> 前后完整 SQL 完全相同，仍包含 IsDeleted 条件。0 次数据库打开。
- 测试盲区：SQLite 现有过滤测试从 `_fixture.ServiceProvider` 根容器取 filter 和 factory，二者碰巧共享根实例；已有 302 个 SQLite 集成通过不覆盖真实请求作用域。
- 为什么需决策：`AddSqlCore_WhenCalledRepeatedly_ShouldKeepSingletonRegistrationsIdempotent` 明确描述现有 Singleton 契约。改 Scoped 会影响把工厂注入 Singleton 的消费者；把 IDataFilter 改 Singleton 会改变同执行流多 scope 之间的状态隔离。不能用关闭 ValidateScopes 掩盖。
- 建议：维护者确认 scope-bound factory 兼容策略后再实现；Query/Executor/Multiple/Transaction 工厂一起评估，元数据/Provider 等无状态服务保持 Singleton。背景任务显式创建 scope。详细文件、测试和 SQL 强门槛见 plan 的 R-SQL。
- 验收：两个探针场景均修复；子 scope 的禁用/启用、跨 scope 隔离、异步/并发、重复渲染、Clone、Query/Executor/Transaction/Multiple 入口均有直接测试；完整 SQL 与 SQLite 实际结果一致；已明确并记录消费者迁移策略。当前不把未批准策略冻结为实现项。

### F009 · P2 · BLOCKED_APPROVAL — 微信退款真实未实现

- 位置：`components/src/Bing.Biz.Payments/Wechatpay/Services/WechatpayRefundService.cs:30`。
- 根因/证据：公开 RefundAsync 无条件抛 NotImplementedException；已有参数 builder 和 URL 配置不构成退款实现，README 也明确标注。
- 验收：先确定协议版本、证书/凭据方式、幂等、错误与回调语义，再完成请求/响应测试、失败与重试边界、隔离沙箱验收。不得调用真实资金退款或未经授权升级支付包。计为确定缺口 G01。

### F010 · P2 · BLOCKED_APPROVAL — 领域全局验证规则入口真实未实现

- 位置：`framework/src/Bing.Ddd.Domain/Bing/Domain/Entities/DomainObjectBase.cs:131`。
- 根因/证据：UseValidationRules 无条件抛 NotImplementedException；UseStrategy/UseStrategyList 的实际实现不等同全局规则注册。
- 验收：先决定规则源、生命周期、泛型继承/覆盖顺序与缓存失效；完成配置与执行链、空规则/重复/并发/失败直接测试，保留明确失败而非静默忽略规则。计为确定缺口 G02。

### F011 · P1 · BLOCKED_EXTERNAL — 真实外部环境与发布链未验收

- 范围：数据库 Provider 实库、Redis Cluster/分片/Hybrid/蓝绿双版本、CAP/MySQL admin 链及受保护 CI。
- 根因：本次没有获准的隔离服务与外部 CI 证据；部分套件还先受 F001/F003/F004 阻断。
- 证据：环境门控关闭、本次命令清单；历史 STATUS-AUDIT 的外部门禁不是此次运行。
- 验收：本地阻断解决后，仅在明确非生产环境执行既有 `Invoke-ProviderIntegrationTests.ps1`、`Invoke-AdminMySqlCapIntegrationTests.ps1` 及对应缓存/蓝绿命令，保存版本、候选、配置、TRX/证据 manifest。不得用反复本地测试替代。

### F012 · P2 · BLOCKED_EXTERNAL — CodeSmith 旧模板工具链缺失

- 位置：`templates/CodeSmith/CodeGenerator/CodeGenerator.csproj:12`。
- 根因：目标 .NET Framework v4.5，本机 dotnet build 缺引用程序集，MSB3644。CodeSmith 专有 DLL/VS 完整 MSBuild 的后续兼容性也尚未验证。
- 验收：维护者提供匹配工具链后在隔离环境构建旧 solution；或另行确认停止支持。不能擅自迁移 TFM/安装工具并称当前完整通过。

### F013 · P1 · BLOCKED_EXTERNAL — 当前正式性能预算缺证据

- 范围：SQL FormalHost、缓存真实网络/并发/分配预算；不涉及自动降低阈值。
- 证据：本次仅有 Dry smoke；历史 task004 有预算 FAIL 的原始 JSON，本机能读取，但不是当前 HEAD 的 before/after。
- 验收：相关源码与 harness 身份匹配后复用合格历史基线，在批准的隔离环境跑最小受影响预算场景；保留既有阈值。详见性能节。若需要改变阈值，再单独 BLOCKED_APPROVAL。

## 真实实现与合并交叉审查

| 面 | 已核对行为与本次证据 | 结论边界 |
|---|---|---|
| Core / 模块 / 插件 | ModuleLoader 拓扑加载和构造失败回收；ModuleManager 锁、重入、初始化/关闭任务；BuildBingServiceProvider 错误清理；Core 各 TFM 639 tests | 生命周期测试通过；完整构建目标 F004 仍失败 |
| API / AOP / DI | 中立特性、转发和 ParameterInterceptorSelector；动态 API 服务端描述、原生代理、WebApiClient 请求上下文；MVC 350、两类客户端 38/26 tests | AOP 有 F001；SQL DI 有 F008；HTTP mock/TestServer 不代表远程部署认证链全部通过 |
| SQL Provider / 缓存 / API | 映射缓存键包含类型/DbKey/Provider/profile/最终物理名；Lazy 失败移除、有限容量准入/LRU；上下文快照、执行租约、流式 reader/finally 清理；完整 SQL 与 Provider 职责测试 | SQL 2,634、CustomProvider 42、Analyzer 32 通过；各 Provider 单元通过；实库只跑 SQLite |
| Query/Executor/异常 | 工厂 CreateInstance、连接所有权、事务 rollback/cleanup 异常聚合、流式提前终止、取消能力检查 | 现有测试通过不能覆盖 scope 捕获，F008 是已复现遗漏 |
| 缓存 / 并发 | CacheKeyFormat、CacheBatchAdaptive 有界拆分与取消；HybridRefillGeneration 同步/异步代次闸门；内存缓存和协调职责测试 | Caching 408、EasyCaching 266 通过，后者 140 外部/容量用例跳过；不能称集群一致性已验证 |
| DDD / Uow / 事件 | DomainEventDispatchSession 按引用去重、最大 32 轮、成功后清理；EFCore/FreeSQL 自动登记调用链真实存在 | Ddd.Domain 310、Uow 20 通过；Events/ORM 完整集成套件被 AOP 阻断，自动排空是“已实现、当前集成待验证” |
| 审计 / 安全 / 多租户 | 审计字段/记录测试；JWT issuer/audience/lifetime/signing key 校验均启用；租户上下文作用域；Security 300、MultiTenancy 122、Auditing 434 | 宿主认证/数据库授权配置不等于单元测试覆盖；不宣称完成渗透测试 |
| 外围集成 / 管理模块 | 支付/OAuth 公共入口、参数与失败分支；admin Identity 注册已有代码，不按注释数量计缺口 | Payments 20、MailKit 92 等本地测试通过；退款占位、全局规则占位明确；admin 发布就绪性未证实 |

## 性能与资源

识别的真实热点：SQL 映射命中/未命中与路由、SQL 构建/快照/Clone、Dapper 物化/流式释放；缓存物理键编码、批量响应预算拆分、分片调度与 Hybrid 回填。ModuleLoader 的拓扑遍历在启动期，不能仅凭静态复杂度当请求热路径优化。

本次只运行现有 BDN 0.14.0 `--ci-smoke` 和 `--e2e-smoke` 的 QueryToList。SDK 8.0.424，runtime 8.0.30，Windows 10，Intel Core Ultra 7 270K Plus（BDN 报 24 核）；Release、Dry、LaunchCount=1、IterationCount=1、WarmupCount=1、ColdStart。CI smoke 总耗时 15.703 秒，E2E 13.438 秒，各预设上限 300 秒；没有同时运行其它构建/测试负载。

| 当前 smoke | 输入 | BDN Mean（单样本） | Allocated |
|---|---|---:|---:|
| BuildRawQuery | 一个 Select/From/Where | 58.01 ms | 16.84 KB |
| SQLite QueryToList | 1 行 | 947.9 μs | 34.21 KB |
| SQLite QueryToList | 100 行 | 1,021.7 μs | 47.2 KB |
| SQLite QueryToList | 1,000 行 | 1,486.2 μs | 152.67 KB |

**这些数值只证明当前测量路径能运行，不证明稳态性能、P95、吞吐、回归预算通过或优化收益。** BDN 给出 MinIterationTime 警告，Error=NA；没有创建 before，也没有据此推算容量。原始 report/csv/html 在 `artifacts/benchmark-ci/results/` 与 `benchmark-e2e/results/`。

历史证据：`ai_docs/tasks/BING-CACHE-TODO-20261005-004/performance.md` 与对应 2026-10-05 budget JSON 存在；1K/10K、2 warmups、10 samples、并发配置 16。报告批读 P95 1/4 通过、扫描 P95 2/4、分配 0/8，且其它矩阵有 7 个历史超时边界。它们提示正式预算风险，**不标记为当前 HEAD 的测量失败或成功**。元数据索引见 [historical-performance-index.json](artifacts/historical-performance-index.json)。本次没有重新运行 Redis、FormalHost 或重资源矩阵，没有伪造实际并发/网络请求数。

## 交付与停止

- Completed：规则/范围/影响分析、本次 restore/build、本地测试、SQLite、诊断、性能 smoke、功能去重及可执行计划。
- Open Actionable：F001–F007，7 项，尚未实施。
- Blocked Approval：F008 SQL 生命周期兼容策略；F009/F010 两项新功能设计；backlog C01/C02 候选产品范围。
- Blocked External：F011–F013。
- Not Applicable：本轮没有代码优化，不存在合法 before/after 提升结论。
- Accepted Limitations：见 backlog L01–L05。
- Verified Boundaries：本次未新增容量边界；历史 timeout 保持历史性质。
- Deferred：低价值 TODO 清理、DocFX 站点生成/部署等，见 backlog D01/D02。
- Implementation Progress：0/7；Release Gates：完整构建未过、外部与正式性能未过，不计算虚构百分比。
- No-Progress Check：CHANGED（新增实际运行及 scope 缺陷证据）；Next Action：本会话 STOP，父会话选择/确认计划后分派 Luna 实现、Sol 独立验证。
