# 阶段 A：CAP 跟踪恢复与 Uow 契约文档

- Task ID：`20261022-abp-phase-a-tracing-contracts`
- 日期：2026-10-22。
- 状态：用户已批准 G01/G12 设计，可执行；本计划没有实施源码。
- 父计划：[ABP 基础设施路线图](../20261022-abp-infrastructure-roadmap/plan.md)。
- 实施顺序：A01 失败回归 → A02 Attribute 最小修复 → A03 真实消费路径同类修复 → A04 文档/编译示例 → A05 有界 probe/验证 → A06 独立 Review/收口。
- 规则：根 `AGENTS.md`、用户级规则、`C:/Users/jianx/.codex/GOAL_RULES.md`；执行者另读 execute-plan 技能。用户已授权该方案，常规实现选择无需重新批准。

## 1. 范围与完成定义

本阶段只修复 Trace 透传中跳过目标和污染父上下文的问题，校正 Uow 新旧接口文档并建立有证据的能力矩阵。不得新增 public/protected API、改变默认注册/开关、替换 AspectCore、重写 Uow 或 CAP 生命周期。不要修改 `TraceIdContext` 的公开可变属性/构造语义，也不要创建通用公开 TraceScope。

完成标准：所有冻结的 A01–A06 实现项关闭；目标调用次数、结果/异常/取消、上下文隔离恢复均有直接证据；Uow 关键示例可编译；影响范围验证和阶段最终 Release/net6/net8 通过或未验证门禁明确分类；独立 Review 无 OPEN_ACTIONABLE。没有正式性能预算或 clean-source 发布门禁未满足不等同代码尚需修复。

禁止 add/commit/push/reset/restore/clean、SDK/依赖/TFM/API baseline/性能预算改动；保留现有未提交修改。禁止运行 workflow-notify 或任何外部通知。历史原 181 项不重新审计。

## 2. 已核实事实与差距

| 证据位置（仓库相对路径） | 实际行为 | 本阶段判断 |
|---|---|---|
| `framework/src/Bing.Events/Cap/CapTraceAttribute.cs` 的 Invoke | 取第一个带 FromCap 的参数；无此参数时 next；有参数但值 null/非 CapHeader 时无 next | 确认缺陷：会吞掉目标调用 |
| 同文件 InitTraceIdContext | 有头时修改 Current.TraceId；没有 finally | 确认缺陷：外层可变 Trace 对象被修改 |
| `framework/src/Bing.Core/Bing/Tracing/TraceIdContext.cs` | AsyncLocal 保存对象引用，属性可变；单参数构造仅 null/空串时生成 Guid | 必须新建独立对象；AsyncLocal 流恢复不能撤销共享对象字段修改 |
| `modules/admin/src/Bing.Admin/Modules/CapModule.cs` | AddServices 注册 CapSubscribeInvoker；其 GetInstance 在解析前写 trace，再调用 base；取消重试注册保留这个派生类型 | 真实消费路径存在同类污染；须真实调用器先红回归 |
| `framework/src/Bing.Events/Cap/BingSubscribeInvoker.cs` | 显式 ISubscribeInvoker 包装原生 InvokeAsync，负责取消转换和在途排空；保留 GetInstance 扩展点 | 已验收逻辑，原则上不改 |
| `framework/src/Bing.Events/Cap/Extensions.Service.cs` | 已适配类型/实例重复注册保持，未知自定义注册拒绝 | 不新增注册模式，不覆盖调用器 |
| `modules/admin/src/Bing.Admin.FreeSQL/Modules/CapModule.cs` | 注册 selector、启用现有 Bing 调用器，没有对应 TraceId GetInstance 实现 | 不凭同名模块扩修 FreeSQL，不自动新增追踪能力 |
| `modules/admin/src/Bing.Admin.EventHandlers/Implements/TestMessageEventHandler.cs` | CapTrace 使用处为注释 | 不能把 Attribute 测试当作 Admin 的真实消费路径证明 |
| `framework/src/Bing.Uow/README.md` 快速上手 | 旧 IUnitOfWorkManager 被调用 Begin；该接口只有 Commit/CommitAsync/Register/GetUnitOfWorks | 文档确认错误，必须拆分旧提交与新 scope |
| `framework/src/Bing.Uow/Bing/Uow/IUnitOfWorkScopeManager.cs`、IUnitOfWorkScope.cs | Begin(options, requiresNew)、CompleteAsync、参与/root 资源协议真实存在 | 新例使用真实接口，不往旧接口添加 Begin |
| `framework/src/Bing.Ddd.Application.AspectCore/Bing/DependencyInjection/BingUnitOfWorkExtensions.cs` | 已验收 source-backed ValueTask、取消、嵌套和类代理终端分派 | 本阶段不修改，精确回归保护 |

