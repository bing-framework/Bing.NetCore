# modules/admin — 后台管理系统（参考实现）

> 配套文档：[架构文档](../../docs/architecture/架构文档.md) ｜ [使用文档](../../docs/getting-started/使用文档.md) ｜ [子系统深挖 · CAP 事件总线](../../docs/architecture/子系统深挖.md)

`admin` 是本仓库中**唯一一个完整的分层业务实现**，用于验证框架在真实场景下的可用性：领域建模、双 ORM 落地、CAP 事件总线、缓存、鉴权、SkyAPM 链路追踪、Swagger、Hangfire 一应俱全。

> ⚠ **它不是"开箱即用的产品"**：数据库迁移与种子数据初始化**尚未接通**，**Identity 与权限注册整段被注释**，连接串为内网调试地址。请以"参考实现 / 架构样板"的预期来阅读它，详见 §5。

---

## 1. 工程清单（12 个，全部 `net6.0`）

| 层 | 工程 | 职责 |
| --- | --- | --- |
| **宿主** | `Bing.Admin` | EF Core 版 Web 宿主 |
| **宿主** | `Bing.Admin.FreeSQL` | FreeSQL 版 Web 宿主（与上一个**互斥**，见 §3） |
| 表现/应用 | `Bing.Admin.Service` | 应用服务实现 |
| 表现/应用 | `Bing.Admin.Service.Shared` | 应用服务契约与 DTO |
| 基础设施 | `Bing.Admin.Infrastructure` | 常量、配置、横切支撑 |
| 基础设施 | `Bing.Admin.EventHandlers` | 事件处理器（领域事件 + CAP 集成事件） |
| 数据 | `Bing.Admin.Data` | 工作单元抽象 `IAdminUnitOfWork`、种子数据（`Seed/`） |
| 数据 | `Bing.Admin.Data.EFCore` | EF Core 实现（含 `CapConsumerServiceSelector`） |
| 数据 | `Bing.Admin.Data.FreeSQL` | FreeSQL 实现（含 `CapConsumerServiceSelector`、`CapUnitOfWorkExtensions`） |
| 领域 | `Bing.Admin.Systems.Domain` | 系统域（用户 / 角色 / 权限 / 应用） |
| 领域 | `Bing.Admin.Commons.Domain` | 公共域（字典 / 附件等） |
| 领域 | `Bing.Admin.Domain.Shared` | 领域共享（枚举、常量、事件） |

分层依赖方向：`宿主 → Service → Domain/Infrastructure → Data.Abstractions`；`Data.EFCore` 与 `Data.FreeSQL` 都实现 `Bing.Admin.Data` 的抽象，**二者可替换**。

## 2. 模块装配（`Bing.Admin/Startup.cs`）

```csharp
services.AddBing()
    .AddModule<LogModule>()
    .AddModule<MapperModule>()
    .AddModule<AppModule>()
    .AddModule<AuthenticationModule>()
    .AddModule<EntityFrameworkCoreModule>()
    .AddModule<CacheModule>()
    .AddModule<CapModule>()
    .AddModule<SkyApmModule>()
    //.AddModule<MySqlAdminUnitOfWorkMigrationModule>()   // ⚠ 已注释，见 §5
    .AddModule<SwaggerModule>()
    .AddModule<HangfireModule>();

app.UseBing();
```

| 模块 | 作用 |
| --- | --- |
| `LogModule` | 日志（Serilog / Exceptionless） |
| `MapperModule` | AutoMapper 映射配置 |
| `AuthenticationModule` | JWT 鉴权（⚠ 内部 `services.AddScoped<IdentitySignInManager>()` 被注释，见 §5） |
| `EntityFrameworkCoreModule` | EF Core 工作单元（FreeSQL 宿主里换成对应的 FreeSQL 模块） |
| `CacheModule` | 缓存（依赖 Redis） |
| `CapModule` | CAP 事件总线（依赖 RabbitMQ + MySQL 出箱表；⚠ `CapConsumerServiceSelector` 注册被注释） |
| `SkyApmModule` | SkyWalking 链路追踪（配置见 `skyapm.json`） |
| `SwaggerModule` | 接口文档 |
| `HangfireModule` | 后台任务 |

## 3. ⚠ 两个宿主互斥

`Bing.Admin` 与 `Bing.Admin.FreeSQL` **不能同时引用**。原因是两套 Data 实现使用了**完全相同的命名空间与类名**：

```
Bing.Admin.Data.UnitOfWorks.MySql.AdminUnitOfWork     // 两个工程各有一份
```

同时引用会造成类型冲突（或解析到错误的实现）。选择方式：

| 你需要的 ORM | 启动工程 |
| --- | --- |
| EF Core | `modules/admin/src/Bing.Admin/Bing.Admin.csproj` |
| FreeSQL | `modules/admin/src/Bing.Admin.FreeSQL/Bing.Admin.FreeSQL.csproj` |

```bash
dotnet run --project modules/admin/src/Bing.Admin/Bing.Admin.csproj
```

## 4. 外部依赖

| 依赖 | 用途 | 配置位置 |
| --- | --- | --- |
| MySQL | 主库 + CAP 出箱表（Outbox） | `appsettings.json` |
| Redis | 缓存（`CacheModule`） | `appsettings.json` |
| RabbitMQ | CAP 消息传输 | **硬编码**在 `Modules/CapModule.cs` |

> ⚠ **安全提示**：上述依赖的连接串与账号密码写在**受版本控制**的 `appsettings.json` 与 `CapModule.cs` 中（`.gitignore` 只忽略了 `appsettings.Development.json`）。这些是内网调试遗留值，**本地运行前请改成你自己的环境配置**，切勿把生产凭据提交进仓库。

