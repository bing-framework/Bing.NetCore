<!-- AI_EXECUTION_STATUS: PARTIAL -->
AI_TASK_ID: BING-ASPECTCORE-3-UPGRADE-20260930
AI_EXECUTION_STARTED_AT: 2026-09-30T00:00:00+08:00
AI_EXECUTION_FINISHED_AT: 2026-09-30T00:00:00+08:00

# 实施执行报告

## 执行结论

AspectCore 已升级到正式版 `3.0.0`。受影响的 Bing 工程已迁移到 `net6.0`，中立基础契约继续保持 `netstandard2.0`。属性注入、模块插件解析和 Admin 两套工作单元的真实 AspectCore 容器链路均已完成本地验证。

执行状态为 `PARTIAL`：仓库锁定的 SDK `8.0.424` 未安装，使用 `8.0.425` 完成验证；MySQL/CAP 外部分布式事务环境未提供，因此不能把外部事务联动标记为通过。除此之外，计划中的本地代码、回归测试、Sample 冒烟和迁移文档已完成。

## 任务信息

- 计划：`ai_docs/tasks/BING-ASPECTCORE-3-UPGRADE-20260930/plan.md`
- 执行器：Codex
- 目标版本：AspectCore `3.0.0`
- 锁定 SDK：`8.0.424`；当前实际 SDK：`8.0.425`

## 变更影响分析

- ChangedFiles：7 个确认的生产 TFM 工程、版本属性/测试引用、插件程序集解析器、Admin 测试、Sample 和迁移文档。
- ChangedProjects：`Bing.Aop.AspectCore`、DDD Application/Domain、MVC Contracts、FreeSQL、FreeSQL.MySql；由于直接依赖已迁移 Domain，`components/src/Bing.Biz/project.props` 一并迁移到 `net6.0`。
- ChangedPublicContracts：Bing 的 `EnableAop`、`EnableAutowired`、`UseServiceContext`、`AutowiredAttribute` 和 `ILazyServiceProvider` 用法保持不变。
- ChangedRuntimePaths：AspectCore 属性注入、工作单元拦截、插件程序集候选解析、宿主停止和资源释放。
- ChangedProviders：EFCore、FreeSQL、AspectCore 服务上下文。
- ChangedTFMs：7 个计划工程加上直接消费 `Bing.Ddd.Domain` 的 `Bing.Biz` 改为 `net6.0`；`Bing.Core` 与 `Bing.Validation` 仍为 `netstandard2.0`。
- ChangedBuildPackaging：生产、测试、探针、Admin、WebApi 和 Hangfire 的 AspectCore 直接依赖统一为 `3.0.0`。
- RiskLevel：HIGH（包版本与目标框架变化）。

## 基线与工具限制

- 工作区原有大量未提交改动，未执行 reset、clean、提交或推送。
- AspectCore 2.4 基线：AOP 测试 `net8.0`/`net6.0` 各 64 项通过。
- 任务状态脚本无法写入 `.agents/runtime/current-task.json` 临时文件，返回 Windows `EPERM`；本报告按执行器要求手工维护状态，未反复重试或绕过该限制。
- 收口时 `task-finish.mjs` 因运行时任务文件不存在而无法登记完成；不影响源码和测试结果，状态以本报告头部为准。

## 计划执行情况

- [x] T1 冻结基线并升级目标框架/依赖。还原图中的 AspectCore 直接和传递版本均为 `3.0.0`；目标工程构建通过。
- [x] T2 验证并修复 Bing AspectCore 适配层。插件加载器仅允许 Microsoft DI Abstractions 请求版本不高于宿主已加载版本，其他候选程序集仍严格检查完整身份；避免把 Bing.Core 类型身份放宽。
- [x] T3 验证 Admin EFCore/FreeSQL modules 工作单元链路。新增两套独立 `net6.0` 测试，使用 `UseServiceContext`、`EnableAutowired`、`EnableAop`，验证继承工作单元的 Lazy 解析、作用域隔离及 `SaveChangesAsync` 基础路径。
- [x] T4 验证资源、Sample、文档和交付证据。本地资源/生命周期回归沿用现有测试集；WebApi 请求和停止信号完成冒烟；新增 AspectCore 迁移文档和包索引说明。

## 测试结果

### 升级后通过

