<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-COMMON-ATTRIBUTES-DECOUPLE-20260930
AI_EXECUTION_UPDATED_AT: 2026-10-04

# 实施执行报告

## 当前结论（2026-10-04）

公共特性解耦、AspectCore 适配与工作单元主要路径已实现。`FIX-033` 已补齐 FreeSQL 部分创建清理，`FIX-031` 已补齐完整工作单元组合故障测试，`FIX-034` 已修正报告当前状态。本轮同时将 FreeSQL 依赖统一升级至 `3.5.311`。

升级后本机 Docker MySQL/CAP 完整事务验收保存于 `artifacts/admin-mysqlcap-results/net6.0-20261003T161736646Z/`：EFCore、FreeSQL 各 5/5 通过，0 失败、0 跳过。前一组合轮次 `net6.0-20261003T161216420Z/` 的 FreeSQL 为 4/5，唯一失败是出箱已持久化但 RabbitMQ 消费者 30 秒内未回调；该用例单项复测通过。保留这一投递波动，不将完整复跑通过解释为恰好一次或稳定时延保证。FreeSQL 生产引用、MySQLConnector Provider 和 SQLite 测试 Provider 已统一声明 `3.5.311`，还原图中的五个 FreeSQL 包均为该版本；框架 `Bing.FreeSQL.Tests` 在 `net6.0`、`net8.0` 各 33/33 通过；Admin FreeSQL 本地 `net6.0` 测试 23 通过、5 个门控跳过、0 失败。FreeSQL 与 EFCore 两个 Admin 宿主均完成默认还原与构建，存在 net6.0 EOL 和既有弃用 API 警告。

当前实际 SDK 为 `8.0.425`，仓库锁定的 `8.0.424` 未安装。以下“历史执行记录”保留此前轮次的原文和当时结论，不能覆盖本节当前状态；测试数量也不能跨版本、跨轮次相加。

## 任务信息

- 任务：公用特性解耦与 AspectCore 升级补齐
- 计划：`ai_docs/tasks/BING-COMMON-ATTRIBUTES-DECOUPLE-20260930/plan.md`
- 执行器：Codex
- 未自动执行 `git commit`、`git push` 或创建 Pull Request。

## 计划执行情况

| 计划项 | 状态 | 证据 |
| --- | --- | --- |
| T1 抽离公共特性 | 已完成 | 中立程序集、类型转发、属性契约测试和旧编译消费者测试 |
| T2 AspectCore 执行器 | 已完成（历史回归） | 参数校验、属性注入、Ignore 代理及旧消费者测试已有直接证据；本轮未重跑 AOP 全量 |
| T3 工作单元执行器 | 已完成本机验证 | FreeSQL 部分创建及完整故障链测试通过；升级后 Docker 两套各 5/5，通过次数和一次投递超时均留档 |
| T4 缩减依赖与目标框架 | 已完成本机验证 | 两套 Admin 宿主默认还原构建通过；FreeSQL 3.5.311 依赖图一致，框架测试 net6/net8 各 33/33 |
| T5 遗留项和文档 | 已完成本轮更新 | Sample HTTP 与资源验证沿用已注明的历史证据；本报告和测试映射已更新，锁定 SDK 8.0.424 尚未验证 |

## 当前验收与 TODO

| 范围 | 最近证据 | 当前结论 |
| --- | --- | --- |
| FreeSQL 3.5.311 框架测试 | `Bing.FreeSQL.Tests` net6.0/net8.0 各 33/33 | 已通过框架层回归；不替代 Admin CAP 事务测试 |
| FreeSQL 3.5.311 Admin 本地测试 | `Bing.Admin.FreeSQL.Tests` net6.0 为 23 通过、5 个门控跳过、0 失败 | 本地行为通过；跳过项不算通过，真实事务仍看 Docker TRX |
| MySQL/CAP 真实事务 | `net6.0-20261003T161736646Z` EFCore、FreeSQL 各 5/5，0 跳过 | 升级后 Docker 验收通过；前一轮 FreeSQL 消费者等待超时保留为波动证据 |
| FreeSQL 工作单元故障清理 | `CapUnitOfWorkExtensionsTest` 12/12；`AdminUnitOfWorkCleanupTest` 5/5 | 新建事务失败回滚、既有事务保留、业务/取消/Flush/释放组合故障已直接验证 |
| FreeSQL Admin 宿主 | 默认还原与构建通过 | 升级后编译与容器事务均通过；net6.0 EOL 警告 |
| EFCore Admin 宿主 | 默认还原与构建通过 | 对照宿主编译与容器事务均通过；net6.0 EOL 和弃用 API 警告 |
| SDK 8.0.424 | 本机实际 8.0.425 | 锁定版本未验证；不修改 `global.json` |

当前没有由本轮证据确定的工作单元代码缺陷；锁定 SDK 8.0.424 和外部部署环境仍未验证。前轮 Sample、AOP、Core、MVC、Validation 测试数量均属于当时的历史证据，本轮未作为升级后的新结果。消费者等待曾超时一次，后续应按出箱、收件和 broker 状态观察，不以扩大超时时间代替诊断。

## 当前生产符号到测试方法映射

- `Bing.FreeSQL` 约定注册与基础仓储能力：`Bing.FreeSQL.Tests` net6.0/net8.0 各 33/33；具体历史方法映射仍见下文和 `test-mapping.md`，此数量不覆盖 Admin CAP 路径。
- `FreeSQL CapUnitOfWorkExtensions.BeginTransaction`：`BeginTransaction_WhenDispatcherIsMissing_ShouldNotOpenDatabaseTransaction`、`BeginTransaction_WhenCapBeginFails_ShouldRollbackNewDatabaseTransaction`、`BeginTransaction_WhenExistingDatabaseTransactionFails_ShouldKeepExistingTransaction`、`BeginTransaction_WhenCleanupFails_ShouldAttemptEveryCleanup`。
- `FreeSQL AdminUnitOfWork.SaveChangesAsync`、`RollbackBoth`、`TransactionCleanup.DisposeAndCapture`：`AdminUnitOfWorkCleanupTest` 的四项完整调用链测试，以及 `AdminMySqlCapIntegrationTest` 的同步成功/失败、异步代理成功、业务冲突和执行中取消测试。
- `EFCore AdminUnitOfWork.SaveChangesAsync`：`AdminMySqlCapIntegrationTest` 同一 Docker 轮次 5/5；升级 FreeSQL 不改变该历史 EFCore 结果。

## 本轮 FreeSQL 3.5.311 与资源清理验证

