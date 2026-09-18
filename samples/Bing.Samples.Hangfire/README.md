# Bing.Samples.Hangfire — 后台任务 + 日志 + DI 打通

> 上级：[samples 总览](../README.md) ｜ 配套：[架构文档 §8 模块系统](../../docs/architecture/架构文档.md) ｜ [组件库 · Exceptionless](../../docs/guides/组件库文档.md)

## 1. 它演示什么

这是三个样例里**信息密度最高**的一个，核心演示三件事：

1. **三模块分层**：`AppModule`（Application）/ `LogModule`（Framework）/ `HangfireModule`（Framework），展示 `ModuleLevel` 的实际用法。
2. **Hangfire 与框架 DI 打通**：用自定义 `JobActivator` 把作业解析交给容器（优先 AspectCore，退化 MSDI），并让 TraceId 跨任务边界传递。
3. **日志双通道**：`Bing.Logging` 的 `ILog<T>` 与 Serilog + Exceptionless Sink 同时工作。

| 项 | 值 |
| --- | --- |
| TFM | `net6.0`（`Microsoft.NET.Sdk.Web`，`InProcess` 托管） |
| 框架引用 | `Bing.AspNetCore`、`Bing.Logging.Serilog`、`Bing.Logging.Sinks.Exceptionless` |
| 三方包 | `Hangfire.AspNetCore` 1.7.16、`Hangfire.MemoryStorage` 1.7.0、`Serilog.Enrichers.Span` 2.0.1、`AspectCore.Extensions.Hosting` 2.2.0 |
| 存储 | **MemoryStorage**（进程内，重启即丢，无需 Redis/SQL Server） |
| 启动地址 | `https://localhost:5001` / `http://localhost:5000` |
| 面板 | `/hangfire` |

## 2. 三个模块

```csharp
// Startup.ConfigureServices
services.AddBing()
    .AddModule<AppModule>()
    .AddModule<LogModule>()
    .AddModule<HangfireModule>();
```

| 模块 | 级别 | 做了什么 |
| --- | --- | --- |
| `AppModule` | `ModuleLevel.Application` | 开 AOP（`EnableAop`，排除 `Bing.Swashbuckle` / `DotNetCore.CAP` 命名空间）；`UseModule` 里注册 `CodePagesEncodingProvider`、`UseAuthentication()` |
| `LogModule` | `ModuleLevel.Framework` | `AddExceptionless(...)` + `AddBingLogging(...)`；用 Serilog 组装 `Enrich.FromLogContext / WithLogLevel / WithSpan`，`WriteTo.Exceptionless(...)` 把 `TraceId` 写成 tag |
| `HangfireModule` | `ModuleLevel.Framework` | `AddHangfire` 用 MemoryStorage 并挂 `CorrelateFilterAttribute`；`UseModule` 里挂 Dashboard、Server，注册周期任务 |

## 3. 两个桥接实现（本样例的精华）

### 3.1 `HangfireDIActivator : JobActivator`

Hangfire 默认自己 `Activator.CreateInstance`，拿不到容器里的服务。这里接管创建逻辑：

```csharp
GlobalConfiguration.Configuration.UseActivator(new HangfireDIActivator(app.ApplicationServices));

public override object ActivateJob(Type jobType) => _serviceResolver == null
    ? _serviceProvider.GetService(jobType)      // 退化：MSDI
    : _serviceResolver.Resolve(jobType);        // 优先：AspectCore
```

作用域同样分两套：`AspectCoreScope`（`IServiceResolver.CreateScope()`）与 `MsdiScope`（`IServiceProvider.CreateScope()`）。**判断依据是 `IServiceProvider is IServiceResolver`**——即是否走了 `UseServiceContext()`。

### 3.2 `CorrelateFilterAttribute : JobFilterAttribute, IClientFilter, IServerFilter`

把 `TraceIdContext.Current.TraceId` 写进 Job 参数，执行时再取回来：

| 阶段 | 行为 |
| --- | --- |
| `OnCreating` | `TraceIdContext.Current ??= new TraceIdContext(string.Empty)`，写入 Job 参数 `CorrelationId` |
| `OnPerforming` | 读回 `CorrelationId`（读不到就用 `BackgroundJob.Id`），重建 `TraceIdContext.Current` |

这样后台任务的日志就能和触发它的请求串到同一个 TraceId 上。

## 4. 任务与日志写法

周期任务注册（`HangfireModule.UseModule`）：

```csharp
RecurringJob.AddOrUpdate<IDebugLogJob>(x => x.WriteLog(), "0/5 5 * * * ? ", TimeZoneInfo.Local);
```

> cron 是 **7 位**（含秒）。Hangfire 用 `Hangfire.Core` 的 cron 解析器，写法与 Quartz 一致。

`DebugLogJob`（`IDebugLogJob : IScopedDependency`，靠**约定式 DI** 自动注册，无需手工 `AddScoped`）演示了 4 种日志写法：

```csharp
// 1) 作用域内打点：BeginScope 的键值会进入日志上下文
using (CurrentLog.BeginScope(new Dictionary<string, object> { ["UserId"] = "svrooij", ["OperationType"] = "update" }))
    CurrentLog.Message($"...").LogDebug();

// 2) 附加属性 + 标签
CurrentLog.ExtraProperty("test", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.sss"))
          .Message($"...").Tags(id).LogInformation();

// 3) 两者叠加（ExtraProperty 支持匿名对象与 JSON 串）
CurrentLog.ExtraProperty("Req", new { A = 1, B = "2" })
          .ExtraProperty("ReqJson", JsonHelper.ToJson(new { A = 1, B = "2" }))
          .Message($"...").Tags(id).LogWarning();

// 4) 微软原生 ILogger<T> 仍可用（SysLogger.LogTrace/LogDebug/LogInformation/LogWarning）
```

## 5. 运行

```bash
dotnet run --project samples/Bing.Samples.Hangfire/Bing.Samples.Hangfire.csproj
```

打开 `https://localhost:5001/hangfire` 看面板，任务每 5 秒级触发一次（按 cron 语义）。

## 6. ⚠ 注意事项

- **硬编码的 Exceptionless 地址与 ApiKey**：`Modules/LogModule.cs` 里 `o.ServerUrl = "http://10.186.135.27:5100"`，ApiKey 也在源码中。这是内网调试遗留，**本地运行前请改成你自己的服务/密钥**，否则日志提交会失败（进程仍可启动）。
- **MemoryStorage 只适合演示**：重启进程后任务历史与队列全部丢失，生产请换 SQL Server / Redis 存储并相应调整 `AddHangfire` 配置。
- **日志可能重复**：源码注释明确写着"同时输出 2 种方式的日志，可能存在重复 需要陆续兼容"——`AddExceptionless` 与 Serilog 的 `WriteTo.Exceptionless` 会各写一份。
- **Hangfire Dashboard 未加鉴权**：样例直接 `app.UseHangfireDashboard()`，任何人可访问；生产必须加 `DashboardOptions` 的 `Authorization`。