- `Bing.Core.Tests`：`net8.0` 610/610，`net6.0` 610/610。
- `Bing.Aop.AspectCore.Tests`：`net8.0` 64/64，`net6.0` 64/64。
- `Bing.AspNetCore.Mvc.Tests`：`net8.0` 58 通过、1 项既有跳过；`net6.0` 58 通过、1 项既有跳过。
- `Bing.Uow.Tests`：`net8.0`/`net6.0` 各 10/10。
- `Bing.FreeSQL.Tests`：`net8.0`/`net6.0` 各 33/33。
- `Bing.EntityFrameworkCore.Tests`：`net6.0` 61/61。
- Admin EFCore AspectCore 兼容性测试：1/1。
- Admin FreeSQL AspectCore 兼容性测试：1/1。

### 构建与 Sample

- DDD Application（包含 Domain、Application Contracts）、MVC Contracts、Events、FreeSQL、FreeSQL.MySql 构建成功。
- Admin EFCore、Admin FreeSQL 宿主构建成功。
- WebApi、Hangfire Sample 构建成功。
- WebApi Sample 启动后 `GET http://127.0.0.1:5128/sample/lazy` 返回 `200`，响应包含 `"injected":true` 和 `Bing.DependencyInjection.LazyServiceProvider`；停止信号触发 `Application is shutting down`。
- `dotnet list package --include-transitive` 验证 AOP、WebApi、Hangfire 和 Admin 宿主中的 AspectCore 全部为 `3.0.0`。

### 警告与未验证项

- 当前 SDK 是 `8.0.425`，没有 `8.0.424`，因此锁定 SDK 验收仍为 `NOT_VERIFIED_ENVIRONMENT`。
- Admin 的 net6 构建出现 Microsoft.Extensions/System.Diagnostics 10.0.0 不支持 net6 的既有传递依赖警告；没有因本次升级顺带升级这些无关依赖。
- MySQL/CAP 实际分布式事务、外部数据库提交/回滚和生产基础设施停止流程没有环境，状态为 `BLOCKED_EXTERNAL`。
- 本地新测试覆盖作用域 Lazy 链路和无事务 `SaveChangesAsync`；CAP 事务分支、嵌套提交和外部异常回滚仍需专用数据库/CAP 环境验证。
- 默认加载上下文中的程序集不可卸载；这不是本次升级的回收验收条件。

## 关键实现说明

AspectCore 3.0 没有要求重写 Bing 属性注入器或 `UseServiceContext` 适配层。升级后唯一的生产行为修复是插件候选程序集解析：插件使用不高于宿主版本的 Microsoft DI Abstractions 时可绑定到宿主契约，Bing.Core 等框架契约仍要求身份一致。该规则由 `ReloadAsync_CopiedHostContracts_ShouldShareContractIdentity` 和 Core 全量回归覆盖。

为保持依赖闭包可构建，`Bing.Biz` 随直接引用的 `Bing.Ddd.Domain` 从 `netstandard2.0` 迁移到 `net6.0`；这属于升级引起的必要扩展，不是无关业务重构。

## 生产符号到测试方法映射

- `BingPropertyInjectorFactory`、`EnableAutowired`、`EnableAop`：`AutowiredContainerBehaviorTest.Resolve_ShouldInjectAutowiredPropertyButNotField`、`EnableAutowired_ShouldRespectCustomPropertyInjectorFactory`、`ResolveInDifferentScopes_ShouldUseDifferentScopedPropertyServices`。
- `BingPluginLoader.PluginAssemblyResolver.Resolve`：`HotPluginHostTest.ReloadAsync_CopiedHostContracts_ShouldShareContractIdentity`，并由 Core 两个目标框架全量回归覆盖。
- `Bing.Aop.AspectCore` 的模块/宿主释放：`AspectCoreBingModuleLifecycleTest.HostStopAndDispose_ShouldShutdownModuleAndDisposeInjectedServiceOnce`。
- Web 宿主初始化与停止：`WebModuleLifecycleTest.HostStop_ShouldAwaitAsyncShutdownOverride`、`PluginWebLifecycleTest.HostStop_ShouldShutdownWebPlugin`。
- Admin EFCore/FreeSQL 的 `AdminUnitOfWork.LazyServiceProvider` 与 `SaveChangesAsync`：两套测试工程中的 `AdminUnitOfWorkAspectCoreTest.Resolve_ShouldInjectScopedLazyServices`。

## 交付文件

- 迁移说明：`docs/migrations/aspectcore-3.0.md`
- 迁移索引：`docs/migrations/README.md`
- AOP 包说明：`framework/src/Bing.Aop.AspectCore/README.md`
- Admin EFCore 测试：`modules/admin/tests/Bing.Admin.EFCore.Tests`
- Admin FreeSQL 测试：`modules/admin/tests/Bing.Admin.FreeSQL.Tests`

## 交付约束

未自动 git commit、git push 或创建 PR。外部限制不通过改代码伪造为已验证。