- `FIX-033`：CAP 分发器和包装对象在数据库事务前准备；使用 FreeSQL 的 `GetOrBeginTransaction(false)` 区分已有事务。CAP 接入失败时只回滚本次创建的数据库事务，继续尝试释放 CAP 包装对象，并把清理错误与原异常汇总。旧的当前 CAP 值始终恢复。五项直接故障用例覆盖缺失分发器、新建和已有事务、回滚与释放同时失败、底层开启失败。
- `FIX-031`：`AdminUnitOfWorkCleanupTest` 5/5，在完整 `SaveChangesAsync` 调用中验证业务错误叠加 CAP 回滚/释放错误、取消叠加释放错误、CAP Begin 错误，以及数据库已提交后 Flush 失败不伪回滚。第五项通过可控 FreeSQL 工作单元同时注入数据库回滚错误，断言业务、CAP 回滚、数据库回滚、CAP 释放四项异常均保留且清理各尝试一次。其余故障用例使用实际 SQLite Provider；真实 Provider 自身抛出回滚/释放异常的发生率不由测试推断。
- `FIX-034`：报告首页与生产符号映射改为当前证据；下文旧轮次标为历史，不把环境跳过计为通过。
- FreeSQL 版本：`version.dev.props` 与两个 SQLite 测试项目统一至 `3.5.311`，还原图中的 `FreeSql`、`FreeSql.DbContext`、`FreeSql.Extensions.Linq`、MySqlConnector 和 SQLite Provider 均为 `3.5.311`。新增的 FreeSQL `IUnitOfWorkManager` 名称与 Bing 接口冲突，三个 Admin 测试文件改用明确类型别名；未修改生产接口。
- 本地回归：`Bing.FreeSQL.Tests` net6.0/net8.0 各 33/33；`Bing.Admin.FreeSQL.Tests` net6.0 为 23 通过、5 个受环境门控跳过、0 失败；FreeSQL 和 EFCore 两个 Admin 宿主均默认还原并构建成功。现有 net6.0 EOL、Hangfire 弃用 API 及部分仓储隐藏成员警告未在本轮改动。
- 容器验收：MySQL 8.4、RabbitMQ 4.3 的临时容器仅绑定本机随机端口，专用测试库为 `bing_admin_integration_test`。第一次因测试连接关闭 TLS 且未允许获取 MySQL 公钥而在认证阶段失败；更正临时连接串后，第一组合轮次 EFCore 5/5、FreeSQL 4/5，失败点是消费者等待 30 秒超时；该用例单项复测通过。最终 `net6.0-20261003T161736646Z/` 两套各 5/5，诊断 `Passed/Test`。临时容器已停止并自动移除，原有容器未改动。
- 实际 SDK 为 `8.0.425`，锁定 SDK `8.0.424` 未验证。默认加载、同步提交、回滚和取消有本机证据；不能从该轮次推断生产部署环境、恰好一次消费或固定投递时延。

## 历史执行记录（以下各轮次按发生时的条件保存）

下文此前的“外部环境不可用”“只剩外部阻断”“Open Actionable：无”只描述对应旧轮次；以本页“当前结论”和“当前验收与 TODO”为准。

## 已完成事项

- `AutowiredAttribute`、`IgnoreAttribute` 移至 `Bing.Core`，验证标记移至 `Bing.Validation.Abstractions`，均不继承 AspectCore 类型。
- `Bing.Aop.AspectCore` 增加类型转发，旧程序集限定名及预先编译的旧消费者可以读取迁移后的标记。
- `EnableAutowired` 使用作用域属性注入工厂；支持继承属性、原生 `FromServiceContextAttribute`、缺失服务保留原值、setter 异常透传和双标记单次注入。
- `EnableAop` 保留调用方参数选择器；中立参数验证默认关闭，`EnableBingParameterValidation` 显式启用；Ignore 标记在接口、实现和属性访问器路径生效。
- 新增 `Bing.Ddd.Application.AspectCore`，将中立 `UnitOfWorkAttribute` 与 `BingUnitOfWorkScope` 适配到 AspectCore；成功提交、CommitAfter、失败/取消、同步/异步、嵌套、并发和三种 Scope 行为有直接测试，重复注册不会追加适配配置。
- 移除 AOP 适配器对完整 `Bing.Validation` 的直接引用，只保留 `Bing.Validation.Abstractions`；更新 DDD、Validation、架构和迁移文档。
- 插件程序集身份校验增加 Culture 和 PublicKeyToken 比较，并补充版本、身份不匹配和解析回调退订测试。
- Sample `/sample/lazy` 实际调用 `LazyGetRequiredService` 并返回 `lazy-service-ready`；请求返回 200，停止宿主后端口已释放。
- 新增 Sample 自动冒烟测试，覆盖真实 HTTP 请求、Lazy 业务值和 StopAsync 后端口释放。
- 新增真实插件程序集的可收集 AssemblyLoadContext 回收测试，验证 IgnoreAspect 代理调用后容器和插件代可回收。
- 新增 NuGet 最小消费者脚本，覆盖契约 netstandard2.0、AOP net6.0 和 Autowired 类型转发运行验证。
- 新增工作单元接口/实现/类型级标记与重复标记测试，并修正实现方法标记的代理回调。
- 参数验证启用状态改为只接受本次配置回调新增的 AspectCore 参数拦截器或 Bing 显式启用标记；无关拦截器不会打开中立参数验证。
- 接口和实现方法参数的 `NotNull`、`NotEmpty`、`Valid` 按具体标记逐项去重，互补约束不会互相覆盖。
- 清理 `Bing.Validation.Tests` 的重复测试类名，并将不存在的 `GetErrors()` 测试调用迁移到现有 `ToValidationMessages()` API；未改变验证生产逻辑。
- 删除 `BingParameterInterceptorSelector` 中无调用的私有验证标记辅助方法；AOP 两个目标框架回归均保持通过。
- `ExceptionPrompt` 的提示注册、读取和匹配改为快照式同步访问，避免并发注册异常及枚举期间修改。
- `EnableAop` 对同一服务集合幂等，AspectCore 核心服务和异常提示不重复注册；忽略接口规则改为无闭包类型捕获的静态判断，避免保留可收集插件类型。
- 补充重复 AOP 注册、提示并发注册和忽略接口程序集回收的直接回归测试。
- 修复 Validation 结果集合的字符串构造、复制隔离和空结果过滤；有效集合的 `RaiseException` 直接返回，异常消息保留逐项验证明细。
- 修复金额错误消息占位符格式化，以及固话和中国车牌号正则的边界规则；新增金额错误消息回归断言。

