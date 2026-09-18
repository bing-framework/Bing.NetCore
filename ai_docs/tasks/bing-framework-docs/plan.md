# Plan: Bing.NetCore 框架文档生成（使用文档 + 设计思路文档）

- **taskId**: `bing-framework-docs`
- **模式**: 先计划，再实现（本文件为计划；实现阶段在计划确认后执行）
- **产出路径**: `docs/使用文档.md`、`docs/设计思路文档.md`
- **约束**: 仅新增文档，**不修改业务源码/测试/配置/数据库/构建文件**；文件统一 UTF-8

---

## 1. 背景与需求

需求（用户原话）：阅读当前项目架构代码，生成两类文档——

1. **使用文档**：安装配置、核心模块、主要 API、典型使用示例；面向后续使用者，帮助快速上手。
2. **设计思路文档**：整体架构设计、模块划分、关键决策及其权衡、数据流与扩展方式。

要求：结构清晰、覆盖主要功能点、与实际代码保持一致。

## 2. 现状 / 完成度判断

- `README.md` 仅有 NuGet 包列表与过时的开发环境说明（SDK v2.2），**无**使用或设计文档。
- `docs/` 下为 SQL 引擎、测试、迁移、AI workflow 类文档，**无**框架级使用/设计文档。
- `modules/admin` 是参考实现（后台管理系统），`samples/` 含最小可用示例，可作为真实用法来源。
- 结论：两类文档均**待新增**，属于纯新增内容，无 Breaking Change，风险低。

## 3. 信息来源（已读取的真实源码，作为文档依据）

| 层 | 已探查工程（真实路径） |
| --- | --- |
| 核心/模块系统/约定DI | `framework/src/Bing.Core`（Modularity、DependencyInjection、Reflection、Exceptions、Options） |
| Web 集成 | `framework/src/Bing.AspNetCore`（`AspNetCoreBingModule`、`ApiResult`、`ResultHandlerAttribute`、`BingControllerBase`、`ApiControllerBase`）、`framework/src/Bing.AspNetCore.Mvc`（`QueryControllerBase`、`CrudControllerBase`） |
| DDD | `framework/src/Bing.Ddd.Domain`（Entity/AggregateRoot/ValueObject、`Bing/Domain/Repositories/IRepository.cs`、`Bing/Data/IStore.cs`、`IQueryStore`）、`Bing.Ddd.Application`、`Bing.Ddd.Application.Contracts` |
| 数据抽象/SQL | `framework/src/Bing.Data`（`Query`、`QueryParameter`、`Pager`、`ICondition`）、`framework/src/Bing.Data.Sql`（`ISqlQuery`/流利 API）、`framework/src/Bing.Uow` |
| ORM 实现 | `framework/src/Bing.EntityFrameworkCore*`（`Extensions.Service.cs` 的 `AddSqlServerUnitOfWork` 等）、`framework/src/Bing.FreeSQL*`、`framework/src/Bing.Dapper.*` |
| 横切 | `framework/src/Bing.Caching*`、`Bing.Logging*`、`Bing.EventBus*`、`Bing.Events`、`Bing.Emailing`、`Bing.MailKit`、`Bing.TextTemplating*`、`Bing.Locks.CSRedis`、`Bing.AutoMapper`、`Bing.Localization`、`Bing.Validation`、`Bing.Auditing`、`Bing.ExceptionHandling`、`Bing.Security`、`Bing.Permissions`、`Bing.MultiTenancy`、`Bing.AspNetCore.MultiTenancy`、`Bing.AspNetCore.Authentication.JwtBearer` |
| 参考实现 | `modules/admin/src/Bing.Admin`（Modules、Apis、Service、Data.EFCore）、`samples/Bing.Samples.WebApi` |

**已核对的关键事实（避免臆造）**：
- 统一响应 = `ApiResult : JsonResult`（`Bing.AspNetCore.Mvc` 命名空间，属性 `Code/Message/Data/OperationTime`）+ `ResultHandlerAttribute`（`Bing.AspNetCore.Mvc.Filters`）；`StatusCode`：`Ok=1`、`Fail=2`、`Unauthorized=401`。**不存在** `UnifyResult`/`IResult`/`[Result]`。
- `IRepository<TEntity,TKey> : IRepository, IQueryRepository<TEntity,TKey>, IStore<TEntity,TKey>`，含 `IUnitOfWork GetUnitOfWork()`（定义于 `Bing.Ddd.Domain`，非 `Bing.Data`）。
- EF Core 运行时命名空间为 `Bing.Datas.EntityFramework.Core`；注册用 `AddSqlServerUnitOfWork<TService,TImplementation>(connection, ...)` 等，无 `AddEntityFrameworkCore<TDbContext>`。
- JWT：`services.AddJwt(configuration)`（命名空间 `Bing.AspNetCore.Authorization.JwtBearer`），配置节 `JwtOptions`，授权策略名 `"jwt"`。
- 缓存无 `AddCache`，按约定 `RedisHelper.Initialization(new CSRedisClient(cs))` + `AddScoped<ICache, CSRedisCacheManager>()`。
- `global.json` 固定 SDK 8.0.424；类库 `netstandard2.0`，Web/应用层 `net6.0`。

