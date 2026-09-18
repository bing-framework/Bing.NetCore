# 从 ABP 迁移 / 对照表

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../ReleaseNotes.md)

> **一句话定位**：如果你用过 ABP Framework（Volo.Abp 系），本文帮你把已有知识映射到 Bing.NetCore——哪些同名、哪些换名、哪些没有。
>
> **关于准确性**：**Bing 侧的每一个类型名都取自本仓库源码**；ABP 侧基于 ABP Framework（vNext / Volo.Abp）的通用 API 认知。**ABP 版本迭代较快，若与你所使用版本不符，请以 ABP 官方文档为准**并回来修正本文。

---

## 1. 先看结论：三件最容易卡住的事

| # | 你会以为 | 实际是 |
| --- | --- | --- |
| 1 | `[DependsOnModule]` 像 ABP 的 `[DependsOn]` 一样能排顺序 | **不能**。它只保证加载，顺序由 `ModuleLevel` 决定 |
| 2 | 仓储接口叫 `IRepository` | 叫 **`IStore`**。`s/IRepository/IStore/` 是迁移里最机械也最重要的一步 |
| 3 | 事件总线是 `ILocalEventBus` / `IDistributedEventBus` | Bing 里**确实有这两个接口**，但在**遗留包** `Bing.EventBus*`；现行的是 `Bing.Core` 的 **`ISimpleEventBus`** / **`IMessageEventBus`** |

> 第 3 条尤其危险：你 `using Bing.EventBus;` 会编译通过（类型确实存在），但那是无人维护的遗留实现。**新代码一律用 `Bing.Core` 的那套。**

---

## 2. 概念对照总表

图例：🟢 同名同义｜🔵 换名｜🟡 同名但语义/用法有差异｜🔴 Bing 侧无对应

### 2.1 模块与启动

| ABP | Bing | 状态 | 说明 |
| --- | --- | --- | --- |
| `AbpModule` | `BingModule` / `IBingModule` | 🔵 | 都继承抽象基类并重写 `ConfigureServices` |
| `[DependsOn]` | `[DependsOnModule]` | 🔵 | **语义不同**：ABP 是依赖图 + 拓扑排序；Bing 只做**递归包含** |
| `AbpApplicationFactory.CreateAsync` | `services.AddBing().AddModule<T>()` | 🔵 | Bing 的入口挂在 **`IBingBuilder`** 上，不在 `IServiceCollection` 上 |
| `app.InitializeApplicationAsync()` | `app.UseBing()`（Web）/ `provider.UseBing()`（非 Web） | 🔵 | 非 Web 宿主**别忘了调** |
| `ServiceConfigurationContext` | `BingModule.ConfigureServices` 自带上下文 | 🟡 | 参数类型不同，照抄 ABP 代码会编译失败 |

### 2.2 领域与数据访问

| ABP | Bing | 状态 | 说明 |
| --- | --- | --- | --- |
| `IRepository<TEntity, TKey>` | **`IStore<TEntity, TKey>`** | 🔵 | **最大差异**。Bing 没有 `IRepository` |
| `EfCoreRepository<...>` | `StoreBase<TEntity, TKey>` | 🔵 | 另有 `CompactRepositoryBase`、`TreeRepositoryBase` |
| 只读仓储 | `IQueryStore<TEntity, TKey>` | 🔵 | `IStore : IQueryStore`，只读路径建议注入父接口 |
| `IUnitOfWorkManager` | `IUnitOfWorkManager` | 🟢 | 同名，同为 `IScopedDependency` |
| `IUnitOfWork` | `IUnitOfWork` | 🟢 | 同名 |
| `[UnitOfWork]` | `[UnitOfWork]` | 🟡 | **同名但实现不同**：Bing 的是 AOP 拦截器（`UnitOfWorkAttribute : InterceptorBase, IScopeInterceptor`），**必须启用 `EnableAop(...)` 才生效** |
| `IDataFilter<TFilter>` | `IDataFilter` | 🟡 | 概念一致（临时关闭全局过滤器）；泛型形态不同 |

### 2.3 审计与并发

| ABP | Bing | 状态 | 说明 |
| --- | --- | --- | --- |
| `IHasCreationTime` / `ICreationAudited` | `ICreationAuditedObject` | 🔵 | 命名体系不同但语义相同 |
| `IHasModificationTime` / `IModificationAudited` | `IModificationAuditedObject` | 🔵 | 同上 |
| `IAuditedObject` | `IAuditedObject` / `IAuditedObject<TKey>` | 🟢 | 同名 |
| `ISoftDelete` / `IHasDeletionTime` | `ISoftDelete` | 🟢 | 同名 |
| `IHasConcurrencyStamp` | `IVersion` | 🔵 | ⚠ **类型不同**：ABP 的 `ConcurrencyStamp` 是 `string`；Bing 的 `IVersion.Version` 是**字节数组**（EF 支写 GUID 字节） |

### 2.4 事件与消息

| ABP | Bing | 状态 | 说明 |
| --- | --- | --- | --- |
| `ILocalEventBus` | **`ISimpleEventBus`**（`Bing.Core`） | 🔵 | ⚠ 不要用 `Bing.EventBus` 里的 `ILocalEventBus`（遗留） |
| `IDistributedEventBus` | **`IMessageEventBus`**（`Bing.Core`） | 🔵 | 底层走 CAP + Outbox |
| `[EventName]` / `IDistributedEventHandler` | `ICapSubscribe` + `[EventHandler]` | 🔵 | ⚠ 用 `[EventHandler]`，**不是** `[CapSubscribe]` |
| Outbox | Outbox | 🟢 | 概念一致：消息与业务数据同库同事务 |