## 部分/未完成事项

- Validation 测试基线已恢复编译并完成行为修复；net8.0/net6.0 均为 186/186 通过。结果集合修复保持复制后的内部列表独立，策略返回 `null` 不再把有效集合误判为无效。
- 预编译旧消费者、插件资源弱引用、SQLite 持久化测试夹具和默认加载上下文事件退订已经补齐；Admin EFCore 和 FreeSQL 的新增持久化测试均已在本地依赖缓存中通过，MySQL/CAP 分布式事务联动没有可用环境，保持未验证。
- 资源测试证明释放次数、异常清理和作用域隔离；没有设定内存预算，也没有把一次工作集变化当成无泄漏结论。AspectCore 3.0 服务提供程序对同时实现同步/异步释放的测试资源走同步释放路径，已作为底层行为记录。

## 修改文件

- 中立契约：`framework/src/Bing.Core/Bing/Aspects`、`framework/src/Bing.Validation.Abstractions/Bing/Aspects`、`framework/src/Bing.Validation.Abstractions/Bing/Validation`、`framework/src/Bing.Ddd.Application.Contracts/Bing/Application/Aspects`。
- AspectCore 适配：`framework/src/Bing.Aop.AspectCore/Bing/DependencyInjection`、`Bing/Parameters`、`Properties/TypeForwardedTypes.cs`。
- 工作单元适配：`framework/src/Bing.Ddd.Application.AspectCore`。
- 目标框架和引用：DDD、MVC.Contracts、FreeSQL、Admin、Sample 及相关 `references.props`/`.csproj`。
- 测试：AOP、Core、Validation 契约、Admin EFCore/FreeSQL、插件身份和旧编译消费者夹具。
- 文档：AspectCore/Validation/DDD README、架构依赖表、包索引、迁移说明和工作单元指南。

## API/数据/配置变化

- 公共标记的命名空间和类型名保持不变；标记不再继承 AspectCore。
- `Bing.Aop.AspectCore` 仍为 `net6.0`，中立契约和原有领域包恢复 `netstandard2.0`。
- 新入口：`EnableBingParameterValidation`、`EnableBingUnitOfWork`。
- 参数验证默认不开启；只启用 `UseServiceContext` 不会执行中立 `AutowiredAttribute`。
- 依赖旧 AspectCore 基类、接口或旧 `Scope` 枚举的外部二进制仍需要迁移并重新编译；类型转发只覆盖标记类型解析。

## 测试结果

| 项目/范围 | 结果 |
| --- | --- |
| `Bing.Aop.AspectCore.Tests` net8.0 | 102 通过，0 失败，0 跳过 |
| `Bing.Aop.AspectCore.Tests` net6.0 | 102 通过，0 失败，0 跳过 |
| `Bing.Core.Tests` net8.0 | 612 通过 |
| `Bing.Core.Tests` net6.0 | 612 通过 |
| `Bing.AspNetCore.Mvc.Tests` net8.0 | 58 通过，1 个既有跳过 |
| `Bing.AspNetCore.Mvc.Tests` net6.0 | 58 通过，1 个既有跳过 |
| Admin EFCore AspectCore 测试 | 6 通过，0 失败，0 跳过 |
| Admin FreeSQL AspectCore 测试 | 6 通过，0 失败，0 跳过 |
| `Bing.Validation.Abstractions` netstandard2.0 构建 | 通过 |
| WebApi Sample 自动冒烟 | 1 通过；HTTP 200，Lazy 业务值正确，StopAsync 后端口释放 |
| `Bing.Validation.Tests` net8.0 | 186 通过，0 失败，0 跳过 |
| `Bing.Validation.Tests` net6.0 | 186 通过，0 失败，0 跳过 |
| `Bing.Ddd.Domain.Tests` net8.0 | 149 通过，0 失败，0 跳过 |
| `Bing.Ddd.Domain.Tests` net6.0 | 149 通过，0 失败，0 跳过 |

## 生产符号到测试方法映射

- `BingPropertyInjectorFactory`、`EnableAutowired`：`AutowiredContainerBehaviorTest.Resolve_ShouldInjectAutowiredPropertyButNotField`、`Resolve_WhenDifferentPropertiesUseBothMarkers_ShouldSetEachOnce`、`DisposeScope_ShouldReleaseCollectiblePropertyDescriptorCache`。
- `EnableAop`、`IgnoreAspectInterfaces`、提示注册：`AutowiredContainerBehaviorTest.EnableAop_WhenCalledRepeatedly_ShouldRegisterAspectServicesOnce`、`IgnoreAspectAssemblyResourceTest.EnableAop_ShouldNotRetainCollectibleIgnoredInterface`、`ExceptionPromptConcurrencyTest.AddPrompt_WhenCalledConcurrently_ShouldKeepOneRegistration`。
- `BingParameterInterceptorSelector`、`EnableBingParameterValidation`：`ParameterValidationRegistrationTest.EnableAop_ShouldKeepParameterValidationDisabledByDefault`、`UnrelatedInterceptor_ShouldNotEnableBingParameterValidation`、`EnableBingParameterValidation_ShouldExecuteNotNull`、`NewAndLegacyParameterValidation_ShouldKeepCustomSelectorAndRunOnce`、`ImplementationParameterAndGenericCollection_ShouldValidateOnce`、`InterfaceAndImplementationMarkers_ShouldValidateEachRule`、`InterfaceNotNullAndImplementationValid_ShouldExecuteBothRules`、`InterfaceAndImplementationSameValidMarker_ShouldExecuteOnce`、`ValidAttributeTest.ProcessObject_WhenParameterIsValidIVerifyModel_ShouldCallValidate`。
- `ValidationResultCollection`、验证异常和 DataAnnotation 属性：`ValidationResultCollectionAdvancedTest.Constructor_CopyFromStringCollection_ShouldCopyResults`、`ValidationResultCollectionAdvancedTest.AddRange_WithNullEntries_ShouldIgnoreNullResults`、`ValidationExceptionExtensionsTest.RaiseException_WhenValid_ShouldNotThrow`、`ValidationAttributesTest.MoneyAttribute_InvalidAmount_ShouldFail`、`ExtendedValidationAttributesTest.TelNoOfChina_InvalidTelNo_ShouldFail`、`ExtendedValidationAttributesTest.PlateNumberOfChina_InvalidPlate_ShouldFail`。
- `IgnoreAttribute` 适配：`IgnoreAttributeBehaviorTest.IgnoredMethod_ShouldSkipInterceptor`、`IgnoredType_ShouldSkipInterceptor`、`IgnoredProperty_ShouldSkipAccessorInterceptor`、`IgnoredBaseMethod_ShouldPreserveNonInheritedAttributeBehavior`、`NativeNonAspect_ShouldRemainSupported`。
- `BingUnitOfWorkInterceptor`、`EnableBingUnitOfWork`：`UnitOfWorkAdapterTest.Success_ShouldCommitThenRunCommitAfter`、`SyncSuccess_ShouldCommitThenRunCommitAfter`、`Failure_ShouldNotCommit`、`Cancellation_ShouldNotCommit`、`Nested_ShouldCommitOnlyAtOuterBoundary`、`NestedScope_ShouldKeepSingleCommitBoundary`、`ConcurrentScopes_ShouldCommitIndependently`、`EnableBingUnitOfWork_WhenCalledRepeatedly_ShouldRegisterOnce`。
- 工作单元标记位置和去重：`UnitOfWorkAdapterTest.InterfaceTypeMarker_ShouldCommitOnce`、`ImplementationTypeMarker_ShouldCommitOnce`、`InterfaceAndImplementationMethodMarkers_ShouldCommitOnce`。
- 中立验证契约：`ValidationAttributeContractTest.Attributes_ShouldInheritDirectlyFromAttribute`、`Attributes_ShouldTargetParametersOnly`、`Attributes_ShouldKeepPublicNames`。
- 插件程序集身份和解析回调：`PluginResourceRegressionTest.DependencyInjectionAssemblyIdentityMismatch_ShouldRejectCultureAndPublicKey`、`SuccessfulRegistration_ShouldUnsubscribeAssemblyResolver`、`FailedRegistration_ShouldUnsubscribeAssemblyResolver`。
- 真实插件代理回收：`IgnoreAspectPluginAssemblyResourceTest.ResolveIgnoredPluginService_ShouldInvokeAndReleaseAssemblyLoadContext`。
- Admin Lazy 工作单元：`Bing.Admin.EFCore.Tests.AdminUnitOfWorkAspectCoreTest.Resolve_ShouldInjectScopedLazyServices` 与 FreeSQL 对应测试；Sample `/sample/lazy` HTTP 冒烟覆盖业务解析和停止释放。
- Sample 自动 HTTP 和停止：`SampleWebApiSmokeTest.LazyEndpoint_ShouldResolveBusinessValueAndReleasePortOnStop`。
- NuGet 消费者：`verify-nuget-consumer.ps1`。