测试入口已确认：`Bing.EventBus.Tests/Cap/BingSubscribeInvokerTest.cs` 使用真实 CAP 8.0.1 invoker、serializer、descriptor、consumer 和 filter；`Bing.Core.Tests/Tracing/TracingAndCancellationTest.cs` 覆盖基础 AsyncLocal；`Bing.Uow.Tests/UnitOfWorkScopeTest.cs` 覆盖 root/嵌套/释放。不能只在 fake delegate 中断言 next 次数而遗漏真实代理返回契约。

### 2.1 CAP 8.0.1 调用边界

已读取固定源码：[ISubscribeInvoker.Default.cs](https://raw.githubusercontent.com/dotnetcore/CAP/v8.0.1/src/DotNetCore.CAP/Internal/ISubscribeInvoker.Default.cs) 与 [ISubscribeInvoker.cs](https://raw.githubusercontent.com/dotnetcore/CAP/v8.0.1/src/DotNetCore.CAP/Internal/ISubscribeInvoker.cs)。原生 InvokeAsync 是非 virtual async 方法：创建 DI scope → 同步 GetInstance → 参数解析 → before filter → await 目标 → after/exception filter → 返回/释放。GetInstance 本身没有目标调用范围；在那里立即恢复会导致目标看不到 Trace。保留该扩展点，在其内部设置新的对象，依靠实际原生 async InvokeAsync 的 ExecutionContext 返回边界恢复调用方；该结论必须由真实 ISubscribeInvoker 测试验证，不能直接调用 GetInstance 充当证据。

### 2.2 基线与证据复用

- 最新基线：[full-suite final-report](../20261022-full-suite-recovery/final-report.md)、该任务 `execution.md` 和 `artifacts/final-candidate-cache-retry-release-l4-20261022T101104Z/`：12,561 Passed / 0 Failed / 1,105 NotExecuted，103 份 TRX。旧 review 的 CURRENT_FINAL_FULL_SUITE_STATUS 早于最后 L4。
- 已验收 Uow、source-backed ValueTask、Provider 与多源 CAP 见父计划证据；没有相关源变更的范围复用，不拼接不同候选为当前全量 PASS。
- 此次局部 before 必须来自开始实施时的实际工作区，不能用 HEAD 覆盖含既有修复的源码。只对本阶段相关文件保存 before 副本和必要身份；不扫描/哈希整个仓库。
- 正式 clean-source release gate 既有 BLOCKED 状态保留；不能清理用户工作区或调整 gate。

## 3. 冻结的运行时契约

### 3.1 Trace 头与参数选择

1. 沿用第一个 FromCap 参数选择顺序，不扩展多头合并规则。无标记参数、值为 null、值不是 CapHeader 时，都调用目标恰好一次并保留父上下文。测试覆盖真实合法的 `[FromCap] CancellationToken` 在首位等不匹配场景。
2. CapHeader 缺少 `Bing.Events.Cap.Headers.TraceId`（值为 `bing-trace-id`）、对应值 null 或空串时，保留父上下文，不新建随机 TraceId，不抛出新的验证异常。
3. 非空字符串为不透明 TraceId，原样传播、不 trim。纯空白保留为非空值，以兼容现有 TraceIdContext 仅 IsNullOrEmpty 的契约；不在本次 bug fix 引入格式校验策略。测试明确记录此选择。
4. 有效非空值创建 `new TraceIdContext(traceId)`；不可修改父对象的 TraceId/RootId/ParentId/ChildId，也不复制这些父链字段到新的独立上下文。即使新旧 TraceId 相等，仍使用新对象证明隔离。
5. 目标可以修改自己的新上下文，不能影响父对象。没有有效头时沿用父对象；本阶段不隔离业务主动修改其继承上下文的行为。

### 3.2 Attribute 的最小内部实现

- 在 Invoke 保存进入时的 `TraceIdContext.Current` 引用。
- 完成参数/头识别，仅有效非空 TraceId 时设置独立对象；不为无效参数增加新返回分支。
- `await next(context)` 在所有分支恰好一次，放入同一受控调用路径；用 `finally` 恢复原引用（包括 null）。
- 保持原返回值、异常对象/取消令牌语义，不捕获吞掉异常、不反射重复调用目标、不在测试中双次消费 ValueTask。
- 同步和 async 目标均走实际 AspectCore 代理；正常、异常、取消、嵌套后父对象引用与全部字段恢复；重叠异步调用互不串线。
- AspectCore 3.0.0 的已取消 Task 快路径可能吞掉取消或破坏返回观察；必须从实际代理调用方断言原取消异常可观察且目标只调用一次。若失败回归证明需要，只允许 CapTraceAttribute 内部最小局部兼容（例如以 Faulted Task 传递同一 OperationCanceledException，保持原 token/异常），不修改全局代理或已验收 Uow 适配器。记录内部 Task 状态适配与调用方可观察语义，不能把取消变成正常返回。
- 可改现有 private helper 或内联最小逻辑；无 public/protected 新成员，无全局缓存，无公共 TraceScope。

### 3.3 Admin 的最小内部实现

- A03 红回归确认后，仅调整 `CapSubscribeInvoker` 的私有 Trace 初始化：有有效非空头时赋新对象，缺失/null/空串不创建；保留 GetInstance 签名、解析顺序、日志等级与现有基类调用。
- 不在 GetInstance 内 try/finally 立即恢复，不把整个 CAP 原生调用器复制到 Admin，不增加公共/受保护 Invoke hook，不改变 ISubscribeInvoker 的取消转换与排空。
- 恢复由 CAP 8.0.1 的真实 InvokeAsync 异步边界验证，覆盖同步完成与真正挂起的目标、异常、取消、连续与并发消费；确保目标和现有过滤器看到消费 trace，返回后父引用/各字段不变。
- 如果真实测试证伪上述最小方案，先输出具体失败证据并在现有接口内收敛。只有确实需要新增公共 API/改变消费策略时才作为超出本阶段的决策，不静默扩建。
- FreeSQL 未复现同类写法，不增加其新追踪行为；原生参数绑定对错误 FromCap 类型的既有异常行为也不更改。

## 4. 已确认修改文件与候选文件

### 4.1 已确认实施范围

| 文件/目录 | 工作 |
|---|---|
| `framework/src/Bing.Events/Cap/CapTraceAttribute.cs` | 修复 next 与上下文恢复，必要中文注释 |
| `framework/src/Bing.Uow/README.md` | 修正旧 manager 示例，新增清晰的 scope 示例与兼容说明；核对已陈旧的 TFM/源文件数表述，不改项目 |
| `framework/src/Bing.Events/README.md` | 记录 Attribute 与实际 Admin 消费路径的适用范围、Trace 值策略与恢复边界 |
| `framework/tests/Bing.EventBus.Tests/Cap/CapTraceAttributeTest.cs`（新增） | 真实代理回归，无需增加依赖 |
| `framework/tests/Bing.Uow.Tests/UnitOfWorkDocumentationExamplesTest.cs`（新增） | 文档旧提交/新 scope 示例的编译与最小行为验证 |
| `docs/architecture/abp-infrastructure-support-matrix.md`（新增） | G01–G12 支持矩阵、入口/开关/Provider/限制/证据位置；置于已有 architecture 目录 |
| 本任务目录下 execution.md、traceability.md、artifacts/ | 当前实施、符号测试映射、原始证据；review.md 仅独立 Reviewer 写 |

### 4.2 有证据后才修改的候选文件

| 候选 | 触发与边界 |
|---|---|
| `modules/admin/src/Bing.Admin/Modules/CapModule.cs` | 已有同类代码证据；A03 真实失败回归确认后只改 Trace 私有方法，不改事务/消费配置 |
| `modules/admin/tests/Bing.Admin.EFCore.Tests/AdminCapTraceInvocationTest.cs`（新增） | 真实 ISubscribeInvoker 路径的直接回归；项目已有 Admin/Events/CAP 引用，保持 net6.0 |
| `framework/tests/Bing.EventBus.Tests/Cap/BingSubscribeInvokerTest.cs` | 仅确有公共测试工厂复用或新的直接保护需要时改；优先新测试文件避免触碰旧取消验收 |
| `framework/tests/Bing.EventBus.Tests/Cap/CapTraceProbeTest.cs`（新增） | A05 有界测量入口，手工过滤执行，不默认跑重负载，不新增包 |
| `framework/tests/Bing.EventBus.Tests/UowDocumentationSqlExamples.cs`（新增） | 若需把 README 已有 SQL 多源长例一并纳入编译；该项目现有 SQL/Uow 引用可容纳，避免改依赖图 |
| `docs/toc.yml` 或已有 architecture 入口 | 仅为新支持矩阵补一条导航链接；遵循现有文档结构 |
| 本任务 artifacts 内局部 PowerShell runner | 隔离默认外部环境门控/保存 UTF-8 证据；不得覆盖历史 runner/artifacts |

只读参考：TraceIdContext.cs、BingSubscribeInvoker.cs、Extensions.Service.cs、旧/新 Uow 接口、BingUnitOfWorkExtensions.cs、现有 Core/Uow/AOP 测试、两套 Admin 项目配置与 Sample。非本阶段原因不得改这些参考源码。确有编译验证需要时可在既有测试目录加文件，不新增项目/PackageReference。

## 5. 可执行任务

### A01 — 冻结局部基线并建立失败回归

- 目标：让原始问题可复现，防止“先修后补测”掩盖假测试。
- 现状：有静态根因，尚无本阶段真实红 TRX。
- 范围：新增 Attribute 测试、Admin 候选测试和任务证据目录；不改生产文件。
- 步骤：记录 HEAD、相关 dirty 文件、SDK/runtime、TFM、已有 baseline artifact；保存本阶段将修改的生产文件原文（UTF-8）/局部身份；搭建真实代理与原生 CAP 测试，先运行缺参数/错误类型/父污染核心方法。缺陷已固定的分支可先绿，不能伪造红。
- 依赖：本计划与规则已读；原有工作区保持。
- 验证：两 TFM 的 Attribute 核心回归；Admin 原生路径 net6.0。红证据应指向“目标次数 0”或“父引用所指对象字段污染”，不是构建失败、找不到测试、环境故障。
- 风险：代理实际未生效会产生假绿；需确认目标上 Attribute 被实际执行，并记录 inside TraceId/次数/返回值。
- 验收：before 源码、红 TRX、原始 stdout/stderr、命令/退出码和失败方法齐全；没有使用 git restore/revert。

### A02 — 修复 Attribute 且验证返回协议

- 目标：无论头参数如何都执行一次，跟踪上下文限定在调用期间。
- 现状/范围：§2 确认的 CapTraceAttribute 缺陷；只改该生产文件和专用测试。
- 步骤：按 §3.2 最小实现；运行原红方法，再完整回归矩阵；对已完成取消与挂起后取消分别从代理调用方观察，必要时按 §3.2 做局部兼容并先保存失败证据；必要中文注释说明可变引用隔离。
- 依赖：A01 红证据。
- 验证：§6 R01–R09，两 TFM；真实同步返回、Task/Task<T>、ValueTask/ValueTask<T> 均由调用方消费一次。
- 风险：scope 抑制导致嵌套测试并未进入内层 Attribute；测试需明确允许嵌套拦截配置并断言内外各一次，不改全局配置。source-backed ValueTask 既有 Uow 测试必须保持，不能为扩大 Attribute 支持改旧适配器。
- 验收：所有目标次数/结果和恢复断言通过；无公开 API 改动；无全局状态/缓存/公共 helper。

### A03 — 真实 CAP 消费路径核查与同类修复

- 目标：修复已注册 Admin 调用器的同类污染，证明异步边界正确。
- 现状：CAP 固定源码与 Admin 注册已确认，Attribute 在仓库使用处仅注释；不能互相替代证据。
- 范围：§4.2 Admin 两文件；若未复现则记录测试与 NOT_APPLICABLE 原因，不修改行为。
- 步骤：用真实 ServiceCollection、CapModule 注册描述或等价保留实际 CapSubscribeInvoker 类型的构造、ISubscribeInvoker、JsonUtf8Serializer、ConsumerContext 和真正订阅对象运行；确认返回实际执行器类型。先红，后只改私有初始化为新对象。
- 依赖：A01、§3 trace 值策略；与 A02 使用相同语义但不新建跨程序集公共辅助 API。
- 验证：R10–R14；目标/过滤器内部 trace、父对象/所有字段、异常取消、连续并发消费。保留原取消包装、过滤器处理、GetInstance 扩展点、排空契约。
- 风险：把 GetInstance 调用后的状态当作完整消费恢复；只用真正 ISubscribeInvoker.InvokeAsync 作为恢复验收入口。
- 验收：同类污染红→绿、真实调用顺序明确、消费/事务策略未变。若变更消费控制/过滤器/重试/事务链，按 §7 条件启动真实 MySQL/RabbitMQ，而非以本地方法测试代替。

### A04 — Uow 文档、可编译用法及支持矩阵

- 目标：开发者复制关键示例后能编译，并理解旧提交与新 scope 的边界。
- 现状：旧 IUnitOfWorkManager 无 Begin；后半多源 scope 已有真实示例。
- 范围：§4.1 文档、Uow 编译示例；必要时 §4.2 SQL 示例编译文件/导航。
- 步骤：把旧例写为注入 IUnitOfWorkManager 后 CommitAsync(token)，说明它提交已注册工作单元、不拥有 Begin；把新例写为注册 AddUnitOfWorkScopes、注入 IUnitOfWorkScopeManager、await using Begin、CompleteAsync(token)。说明默认/嵌套/requiresNew 与新 DI scope 的区别；requiresNew 不能被写为自动重新解析 EF。保留多库顺序本地提交、可能部分提交及非事务写入不可回滚说明。
- 编译策略：关键旧/新示例使用完整 usings/类/方法，逐字对应既有 Uow 测试项目中的编译源码（标注路径/方法）；用记录资源检验旧提交一次与新 scope 完成/释放、异常不完成。不要添加第三方编译器依赖。已有 SQL 长例若作为可复制主例保留，则在已有 EventBus 测试项目编译同源代码，或引用已编译 Sample 的准确源位置，不以 verify-symbols 的名字匹配代替编译。
- 支持矩阵：每行含 Gap、能力状态、已验证入口、显式开关、Provider/TFM、限制、生产符号、测试方法、raw artifact/历史证据；G02–G11 未完成目标记“规划/部分”，不凭文档存在标支持。G01 区分 Attribute opt-in 与 Admin 既有调用器，不宣称所有 CAP 默认启用。FreeSQL 无新增 Trace 适配明确列出。
- 依赖：A02/A03 最终实际行为；历史能力仅引用父计划已验收证据。
- 验证：编译/运行文档示例；python 文档检查；链接可达、UTF-8 无乱码；实际程序集/项目 TFM 与文档一致。
- 风险：支持矩阵把跳过写成支持，或修错文档时顺便改 API。
- 验收：没有 IUnitOfWorkManager.Begin 幻想接口，旧/新示例可编译，G01–G12 与限制有明确位置与证据。

### A05 — 验证与有限 Trace 开销观察

- 目标：确认局部行为和跨项目兼容，给出有界观察而不创造性能预算。
- 范围：受影响测试与 task artifacts；§8 probe 仅 trace 热路径。
- 步骤：每次先 Change Impact Analysis，再 L0/L1，正确性通过后受影响 L2；A03 触发时做对应 L3；最终 candidate 做一次 L4。记录命令、退出码、TRX 测试计数、失败/未执行名单、源范围身份与环境。
- 依赖：A02–A04；probe before 在生产修改前冻结，之后不能 current 覆盖 before。
- 验证：§7/§8。
- 风险：重跑相同实验/全量、耗尽时间窗口、混用历史与当前计数。
- 验收：必需验证有直接原始证据，未运行项单列；旧 AOP source-backed/取消/嵌套回归保持；没有放宽门控/预算。

### A06 — 独立 Review 与收口

- 目标：独立核查 plan、真实 diff、测试与证据，完成准确交付。
- 范围：本阶段源码与合理回归面，execution/traceability/review；不重审历史全框架。
- 步骤：实施者完成 execution.md 与最终符号→测试映射；独立 Reviewer 写 review.md。只处理 OPEN_ACTIONABLE，修复后按影响范围复测，不自动全量重跑。
- 依赖：A05 证据齐备。
- 验证：目标一次、真实 CAP 生命周期、无新增公开 API、文档编译、候选身份与 TRX 一致、复用证据源范围未变。
- 风险：把性能预算/clean-source gate 缺失当成代码 bug 触发无穷修复。
- 验收：无 OPEN_ACTIONABLE；最多 3 轮自动 Review/Fix，No Progress Guard 生效；实现、测试、性能证据、外部/Release 状态分开。收口后停止，不自动进入 B。

## 6. 生产符号与直接测试矩阵

下表测试名为计划新增名；实施可按实际命名调整，但必须在 traceability.md 填完整限定名、项目、TFM、结果与精确 TRX。不得把“拟新增”写成已 Passed。

| ID | 生产符号/行为 | 拟直接测试方法与必需断言 |
|---|---|---|
| R01 | CapTraceAttribute.Invoke 无 FromCap | `CapTraceAttributeTest.Invoke_WithoutFromCap_ShouldCallTargetOnce`：同步/Task/ValueTask 返回值、次数 1、父引用不变 |
| R02 | FromCap 值 null 或非 CapHeader | `Invoke_WithNullOrNonHeader_ShouldCallTargetOnce`：包括合法 CancellationToken 标记，次数 1、token/参数原样，原返回值 |
| R03 | 缺失/null/空 TraceId | `Invoke_WithoutNonEmptyTraceId_ShouldKeepParent`：父有/无两种状态，不生成随机 trace、目标 1 |
| R04 | 非空/空白/相同 TraceId | `Invoke_WithTraceId_ShouldUseIndependentContext`：新引用、原样值、父四字段不变；空白兼容可单独 theory |
| R05 | 正常异步完整调用范围 | `Invoke_WithAsyncTarget_ShouldKeepTraceUntilCompletion`：真实挂起期间与恢复后观察，Task<T>/ValueTask<T> 结果准确 |
| R06 | 异常与取消 | `Invoke_WhenTargetThrowsOrCancels_ShouldRestoreParent`：原异常/令牌语义、次数 1、finally 后原引用/字段；sync、异步都覆盖 |
| R07 | 嵌套 | `Invoke_WhenNested_ShouldRestoreEachParent`：外→内→外→根的 trace 和引用正确，失败/取消嵌套也恢复；确认内层真实被拦截 |
| R08 | 并发 | `Invoke_WhenOverlapped_ShouldIsolateContexts`：两个真正重叠任务用信号协调，分别检查内部 trace、共享父不变；finally 释放信号，禁止延时碰运气 |
| R09 | 返回值与现有 Uow 兼容 | 专用代理返回形态测试；已有 `Bing.Aop.AspectCore.Tests.UnitOfWorkAdapterTest` 精确回归 source-backed 成功/取消/嵌套/虚方法及 once 契约，不改其生产适配器 |
| R10 | Admin GetInstance + CAP InvokeAsync | `AdminCapTraceInvocationTest.InvokeAsync_WithTrace_ShouldKeepTargetTraceAndParent`：真实 ISubscribeInvoker、目标与 filter 可见、父四字段不变，sync 与 suspend |
| R11 | Admin 无有效头 | `InvokeAsync_WithoutNonEmptyTrace_ShouldKeepParent`：缺/null/空、父 null/non-null，不生成新值 |
| R12 | Admin 异常/取消 | `InvokeAsync_WhenFaultedOrCancelled_ShouldRestoreParent`：原生 filter 语义/既有 Bing 取消包装不变，父恢复 |
| R13 | Admin 连续/并发 | `InvokeAsync_WhenSequentialOrConcurrent_ShouldIsolateTrace`：不同头和有头→无头消息不串线，真正重叠而非顺序 Task.WhenAll |
| R14 | 既有消费契约 | `BingSubscribeInvokerTest` 全类与 `AdminCapRegistrationTest` 全类：扩展点/注册保持、过滤器/排空/取消回归 |
| R15 | IUnitOfWorkManager.CommitAsync 文档 | `UnitOfWorkDocumentationExamplesTest.LegacyCommitExample_ShouldCommitRegisteredWorkOnce`：实际 manager、记录 Uow，token/次数一致 |
| R16 | IUnitOfWorkScopeManager.Begin 文档 | `ScopeExample_ShouldCompleteAndRestore` / `ScopeExample_WhenFailed_ShouldNotComplete`：可编译同源例、资源完成/回滚/释放、Current 恢复 |

基础 TraceIdContext 不改源码时复用其已有单测；不要为其不变实现额外扩建测试项目。source-backed ValueTask 的已有支持范围与 Attribute 返回形态覆盖分别记录，不宣称 CAP 8.0.1 支持任意未验收 awaitable。

## 7. 验证命令和影响分级

以下命令依据真实 `framework.tests.props`（net8.0/net6.0）、现有 csproj 和最终全量 runner。工作目录为仓库根；本节命令模板中的 `$evidenceRoot` 由执行者设为本任务唯一新 artifact 目录，明确 UTF-8 保存 stdout/stderr 和退出码。命令不得覆盖历史 evidence。短串 ASCII 参数可直接 shell，中文报告用显式 UTF-8 文件读写。

### 7.1 影响分析模板

```text
ChangedFiles: CapTraceAttribute.cs；条件触发的 Admin CapModule.cs；直接测试与文档
ChangedProjects: Bing.Events；条件触发 Bing.Admin；对应测试项目
ChangedPublicContracts: none（公开/受保护形状不变；Trace 缺失/恢复语义修复）
ChangedRuntimePaths: Attribute invoke；条件触发 CAP GetInstance 的 trace 对象创建
ChangedProviders: none
ChangedTFMs: none；framework tests net6.0/net8.0，Admin tests net6.0
ChangedBuildPackaging: none
ChangedBenchmarkHarness: task-local bounded probe only
ChangedDocsOnly: Uow README/support matrix；生产 Uow 不变
AffectedDependents: EventBus/Admin consumers；AOP 兼容回归
RiskLevel: MEDIUM
```

执行前用实际 diff 更新，不机械抄模板。新增公开签名、SQL/Provider/事务路径变化则已超出模板，重新分类与必要验证。

### 7.2 L0/L1（before 和 after 目录分别命名）

```powershell
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$evidenceRoot = 'ai_docs/tasks/20261022-abp-phase-a-tracing-contracts/artifacts/<unique-run>'

dotnet test framework/tests/Bing.EventBus.Tests/Bing.EventBus.Tests.csproj -c Release -f net6.0 --no-restore --filter 'FullyQualifiedName~CapTraceAttributeTest|FullyQualifiedName~BingSubscribeInvokerTest' --logger trx --results-directory "$evidenceRoot/L1/net6.0"
dotnet test framework/tests/Bing.EventBus.Tests/Bing.EventBus.Tests.csproj -c Release -f net8.0 --no-restore --filter 'FullyQualifiedName~CapTraceAttributeTest|FullyQualifiedName~BingSubscribeInvokerTest' --logger trx --results-directory "$evidenceRoot/L1/net8.0"

dotnet test modules/admin/tests/Bing.Admin.EFCore.Tests/Bing.Admin.EFCore.Tests.csproj -c Release -f net6.0 --no-restore --filter 'FullyQualifiedName~AdminCapTraceInvocationTest|FullyQualifiedName~AdminCapRegistrationTest' --logger trx --results-directory "$evidenceRoot/L1/admin-net6.0"

dotnet test framework/tests/Bing.Uow.Tests/Bing.Uow.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~UnitOfWorkDocumentationExamplesTest' --logger trx --results-directory "$evidenceRoot/L1/docs"
dotnet test framework/tests/Bing.Aop.AspectCore.Tests/Bing.Aop.AspectCore.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~UnitOfWorkAdapterTest' --logger trx --results-directory "$evidenceRoot/L1/aop-compatibility"
python eng/ci/check-docs.py --quiet
git diff --check -- framework/src/Bing.Events/Cap/CapTraceAttribute.cs modules/admin/src/Bing.Admin/Modules/CapModule.cs framework/src/Bing.Uow/README.md framework/src/Bing.Events/README.md framework/tests/Bing.EventBus.Tests framework/tests/Bing.Uow.Tests modules/admin/tests/Bing.Admin.EFCore.Tests docs/architecture/abp-infrastructure-support-matrix.md
```

before 先过滤新增核心失败方法，确认生产代码未改；完整矩阵按实际新增方法执行。after 扩成上述类过滤。测试未发现或全部跳过不得当作通过。文档全局 checker 如失败，先分清既有基线问题与本阶段引入，不顺便修全文档。

### 7.3 L2 受影响项目

```powershell
dotnet test framework/tests/Bing.EventBus.Tests/Bing.EventBus.Tests.csproj -c Release --no-restore --logger trx --results-directory "$evidenceRoot/L2/eventbus"
dotnet test framework/tests/Bing.Uow.Tests/Bing.Uow.Tests.csproj -c Release --no-restore --logger trx --results-directory "$evidenceRoot/L2/uow"
dotnet test modules/admin/tests/Bing.Admin.EFCore.Tests/Bing.Admin.EFCore.Tests.csproj -c Release -f net6.0 --no-restore --logger trx --results-directory "$evidenceRoot/L2/admin"
```

Admin 仅 A03 实际修改时必需；仅文档示例的 Uow 项目无生产改动，执行一次示例及项目测试足够。不要重复所有无关框架项目。无新源变化时复用通过证据。

### 7.4 L3 条件触发

- 只改 Trace 对象赋值、未改变消费控制/过滤器/重试/事务时：真实原生 CAP 调用器本地回归为本阶段必需；既有双 MySQL/RabbitMQ 事务消费证据按源范围未变复用，不重复启动外部设施。
- 实际 diff 改变 CAP 消费控制/事务链、过滤器处理或取消/重试行为时：先说明影响，再使用现有 `eng/ci/Invoke-AdminMySqlCapIntegrationTests.ps1` 的环境门控与批准测试数据库；真实 MySQL/RabbitMQ 证据不可由 mock 代替。现有脚本参数已确认：

```powershell
pwsh -File eng/ci/Invoke-AdminMySqlCapIntegrationTests.ps1 -Framework net6.0 -Configuration Release -ResultsDirectory "$evidenceRoot/L3/admin-mysqlcap"
```

- SQL 执行、Provider、连接、查询工厂不在 A 允许改动范围。若修复证据确实触及该范围，先记录范围偏差并按根 AGENTS 直接测试及 SQLite gate 执行，不能悄悄跳过或扩大成 B。
- 外部环境不可用记 BLOCKED_EXTERNAL，完成其余已授权工作；不输出“集成已通过”。

### 7.5 阶段最终 L4

用独立子进程参考历史 `final-candidate-cache-retry-release-l4-20261022T101104Z/run-final-checkpoint.ps1` 的门控隔离逻辑，新 runner 写到本任务目录；不要运行原脚本而覆盖其 PSScriptRoot 输出。外部 `RUN_*` gate 显式 false、清理子进程中的连接变量、清除 RUN_METADATA_PATH，仅记录变量名，不输出秘密；不传隐式 runsettings、不改变 xUnit 默认并行。保留 SQLite 默认本地集成。

```powershell
dotnet build Bing.All.slnx -c Release --no-restore --nologo -m:1
dotnet test Bing.All.slnx -c Release --no-build --no-restore --logger trx --results-directory "$evidenceRoot/L4/trx"
```

阶段 candidate 固定后运行一次。解析 TRX 按项目/TFM核对真实覆盖、Passed/Failed/NotExecuted，并列关键新增与已有 AOP 方法结果；不能只依赖进程退出码。基线 103 运行单元是对照，新增测试方法会改变计数，不要求机械保持 12,561。最终只改报告时复用 L4；若真正源码再变，按影响分析决定证据失效及阶段 fresh gate。restore 仅在现有 assets 缺失时按当前固定依赖执行，不升级包。

## 8. 有界 Trace 开销 probe

Trace 新对象改变分配路径，安排一次局部观察；不运行 SQL benchmark、数据库负载、500K/1M 或完整资源矩阵。

- 入口：真实代理 Attribute 调用；若 Admin 修复，增加真实 ISubscribeInvoker 同步完成的内存消费者。无网络/数据库；记录服务和代理准备阶段，计时只包含稳态调用。
- 固定工作负载：无头、有效 trace 且已有父上下文两种；可补不匹配参数用于正确性，不把旧实现不调用目标的耗时当性能优势。每场景预热 100 次，正式固定 5 批 × 1,000 次，每次均校验目标调用计数；单进程顺序执行，记录 requestedConcurrency=1/maxActualParallelism=1。
- 同步完成用例记录 elapsed ticks、操作数和适用的当前线程分配字节；若有真实 await 切线程，不使用 GC.GetAllocatedBytesForCurrentThread 作跨线程总分配统计，标为不适用或采用明确记录的进程指标。不得报告伪造的零分配。
- before 身份：A01 冻结的实际源码/既有 DLL 和同构 harness。优先在修改前运行可比 trace 场景并保存原结果，后续不回滚工作区；若 before 不具相同调用语义（例如原 null 参数根本未执行），BEFORE_STATUS=NOT_APPLICABLE，当前场景仅记录正确性/观察值。
- before/after 固定 Release、同一 runtime/TFM/机器/方法/次数；net6.0 与 net8.0 分别比较。不能把不同 TFM/不同 Provider 写成性能提升。
- 预先界定进程窗口：每个 TFM 的整组 probe 短诊断最多 30 秒；如诊断揭示 harness 启动问题，只允许一次正式确认最多 60 秒。成功诊断已有完整数据时不重复确认；after 是新 candidate 的独立观察。外部 runner 到时终止自己启动的 probe 子进程树，保留超时原始输出，不杀其他 dotnet 进程、不增大窗口继续跑。
- 窗口只限制实验耗时，不是发布性能阈值。没有获批 regression budget，THRESHOLD_STATUS=BLOCKED_APPROVAL/未设定；PERFORMANCE_EVIDENCE_STATUS 可为 OBSERVED，不能宣称 performance gate PASS。无需为此再次请求常规实现授权，也不触发 Fix。
- 若测量发现明确局部泄漏、无限重入、目标次数错误，作为 OPEN_ACTIONABLE 附可执行验收；一般 ns/op 差异不自行设阈值。超时分清 harness/环境失败与真实受控边界，最多诊断+确认，禁无限循环。

## 9. 原始证据与交付

本任务建议目录：

```text
execution.md                 当前候选、任务项状态、简短轮次索引、影响分析
traceability.md              最终生产符号→行为→完整测试方法/项目/TFM→原始结果
review.md                    独立 Reviewer 的当前结论
artifacts/<run>/before/      局部原源码、候选身份、红回归TRX与日志
artifacts/<run>/L1-L4/       原始命令stdout/stderr、退出码、TRX及明确清单
artifacts/<run>/probe/       harness/环境/输入/次数/时间窗/原始数据及before身份
```

仅 hash 本阶段有关源码/测试程序集/harness以识别候选，不因可做就哈希全仓库。所有保存文本显式 UTF-8。报告不无限堆叠历史，原 artifact 不覆盖。支持矩阵位于 `docs/architecture/abp-infrastructure-support-matrix.md`，本阶段新增回归证据由 execution/traceability 链接，已有能力引用历史最终已验收证据并标复用。

API 验收检查现有 public/protected 声明及最终 diff，确认无变化；不为修 bug 更新 API baseline。文档编译证据与运行正确性分列。最终报告要写清只修复已启用追踪路径、未来阶段尚未实现、空白值保留策略、CAP8.0.1边界与发布门禁限制。

## 10. 循环与状态规则

每轮输出实现任务完成数量、Release gate 完成数量和以下分组：Completed、Open Actionable、Blocked Approval、Blocked External、Not Applicable、Accepted Limitations、Verified Boundaries、Deferred。

- 仅 OPEN_ACTIONABLE>0 允许 Fix；其条目必须有根因、范围和明确验收。
- 实现/测试/性能证据/资源证据/外部gate/Release 分列；不存在性能预算、clean-source BLOCKED、外部环境缺失、跳过测试均不自动变代码缺陷。
- 自动 Review→Fix→Review 最多 3 轮；连续两轮无相关候选/根因/验收/证据变化立即 STOPPED_NO_PROGRESS。
- 同一 finding+candidate+workload+environment+hypothesis 最多诊断一次和正式确认一次；只增加 timeout 不算进展。
- OPEN_ACTIONABLE=0 时停止；若外部/预算 gate 留存则如实 STOPPED_BLOCKED/STOPPED_LIMITATION，不自动进入 B。

## 11. 规划完成状态

已核对规则、实际 Trace/Uow/注册代码、CAP8.0.1 固定调用生命周期、测试项目及命令。已写明 confirmed/candidate 文件、失败回归与 before、内部最小修复、API 不变、文档编译、能力矩阵与有界 probe。本规划阶段只创建两份 plan.md；没有运行实施测试、没有修改业务源码、没有外部通知。