## 4. 计划阶段

### Phase 1 — 调研（规划前已完成，作为依据，不重复执行）
- 已通过多路并行探查覆盖上述工程，提取真实类型/命名空间/签名。

### Phase 2 — 生成使用文档（Task D1）
- **目标**：产出 `docs/使用文档.md`，覆盖安装配置、核心模块、主要 API、典型示例。
- **修改范围**：仅新增文件。
- **实施步骤**：
  1. 概述 + 技术栈（SDK/目标框架/ORM 版本表）。
  2. 环境要求与 NuGet 安装（逐能力包对照表）。
  3. 快速开始（最小 Web API：`AddBing().AddModule<AppModule>()` + `app.UseBing()`）。
  4. 核心概念：模块化系统、约定式 DI、统一响应、异常处理、当前用户/多租户。
  5. DDD 分层：实体/聚合根/仓储/工作单元/查询对象/应用服务 + 完整 CRUD 示例（两种写法）。
  6. 数据访问：EF Core / FreeSQL / Dapper+Bing.Data.Sql 注册与用法、选型对照。
  7. Web 与认证：控制器基类、JWT、多租户、AOP。
  8. 横切能力：缓存/日志/事件/邮件/模板/锁/映射/本地化/验证/审计（逐条给出真实扩展方法）。
  9. 配置参考表、进阶提示。
- **依赖**：Phase 1 调研结果。
- **验证**：类型名/扩展方法均与源码一致；示例代码可对照 `samples/Bing.Samples.WebApi` 与 `modules/admin`。
- **风险**：低（仅新增文档）。
- **验收标准**：文档存在且结构完整，所有类型名可在 `framework/src` 检索到。

### Phase 3 — 生成设计思路文档（Task D2）
- **目标**：产出 `docs/设计思路文档.md`，覆盖整体架构、模块划分、关键决策权衡、数据流、扩展方式。
- **修改范围**：仅新增文件。
- **实施步骤**：
  1. 设计目标与定位。
  2. 整体架构分层图 + 模块依赖图（ASCII）。
  3. 模块划分表（按 `framework/src` 工程分组）。
  4. 关键设计决策与权衡（≥10 项：模块化启动、约定 DI、多 ORM 并存、统一响应、UoW+AOP、多租户链、约定注册、异常体系、双目标框架、Roslyn 源生成器）。
  5. 数据流：写/读/事件/异常四条链路。
  6. 扩展方式：自定义模块、仓储/UoW、结果工厂、异常转换、本地化/模板/缓存提供器、新增数据库方言、AOP。
  7. 命名约定、与 ABP 对比、上手建议。
- **依赖**：Phase 1 调研结果。
- **验证**：架构图与模块职责与真实工程一一对应；决策项有源码证据（如 `BingModule`、`DependencyModule`、`AddSqlServerUnitOfWork`、`MultiTenancyMiddleware`）。
- **风险**：低。
- **验收标准**：文档存在且覆盖需求列出的全部要点（架构/划分/决策权衡/数据流/扩展）。

### Phase 4 — 一致性校验（Task V1）
- **目标**：确保两文档与实际代码一致。
- **实施步骤**：
  1. 抽查 `ApiResult`/`StatusCode`/`ResultHandlerAttribute` 属性与枚举值。
  2. 抽查 `IRepository` 接口与 `AddSqlServerUnitOfWork` 签名。
  3. 抽查 `ICurrentUser.GetTenantCode()`、JWT 配置节名。
  4. 修正任何不一致表述。
- **依赖**：D1、D2。
- **验收标准**：抽查项全部命中源码；无虚构类型/方法。

## 5. 关键决策（文档内容层面的取舍）

- 文档语言：中文（项目与用户均为中文环境，遵循 AGENTS.md）。
- 放置位置：`docs/` 根（与现有 `docs/ReleaseNotes.md`、`docs/sqlquery-usage.md` 同级），不污染源码目录。
- 强调“与代码一致”：对仓库中易误解点（统一响应并非 `UnifyResult`、仓储接口在 `Bing.Ddd.Domain`、EF 运行时命名空间 `Bing.Datas.EntityFramework.Core`、缓存无 `AddCache`）在文档中明确纠偏。

## 6. 验收标准（整体）

- `docs/使用文档.md` 与 `docs/设计思路文档.md` 均存在、非空、结构清晰。
- 覆盖需求中列出的全部功能点。
- 文中所有类型名/命名空间/扩展方法均可在 `framework/src` 检索到对应定义。
- 未修改任何业务代码、测试、配置、数据库、构建文件。

## 7. 风险

- 极低：纯文档新增。唯一风险为类型名笔误 → 由 Phase 4 校验消除。