## Build/Typecheck/Lint/Format

- AspectCore、DDD 适配器、Admin 定向测试及 Sample 构建均通过；net6.0 产生的 EOL 警告来自 SDK/传递依赖。
- `Bing.AspNetCore` 和 `Bing.Ddd.Application.AspectCore` 直接构建均为 0 警告、0 错误。
- `dotnet list package --include-transitive` 显示 AOP 适配器的 AspectCore 运行时依赖统一为 3.0.0，且不再传递 `Bing.Validation` 实现包；Admin net6.0 的 Microsoft.Extensions/System.Diagnostics 10.x 链路由 `AutoMapper 15.1.1` 引入，本任务未升级或降级该依赖。
- 已安装的 Bing.Utils 1.5.0 nuspec 仍声明 AspectCore.Extensions.Reflection 2.4.0 的传递下限；这是已发布包元数据，不是 Bing 源码中的直接 ProjectReference。AOP 3.0.0 还原图将该下限统一到 3.0.0。
- 当前实际 SDK 为 `8.0.425`；仓库要求的 `8.0.424` 未安装，未修改 `global.json` 绕过。
- 未进行全仓库格式化，避免改写用户已有的无关差异。

## 计划偏差

- Validation 测试基线先暴露了重复类型和失配 API，已在本轮清理并完成 net8.0/net6.0 全量回归；中立契约测试仍保留在 AOP 项目，没有删除或弱化业务验证测试。
- MySQL/CAP 专用环境缺失，未用 SQLite 或 Mock 冒充分布式事务验收。
- `task-state.mjs start` 曾因写入 `.agents/runtime/current-task.json` 的临时文件权限错误失败；随后 `task-finish.mjs` 因该文件仍指向其他任务而无法收尾。执行报告仍按实际代码和测试结果维护，未修改运行时状态文件。

## 基线问题

- Validation 测试的重复类型、失配 API、结果集合复制、空结果处理、异常明细和属性正则边界已修复，并有双框架回归证据。
- Admin 构建存在 Microsoft.Extensions/System.Diagnostics 10.x 与 net6.0 的支持警告；本次没有顺带升级依赖。

## 已知问题

- 默认 AssemblyLoadContext 中已加载程序集不会卸载，这是当前可信启动期插件设计的明确限制。
- AspectCore 3.0 的底层服务提供程序不保证异步释放优先；需要异步资源释放时必须依照实际容器能力和宿主关闭路径验证。
- 不响应取消的外部代码不能被框架强制终止；清理未完成时不得报告为已释放。

## 风险与回归关注点

- 生产应用使用参数标记时必须显式启用参数验证；旧 `EnableParameterAspect` 入口仍可用，但新旧入口组合应避免重复选择器。
- 使用 `[UnitOfWork]` 的宿主必须同时引用并启用 `Bing.Ddd.Application.AspectCore`；中立标记本身不会产生提交行为。
- 仍需在具备 SDK 8.0.424 和 MySQL/CAP 的受控环境中执行最终验收。

## 当前 TODO 状态

- **Completed**：FIX-015、FIX-016、FIX-018、FIX-019、FIX-020、FIX-021、FIX-022；公共特性解耦、AspectCore 适配、工作单元代理行为和本地双框架回归。
- **Completed**：FIX-017 的真实 Admin EFCore/FreeSQL SQLite 持久化测试各 6/6 通过。
- **Open Actionable**：无。
- **Blocked External**：SDK 8.0.424 未安装；专用 MySQL/CAP 环境不可用。
- **Accepted Limitations**：默认加载上下文中的可信插件程序集不可卸载；不响应取消的外部代码无法由托管框架强制终止。
- **Deferred**：发布不含 AspectCore 传递依赖的新 Bing.Utils 包属于独立包维护任务。

## 本轮新增证据