### 2.5 横切服务

| ABP | Bing | 状态 | 说明 |
| --- | --- | --- | --- |
| `ICurrentUser` | `ICurrentUser` | 🟢 | 同名（`Bing.Security`） |
| `ICurrentTenant` | `ICurrentTenant` | 🟢 | 同名（`Bing.MultiTenancy.Abstractions`） |
| `IClock` | `IClock` | 🟢 | 同名（`Bing.Core/Bing/Timing`） |
| `IAsyncQueryableExecuter` | `IAsyncQueryableExecuter` | 🟢 | 同名同义（`Bing.Core/Bing/Linq`），连 `IAsyncQueryableProvider` 都同名 |
| `IObjectMapper` | `IObjectMapper` | 🟢 | 同名 |
| `IStringLocalizer` / 本地化资源 | `Bing.Localization` | 🔵 | 有对应包，API 形态需按 Bing 文档确认 |
| `IApplicationService` | `IAppService` | 🔵 | Bing 的应用服务契约家族见下 |
| `ICrudAppService<...>` | `ICrudAppService<...>` | 🟡 | 同名，但**泛型参数不同**：Bing 额外区分 `TCreateRequest` / `TUpdateRequest` / `TQueryParameter` |
| 查询型应用服务 | `IQueryAppService<TDto, TQueryParameter>` | 🔵 | Bing 独有分层；另有 `ITreesQueryAppService`（树形） |
| `IGuidGenerator` | — | 🔴 | 无对应，自行生成 |
| `ISettingProvider` / 设置系统 | — | 🔴 | 无内置设置管理 |
| `IBackgroundJobManager` / 后台任务 | — | 🔴 | 无内置；参考 `samples/Bing.Samples.Hangfire` 自行接入 Hangfire |
| `PermissionDefinitionProvider` / `IPermissionChecker` | `Bing.Permissions` | 🟡 | 有权限包（`ISignInManager<TUser,TKey>`、`UserBase<TUser,TKey>`），但**模型与 ABP 不同**，需重写权限定义 |

---

## 3. 迁移步骤清单

按此顺序推进，每步都可独立验证：

- [ ] **1. 换仓储**：`IRepository<T,K>` → `IStore<T,K>`；`IRepository<T>` → `IStore<T>`（默认 `Guid` 主键）
- [ ] **2. 换模块**：`AbpModule` → `BingModule`；`[DependsOn]` → `[DependsOnModule]`；然后**逐个检查初始化顺序**——ABP 靠依赖图，Bing 必须显式设 `ModuleLevel`
- [ ] **3. 换注册入口**：`services.AddApplicationAsync<T>()` → `services.AddBing().AddModule<T>()`；补 `UseBing()`
- [ ] **4. 换事件总线**：`ILocalEventBus` → `ISimpleEventBus`；`IDistributedEventBus` → `IMessageEventBus`；订阅特性改为 `[EventHandler]`
- [ ] **5. 换审计/并发**：`IHasConcurrencyStamp` → `IVersion`（注意类型从 `string` 变字节数组，数据库列要一并调整）
- [ ] **6. 确认 AOP**：用了 `[UnitOfWork]` 的话，必须调 `EnableAop(...)`（**入口是 `EnableAop` 不是 `AddAop`**）
- [ ] **7. 补缺口**：设置管理、后台任务、Guid 生成器若 ABP 里用了，Bing 侧需自行实现或替换
- [ ] **8. 选型复核**：ABP 默认 EF Core；Bing 有三支 ORM，按 [能力矩阵](能力矩阵.md) 重新确认一遍（尤其是用了 Bulk、跨库查询、或 Oracle/Sqlite 的场景）

---

## 4. 什么情况下不建议迁移

| 情况 | 原因 |
| --- | --- |
| 重度依赖 ABP 的**设置系统 / 后台任务 / 权限定义体系** | Bing 侧无对应或模型差异大，重写成本高于收益 |
| 依赖 ABP 的商业模块（Account / Identity Server / SaaS） | Bing 没有等价物 |
| 项目已在 ABP 上稳定运行 | 换框架是高风险动作，除非有明确收益 |

---

## 5. 反过来：ABP 用户会觉得 Bing 更好的点

| 点 | 说明 |
| --- | --- |
| **三支 ORM 可选** | EF Core 之外还有 FreeSQL（`netstandard2.0`）与 Bing.Data.Sql（批量 / 跨库 / 存储过程），ABP 主要绑 EF Core |
| **批量写入与跨库查询** | `InsertBatch` / `UpdateBatch` / 跨库校验器是 Bing.Data.Sql 的核心能力 |
| **`Bing.Biz.Payments`** | 内置支付宝 / 微信支付封装（`AddPay` 系列），ABP 无 |
| **`Bing.Biz.OAuthLogin`** | 内置十余家第三方登录 Provider |
| **更轻** | 无 ABP 那样的强约定（应用服务自动 API 暴露、自动审计日志等），想要更多手动控制时更顺手 |

---

## 更多文档

- 术语辨析：[术语表](术语表.md)（含 `IStore` vs `IRepository`、事件总线双份问题）
- 选型：[能力矩阵](能力矩阵.md)
- 上手：[使用文档](使用文档.md)
- 正确写法：[最佳实践](../guides/最佳实践.md)
- 踩坑：[FAQ 与排错](FAQ与排错.md)