## 5. ⚠ 迁移、种子与 Identity 注册尚未接通

这是当前最影响"能否直接跑起来"的一点，请务必知悉。**三块代码被整段注释**：

| 位置 | 状态 | 后果 |
| --- | --- | --- |
| `Modules/MySqlAdminUnitOfWorkMigrationModule.cs`（两个宿主各一份，各 46 行） | **整个文件被注释**，且 `Startup.cs` 中对应的 `AddModule<>` 也是注释状态 | 不建表 |
| `Bing.Admin.Data/Seed/*SeedDataInitializer.cs`（`Application` / `Role` / `Administrator`） | 类已实现，但注册代码在注释掉的迁移模块里，**不会被注册** | 不写种子数据 |
| `Bing.Admin.Service/Extensions/Extensions.Service.cs`（**42 行 100% 注释**） | 文件整体被注释 | **`AddPermission` 不存在**；`AddIdentity` / `AddUserStore` / `AddRoleStore` / `IdentityUserManager` / `IdentitySignInManager` / `IdentityErrorChineseDescriber` **均未注册** |
| `Bing.Admin/Modules/EntityFrameworkCoreModule.cs:42` | 单行注释 | 只读工作单元 `IAdminReadonlyUnitOfWork` 未注册 |
| EF Migrations 目录 | 不存在 | 无迁移脚本 |

**结论**：启动后数据库表结构不会自动创建、种子数据不会写入、Identity 与权限服务不会注册。要真正跑通，至少需要：

1. 取消 `MySqlAdminUnitOfWorkMigrationModule` 的注释（含 `Startup.cs` / `FreeSqlModule.cs` 里的 `AddModule`）；
2. 为 `AdminUnitOfWork` 补 EF Core Migrations（或改用 FreeSQL 的 `CodeFirst` 同步结构）；
3. 确认 `Seed` 里三个 `ISeedDataInitializer` 的注册生效；
4. 恢复 `Bing.Admin.Service/Extensions/Extensions.Service.cs` 的 `AddPermission`，打通 Identity 与权限。

> 全仓共有 **25 处被注释的注册调用**（含 `//services.AddAudit()`、`//services.AddSqlQuery<…>`、`//services.AddSqlExecutor()`、`//services.AddNLog()`），逐处清单见 [功能完成度与已知缺口 §6](../../docs/architecture/功能完成度.md)。
>
> 这些注释**是有意临时停用还是已经废弃**，文档无法判断——需要维护者确认。

## 6. CAP 事件总线在这里怎么落地的

### 6.1 出箱（Outbox）

`CapModule.AddServices` 里：

```csharp
services.AddCapEventBus(o =>
{
    o.UseEntityFramework<AdminUnitOfWork>();   // ← 出箱表落在 MySQL（与工作单元同库同事务）
    o.UseDashboard();
    o.SucceedMessageExpiredAfter = 24 * 3600;  // 成功消息保留 24 小时
    o.FailedRetryCount = 5;
    o.Version = "admin";
    o.UseRabbitMQ(x => { /* 见 §4 安全提示 */ });
    o.ConsumerThreadCount = 1;
    o.UseDispatchingPerGroup = true;
});
```

即：**业务写入与消息写入在同一事务**，由 CAP 异步投递到 RabbitMQ。详见 [子系统深挖](../../docs/architecture/子系统深挖.md)。

### 6.2 订阅端三件套

```csharp
public class TestMessageEventHandler : MessageEventHandlerBase, ITestMessageEventHandler
{
    [EventHandler(MessageEventConst.TestMessage1)]                 // ← 框架特性，继承自 CAP 的 TopicAttribute
    public async Task TestMessage1Async(TestMessage message, [FromCap] CapHeader header)
    {
        // ...
        await MessageEventBus.PublishAsync(new TestMessageEvent2(message, message.Send));
        if (message.NeedCommit)
            await UnitOfWork.CommitAsync();
    }
}
```

1. 实现 `ICapSubscribe`（或继承 `MessageEventHandlerBase`）；
2. 方法上标 **`[EventHandler(...)]`**（*不是* CAP 原生的 `[CapSubscribe]`）；
3. 在 `CapModule.LoadEvent` 里显式 `services.AddTransient<IXxxEventHandler, XxxEventHandler>()`。

### 6.3 TraceId 跨服务传递

`CapModule` 里用自定义 `CapSubscribeInvoker : SubscribeInvoker` 替换了 CAP 的实现，从消息头 `bing-trace-id` 取回 TraceId 并写入 `TraceIdContext.Current`，使消费端日志与生产端串到同一条链路。

## 7. 接口调试

Postman 集合：`modules/admin/docs/postman/Bing.Admin.postman_collection.json`

启动后另有：

| 入口 | 地址 |
| --- | --- |
| Swagger | `/swagger`（端口见 `Properties/launchSettings.json`） |
| CAP Dashboard | `/cap`（`o.UseDashboard()`） |
| Hangfire Dashboard | `/hangfire` |

## 8. 相关文档

- 分层与模块系统：[架构文档 §8](../../docs/architecture/架构文档.md)
- 注册入口速查：[架构文档 §12](../../docs/architecture/架构文档.md) / [使用文档 §3.3](../../docs/getting-started/使用文档.md)
- **本参考实现的缺口清单**：[功能完成度与已知缺口](../../docs/architecture/功能完成度.md)
- 常见坑：[FAQ 与排错](../../docs/getting-started/FAQ与排错.md)
- 更简单的入门样例：[samples/](../../samples/README.md)