- Bing.Core.Tests net8.0/net6.0：各 612/612。
- Bing.Aop.AspectCore.Tests net8.0/net6.0：各 102/102。
- UoW 标记位置专项：13/13；覆盖接口方法、实现方法、接口/实现类型和重复标记去重。
- Admin EFCore SQLite 持久化测试：6/6；覆盖成功写入、跨作用域回读和失败不持久化。
- Admin FreeSQL SQLite 持久化测试：6/6；覆盖成功写入、跨作用域回读和失败不持久化。
- Bing.Samples.WebApi.Tests：1/1；覆盖 /sample/lazy、真实 Lazy 业务值、StopAsync 和端口释放。
- verify-nuget-consumer.ps1：契约 netstandard2.0、AOP net6.0 编译和 Autowired 类型转发运行通过。
- IgnoreAspectPluginAssemblyResourceTest：真实夹具程序集、可收集加载上下文和代理调用纳入 AOP 全量测试。

## Reviewer 注意事项

- 重点检查类型转发对旧编译消费者的边界：标记读取已验证，旧 AspectCore 基类继承关系不承诺二进制兼容。
- 重点检查 `BingParameterInterceptorSelector` 对调用方已有选择器的保留，以及属性注入器是否只保留当前作用域缓存。
- 重点检查工作单元拦截器的提交与 `ICommitAfter` 顺序，以及提交异常时不执行提交后回调。

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX
- Fix Scope：recommended
- Review 文件：`ai_docs/tasks/BING-COMMON-ATTRIBUTES-DECOUPLE-20260930/review.md`

#### FIX-001

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Ddd.Application.AspectCore/Bing/DependencyInjection/BingUnitOfWorkExtensions.cs`
  - `framework/tests/Bing.Aop.AspectCore.Tests/UnitOfWorkAdapterTest.cs`
- 修复：增加工作单元注册标记，避免重复追加验证器；补充同步调用、`Nested` Scope、提交顺序和重复注册测试。
- 验证：AOP net8.0/net6.0 各 90 项通过。

#### FIX-002

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Aop.AspectCore.Tests/ParameterValidationRegistrationTest.cs`
  - `framework/tests/Bing.Aop.AspectCore.Tests/IgnoreAttributeBehaviorTest.cs`
- 修复：补充新旧参数入口、自定义选择器、接口/实现双标记、泛型集合和单次执行测试；补充并明确 Ignore 基类标记的原有非继承边界。
- 验证：AOP net8.0/net6.0 各 90 项通过。

#### FIX-003

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/tests/Bing.Aop.AspectCore.Tests/ValidationAttributeContractTest.cs`
  - `framework/tests/Bing.Validation.Tests/Bing/Validation/ValidationAttributeContractTest.cs`
- 修复：将中立验证特性契约测试移入可运行的 AOP 测试项目，并修正反射测试只检查类型自身声明的 `AttributeUsage`。
- 验证：契约测试随 AOP net8.0/net6.0 执行通过；Validation 业务测试仍被既有重复类阻塞。

#### FIX-004

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `ai_docs/tasks/BING-COMMON-ATTRIBUTES-DECOUPLE-20260930/execution.md`
- 修复：更新阶段状态、90 项测试数量、生产符号映射、AutoMapper 15.1.1 引入 Microsoft.Extensions/System.Diagnostics 10.x 的依赖链和外部门禁说明。
- 验证：报告内容与本轮实际测试结果一致。

### Round 1 汇总

- MUST_FIX：0
- 已完成：FIX-001、FIX-002、FIX-003、FIX-004
- PARTIAL：0
- BLOCKED：SDK 8.0.424、MySQL/CAP 外部环境
- FAILED：0
- 回归验证：AOP net8.0 93/93、net6.0 93/93；Validation.Tests 仍为既有编译基线失败。
- 下一步：重新进行独立 Review；不在本轮继续修改外部环境或无关 Validation 业务测试。

### Round 3

- Review 状态：NEEDS_FIX
- Fix Scope：recommended
- Review 文件：`ai_docs/tasks/BING-COMMON-ATTRIBUTES-DECOUPLE-20260930/review.md`

#### FIX-007

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Aop.AspectCore/Bing/DependencyInjection/AspectCoreExtensions.cs`
  - `framework/src/Bing.Aop.AspectCore/Bing/DependencyInjection/BingInterceptorCollector.cs`
  - `framework/src/Bing.Aop.AspectCore/Bing/Parameters/BingParameterInterceptorSelector.cs`
  - `framework/tests/Bing.Aop.AspectCore.Tests/ParameterValidationRegistrationTest.cs`
- 根因：通过拦截器数量变化判断参数验证启用状态，会把无关拦截器或共享配置中的参数拦截器误认为当前服务集合显式启用。
- 修复：参数验证注册状态只在当前配置回调新增 AspectCore 参数拦截器，或当前回调新增 Bing 显式启用处理器时置为启用；选择器和实现参数适配器均读取该服务集合状态。
- 验证：
  - 参数验证专项：net8.0 7/7、net6.0 7/7 通过。
  - AOP 全量回归：net8.0 93/93、net6.0 93/93 通过。

#### FIX-008

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Aop.AspectCore/Bing/Parameters/BingParameterInterceptorSelector.cs`
  - `framework/tests/Bing.Aop.AspectCore.Tests/ParameterValidationRegistrationTest.cs`
- 根因：实现参数适配器按任一标记整体判断接口侧覆盖，导致不同标记之间相互跳过。
- 修复：按 `NotNull`、`NotEmpty`、`Valid` 的具体类型分别判断接口侧是否已声明；相同标记只执行一次，不同标记全部执行。
- 验证：`InterfaceAndImplementationMarkers_ShouldValidateEachRule` 及 AOP 两目标框架全量回归通过。

#### FIX-009

- 严重程度：LOW
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `ai_docs/tasks/BING-COMMON-ATTRIBUTES-DECOUPLE-20260930/execution.md`
- 修复：同步主测试表、执行结论、生产符号映射和审查修复数量；历史 Round 记录保持不变。
- 验证：执行报告与本轮实际测试输出一致。

### Round 3 汇总

- MUST_FIX：0
- 已完成：FIX-007、FIX-008、FIX-009
- PARTIAL：0
- BLOCKED：SDK 8.0.424、MySQL/CAP 外部环境；任务状态脚本写入 `current-task.json` 时权限失败。
- FAILED：0
- 回归验证：AOP net8.0/net6.0 各 93/93；参数验证专项各 7/7。
- 下一步：重新进行独立 Review；不修改外部环境或无关 Validation 基线。

### Round 2

- Review 状态：NEEDS_FIX
- Fix Scope：recommended
- Review 文件：`ai_docs/tasks/BING-COMMON-ATTRIBUTES-DECOUPLE-20260930/review.md`

#### FIX-005

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `framework/src/Bing.Aop.AspectCore/Bing/DependencyInjection/AspectCoreExtensions.cs`
  - `framework/src/Bing.Aop.AspectCore/Bing/DependencyInjection/BingInterceptorCollector.cs`
  - `framework/src/Bing.Aop.AspectCore/Bing/Parameters/BingParameterInterceptorSelector.cs`
  - `framework/tests/Bing.Aop.AspectCore.Tests/ParameterValidationRegistrationTest.cs`
- 根因：AspectCore 的参数选择器只提供服务方法参数，实现方法独有的参数标记不会进入原有选择器。
- 修复：在显式参数验证启用状态下，于拦截器收集阶段检查实现方法参数；仅实现方法独有的 `NotNull`、`NotEmpty`、`Valid` 标记由新增拦截器执行，接口侧已有标记不会重复验证。保留默认 AOP 关闭参数验证及旧 `EnableParameterAspect` 兼容路径。
- 验证：
  - 实现方法参数单独标记测试：net8.0 通过。
  - 参数验证专项：net8.0 4/4 通过。
  - AOP 全量定向回归：net8.0 91/91、net6.0 91/91 通过。

#### FIX-006

- 严重程度：MEDIUM
- 处理要求：SHOULD_FIX
- 执行状态：COMPLETED
- 修改文件：
  - `modules/admin/tests/Bing.Admin.EFCore.Tests/AdminUnitOfWorkAspectCoreTest.cs`
  - `modules/admin/tests/Bing.Admin.FreeSQL.Tests/AdminUnitOfWorkAspectCoreTest.cs`
- 修复：为两套 Admin 真实工作单元代理链增加目标异常不提交、嵌套调用单次提交和 `Target -> Commit -> CommitAfter` 顺序断言，并保留 SQLite 本地依赖链。
- 验证：
  - Admin EFCore 定向测试：4/4 通过。
  - Admin FreeSQL 定向测试：4/4 通过。

### Round 2 汇总

- MUST_FIX：0
- 已完成：FIX-005、FIX-006
- PARTIAL：0
- BLOCKED：SDK 8.0.424、MySQL/CAP 外部环境；任务状态脚本写入 `current-task.json` 时权限失败。
- FAILED：0
- 回归验证：AOP net8.0/net6.0 各 91/91；Admin EFCore/FreeSQL 各 4/4。
- 下一步：重新进行独立 Review；不修改外部环境或无关 Validation 基线。

### Round 4

- 修复项：`FIX-010`
- 修改范围：`framework/tests/Bing.Aop.AspectCore.Tests/ParameterValidationRegistrationTest.cs`
- 修复：增加接口 `[NotNull]` 与实现 `[Valid]` 的互补标记测试，以及接口和实现同时声明 `[Valid]` 时只执行一次的测试。
- 影响分析：仅增加 AOP 参数验证回归夹具，不修改生产代码、公共 API、依赖、目标框架或运行时路径。
- 定向验证：参数验证专项 `net8.0`、`net6.0` 各 9/9 通过。
- 全量验证：AOP 测试 `net8.0`、`net6.0` 各 95/95 通过。
- 环境：实际使用 SDK 8.0.425；SDK 8.0.424 仍不可用。`net6.0` 仅产生既有生命周期警告，无测试失败。
- 当前状态：独立 Reviewer 已将 `FIX-010` 标记为 CLOSED，`OPEN_ACTIONABLE = 0`；剩余项目为外部环境或既有基线阻塞。

### Round 5

- Review 状态：NEEDS_FIX
- Fix Scope：recommended
- Review 文件：`ai_docs/tasks/BING-COMMON-ATTRIBUTES-DECOUPLE-20260930/review.md`
- `FIX-011`：已完成。`ExceptionPrompt` 的注册和读取使用锁保护及快照，补充并发注册测试；Core net8.0/net6.0 定向测试各 1/1 通过。
- `FIX-012`：已完成。`EnableAop` 增加服务集合级幂等标记，并将 AspectCore 适配器注册改为 TryAdd 语义；AOP 全量 net8.0/net6.0 各 97/97 通过。
- `FIX-013`：已完成。忽略接口规则使用无闭包类型捕获的静态谓词，移除程序集扫描和全局类型保留路径；忽略接口回收专项及 AOP 全量 net8.0/net6.0 均通过。
- `FIX-014`：已完成。同步 AspectCore 迁移计数、版本说明、Sample 行为和适配包使用说明；代码测试证据沿用未变更范围。
- 任务状态注册：`task-state.mjs` 仍因写入 `.agents/runtime/current-task.json` 临时文件权限失败，归类为工具环境阻塞；未修改运行时状态文件。
- Round 5 汇总：本轮 4 项审查修复均已完成，`OPEN_ACTIONABLE = 0`；实际 SDK 为 8.0.425，锁定 8.0.424 与 MySQL/CAP 专用环境仍未验证。

## Git 状态

- 工作区包含本任务以及之前任务的未提交修改，未重置、覆盖或清理用户变更。
- 本次未自动 `git add`、`git commit`、`git push` 或创建 PR。

## 本轮 MySQL/CAP 分布式事务验证

- 新增 `modules/admin/tests/Bing.Admin.EFCore.Tests/AdminMySqlCapIntegrationTest.cs`，使用真实 `AdminUnitOfWork`、EFCore MySQL、CAP EntityFramework 存储和 RabbitMQ 订阅，覆盖成功提交、跨作用域回读、业务主键冲突回滚及回滚消息不消费。
- 新增 `eng/ci/Invoke-AdminMySqlCapIntegrationTests.ps1`，在执行前校验 `RUN_MYSQL_INTEGRATION_TESTS`、`RUN_MYSQL_CAP_INTEGRATION_TESTS`、`ALLOW_DATABASE_RESET_FOR_TESTS`、专用测试库名称、MySQL/RabbitMQ 端口和必要凭据；诊断文件不写入连接字符串、用户名或密码。
- 修复 `Bing.Admin.Data.FreeSQL` 的 CAP 工作单元异常路径：出箱动作、保存或取消失败时先清理 CAP 消息缓冲，再回滚 FreeSQL 底层事务；回滚异常与原始异常聚合保留。
- 测试项目使用 SDK 8.0.425 编译通过；无外部门控时 6 项本地测试通过，2 项 MySQL/CAP 测试安全跳过。
- 使用现有全局 NuGet 缓存生成临时离线源后，`Bing.Admin.FreeSQL.Tests` net6.0 重新编译并通过 6/6；临时源已清理。该结果只证明代码和本地 SQLite 回归，不证明 MySQL/CAP 分布式事务。
- `Invoke-AdminMySqlCapIntegrationTests.ps1 -ValidateOnly` 已执行，结果为 `Blocked/Preflight`：本机未设置专用环境变量；使用专用测试库名和本机端口探针重复验证时，MySQL 与 RabbitMQ 均明确不可达。此前已确认 Docker 服务停止、MySQL `10.186.135.162:3306` 不可达、RabbitMQ `10.186.132.60:5672` 和 `127.0.0.1:5672` 均未监听，因此没有伪造分布式事务通过结论。
- 为避免重复使用专用测试库时受到历史主键影响，EFCore CAP 夹具每次运行生成唯一业务键，回滚用例在同一轮复用该键验证重复键失败；不会清理或覆盖非本轮数据。
- FreeSQL Admin 的 `DotNetCore.CAP.MySql` 已从 3.1.2 对齐到 8.0.1，与 CAP 核心和 RabbitMQ 适配器一致；这是还原图发现的直接版本冲突修复，不代表已完成 FreeSQL 的外部 MySQL/CAP 验证。
- `framework/src/Bing.Events.Cap.MySql` 仍是未被当前工程引用的旧 CAP 3.1.2 源码镜像，本轮没有把它混入实际 Admin FreeSQL 依赖图；若后续重新启用，需要单独完成 CAP 版本适配。

本轮状态：**BLOCKED_EXTERNAL**。获得专用 MySQL 测试库和 RabbitMQ 后，在工作区执行：

```powershell
$env:RUN_MYSQL_INTEGRATION_TESTS = "true"
$env:RUN_MYSQL_CAP_INTEGRATION_TESTS = "true"
$env:ALLOW_DATABASE_RESET_FOR_TESTS = "true"
$env:ConnectionStrings__MySqlConnection = "Server=<host>;Port=3306;Database=bing_admin_integration_test;User Id=<user>;Password=<secret>;"
$env:CAP_RABBITMQ_HOST = "<rabbit-host>"
$env:CAP_RABBITMQ_USERNAME = "<user>"
$env:CAP_RABBITMQ_PASSWORD = "<secret>"
pwsh -NoProfile -File .\eng\ci\Invoke-AdminMySqlCapIntegrationTests.ps1 -Configuration Release
```

仅当 TRX 显示 2/2 通过、0 失败、0 跳过时，才可将 MySQL/CAP 联动从外部阻断改为通过。当前仍受 SDK 8.0.424 未安装限制，实际验证使用 SDK 8.0.425。

## 2026-10-03 FIX-023 至 FIX-028 修复进展

本节覆盖上次对照审查新增的问题；前文的历史 `OPEN_ACTIONABLE = 0` 和“仅剩外部环境”结论不适用于这轮审查。

- FIX-023：FreeSQL 分别执行 CAP 和业务事务回滚，原始异常及两项清理异常按顺序保留；数据库已提交后 `Flush` 失败不再声称回滚。EFCore 使用独立于业务取消令牌的清理令牌，回滚错误与原始异常聚合。FreeSQL 3 项及 EFCore 2 项本地故障注入通过；真实 Provider 事务结果仍需专用环境验证。
- FIX-024：EFCore 集成夹具在 CAP 宿主启动前幂等创建专用业务表，不依赖 `EnsureCreated` 对已有 CAP 表的行为。空库、仅 CAP 表、已有业务表三种状态在无 MySQL 环境时仍未运行。
- FIX-025：EFCore 命令目标不再手工保存，由 `[UnitOfWork]` 代理触发提交；成功及回滚路径检查 CAP 出箱持久化，消息消费等待作为辅助断言。真实事务断言仍等待环境执行。
- FIX-026：FreeSQL 增加独立 MySQL/CAP 集成夹具，覆盖成功和重复主键回滚；缺失 CAP `Flush` 现在显式报错。夹具已编译并发现，2 项因门控跳过。取消、清理失败及提交后 Flush 失败的真实 Provider 边界仍需专项补测。
- FIX-027：脚本验证非法 MySQL/RabbitMQ 端口，保留 `Blocked/Preflight` 状态；无网络的 5 项脚本测试通过。脚本现在顺序验收 EFCore、FreeSQL 各 2 项 TRX，不接受跳过。运行前须先还原依赖并使用 PowerShell 7。
- FIX-028：新增 `test-mapping.md` 映射当前生产符号和直接测试，并记录实际验证范围。CAP Core/MySql/RabbitMQ 的测试项目还原图均解析到 8.0.1；FreeSQL 之前的 CAP MySql 3.1.2 升到 8.0.1，是避免同一运行时混用 3.x/8.x 的必要闭包改动。完整 Admin 两宿主构建仍因本机离线缺少 Serilog、SkyAPM 和 EFCore 等包而未通过。

本轮实际使用 SDK `8.0.425`：Admin EFCore `net6.0` 为 8 通过、2 个外部测试跳过；Admin FreeSQL `net6.0` 为 9 通过、2 个外部测试跳过；无网络预检脚本 5 项通过。EFCore 最终一次本地测试因离线缓存与仓库的浮动版本还原无法正常完成，使用本地已有包、命令行 `EfCore6Version=6.0.0` 和 `NoWarn=NU1605` 验证，因此该次数不等同于默认还原图验收；前一次默认资产验证为 6 通过、2 跳过。外部环境中 4 个真实 MySQL/CAP 测试尚未运行，不能宣称事务联动验收通过；锁定 SDK `8.0.424` 也未验证。本轮未重跑 Core/AOP/MVC/Sample 全量回归，其前文数量仅为历史证据。

当前状态：本地实现已推进，`FIX-023/026/028` 仍有上述直接验收缺口；`EXT-001` 专用 MySQL/RabbitMQ 和 `EXT-002` 锁定 SDK 继续受环境阻断。不得将脚本预检通过或测试夹具被发现写成真实事务通过。

## 2026-10-03 Docker 真实事务验收

使用独立临时 Docker 容器 `mysql:8.4`、`rabbitmq:4.3-management`，仅将随机端口绑定到 `127.0.0.1`，使用专用 `bing_admin_integration_test` 数据库。测试连接串只在本机容器验收中设置 `SslMode=Disabled`，不修改生产连接配置。实际 SDK 为 `8.0.425`，目标运行时为 `net6.0`。

首次真实运行揭示并修复了三个可执行问题：CAP `Version` 测试值超过 MySQL 字段宽度；测试订阅者按实例注册时 CAP 未发现消费者；FreeSQL 在 CAP 8 中从 DI 获取 `ICapTransaction` 返回 `null`。FreeSQL 现使用 CAP MySQL 事务包装 FreeSQL 已开启的事务，并由工作单元释放包装对象。新增取消测试又发现 EFCore 错误释放 `Database.GetDbConnection()` 的 EF 自有连接；现由 EF 事务管理连接生命周期。出箱验收改为按唯一负载参数化查询 CAP MySQL 表。

最终 `eng/ci/Invoke-AdminMySqlCapIntegrationTests.ps1` 结果为 `Passed/Test`，诊断和两份 TRX 位于 `artifacts/admin-mysqlcap-results/net6.0-20261003T142701992Z/`：EFCore 3/3、FreeSQL 3/3，均为 0 失败、0 跳过。EFCore 连接所有权的最后调整后，EFCore 三项真实测试再次 3/3 通过。六项覆盖代理提交、业务失败回滚、出箱持久化、成功消费及出箱动作后的取消回滚。消费者可能重试，测试不证明恰好一次处理。

本轮已解决 `EXT-001` 的本机容器验收；外部部署环境及锁定 SDK `8.0.424` 不在此次通过结论内。FreeSQL 的提交完成后 `Flush` 再失败无法回滚已提交数据库事务，仍是需要业务幂等与重试策略处理的边界。前文“外部测试跳过”的记录保留为历史状态，以本节最终验收为准。

收口验证：预检脚本无网络用例 5/5；不启用外部门控的 Admin EFCore `net6.0` 回归 8 通过、3 跳过，FreeSQL 9 通过、3 跳过。两只临时容器已停止并自动移除，原有 Docker 容器未改动。`git diff --check` 无空白错误。尚未在 SDK `8.0.424` 或外部部署环境重跑。

## Review 修复记录：FIX-029 至 FIX-032

本轮采用 `recommended` 范围。`.agents/scripts/task-state.mjs review-fix` 尝试写入 `.agents/runtime/current-task.json` 时收到 `EPERM`，因此任务状态脚本未完成；本节和前三行由执行器直接维护，未修改 Reviewer 的 `review.md`。

### FIX-029：直接同步事务

- 两套 Admin 工作单元重写 `SaveChanges()`：无待执行事务动作时走原有同步路径；有延迟消息动作时同步等待现有 CAP 事务流程。`Commit()` 由继承的虚方法分派进入该覆盖。
- 两套真实 MySQL/CAP 夹具各增加同步成功和重复主键失败用例，按业务记录与 CAP 出箱共同提交/回滚断言。脚本验收门槛同步调整为每套 5/5。
- `artifacts/admin-mysqlcap-results/net6.0-20261003T151038606Z/` 的两套 TRX 各 5/5；当前生产源码的 `net6.0-20261003T152807178Z/` 中两套各 5/5，同步用例各 2/2，并断言事务结束后当前 CAP 值恢复。

### FIX-030：事务开始失败

- FreeSQL 在取得底层事务并完成 CAP `Begin` 后才设置当前 CAP 事务；若 CAP `Begin` 抛错则恢复此前状态并尝试释放新建包装对象，释放失败时聚合两项异常。
- `BeginTransaction_WhenDatabaseBeginFails_ShouldPreserveAmbientTransaction` 通过，验证底层事务启动失败时保留原错误与既有当前事务。CAP `Begin` 自身抛错路径尚无可控真实 Provider 故障注入，不声称已直接验证该分支。

### FIX-031：清理异常与提交后失败

- 两套工作单元都通过 `TransactionCleanup.DisposeAndCapture` 释放 CAP 事务包装对象，使释放异常不会覆盖此前业务/回滚异常，并恢复进入前的 CAP 当前事务值。FreeSQL 用提交后回调标记数据库已提交，`Flush` 失败时不尝试回滚已提交事务。
- FreeSQL `CapUnitOfWorkExtensionsTest` 8/8（其中 `RollbackBoth`、启动失败、提交后 Flush 失败、释放异常与正常释放次数的直接断言）；EFCore `AdminUnitOfWorkRollbackTest` 2/2。真实 Provider 的回滚与释放同时抛错仍缺直接故障注入，属于当前剩余测试缺口。
- 中间组合轮次 `net6.0-20261003T151638527Z/`：EFCore 5/5；FreeSQL 4/5，唯一失败是出箱已持久化但消费者 30 秒内未回调；该单项在全新容器中 1/1 通过。当前源码组合轮次 `net6.0-20261003T152807178Z/` 两套均 5/5、诊断 `Passed/Test`；保留前次波动证据，不将单次成功解释为投递稳定性保证。

### FIX-032：映射与宿主闭包

- 更新 `test-mapping.md`，修正过期的“CAP 8 待外部测试”文案，增加同步事务和清理符号的测试映射，并区分旧 3/3、中间波动轮次及当前源码的两套 5/5。
- 预检脚本 5/5；不启用外部门控的 Admin FreeSQL `net6.0` 13 通过/5 跳过、Admin EFCore 8 通过/5 跳过。实际 SDK 8.0.425，锁定 8.0.424 未安装。
- 两套完整 Admin 宿主 `--no-restore` 构建未通过：EFCore 宿主的 Serilog/Exceptionless 包无法从当前离线源取得（NU1101）；FreeSQL 宿主的现有还原资产缺少 `Bing.Ddd.Application.AspectCore` 项目信息（NU1105）。这不足以证明生产代码编译失败；默认依赖图的干净还原和完整宿主构建仍待可用源验证。
- Docker 验收容器均已停止并移除。未修改包版本、目标框架或 `global.json`，未自动提交、推送或创建 PR。

### 本轮状态

- Completed：FIX-029 直接同步事务；FIX-030 已确认的底层事务开始失败状态保留；FIX-031 的原始异常汇总与提交后 Flush 状态；FIX-032 的当前映射修正。
- Open Actionable：真实 Provider 同时发生回滚与释放故障时的可控注入验证；当前只有生产清理辅助函数及其异常/调用次数直接测试。
- Blocked External：锁定 SDK 8.0.424；默认依赖图的完整 Admin 宿主构建所需包源。
- Accepted Limitation：数据库提交完成后 CAP Flush 失败无法撤回事务，需要出箱重试与业务幂等。
- Deferred：后台消费者出现过一次 30 秒等待超时，随后单项和当前完整组合轮次均通过；没有足够证据指定实现缺陷，后续单独监测投递延迟与失败状态，不靠连续提高超时掩盖。
- No-Progress Check：CHANGED；同步入口、资源清理和当前事务状态恢复均有代码及真实容器证据。
- Goal Status：RUNNING，下一步是独立 Review 当前变更及剩余故障注入边界。
