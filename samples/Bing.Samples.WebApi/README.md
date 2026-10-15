# Bing.Samples.WebApi — 最小 Web 宿主骨架

> 上级：[samples 总览](../README.md) ｜ 配套：[使用文档](../../docs/getting-started/使用文档.md) ｜ [架构文档 §8 模块系统](../../docs/architecture/架构文档.md)

## 1. 它是什么

**一个可运行的最小 Web 宿主**，用来展示 Bing 框架的模块启动链路、AspectCore 属性注入和 `ILazyServiceProvider` 延迟解析。

如果你的目标是"学习某个具体能力怎么用"，这里给不了答案；如果你的目标是"确认框架怎么被宿主起来"，看这一个文件就够了。

| 项 | 值 |
| --- | --- |
| TFM | `net6.0`（`Microsoft.NET.Sdk.Web`） |
| 框架引用 | `framework/src/Bing.AspNetCore`、`framework/src/Bing.Aop.AspectCore` |
| 三方包 | `AspectCore.Extensions.Hosting` 3.0.0 |
| 外部依赖 | **无** |
| 启动地址 | `http://localhost:5128`（见 `Properties/launchSettings.json`） |

## 2. 启动链路（5 个文件）

| 文件 | 职责 |
| --- | --- |
| `Program.cs` | `Host.CreateDefaultBuilder` → `.UseServiceContext()`（AspectCore AOP 宿主）→ `ConfigureWebHostDefaults` → `UseStartup<Startup>()` |
| `Startup.cs` | `services.EnableAutowired()`、`services.AddBing().AddModule<AppModule>()`；`app.UseBing()` |
| `Modules/AppModule.cs` | `AppModule : AspNetCoreBingModule`，`[DependsOnModule(typeof(AspNetCoreModule))]`，`Level = ModuleLevel.Application` |
| `Controllers/SampleController.cs` | 继承 `BingControllerBase`，通过 `LazyServiceProvider` 展示属性注入 |
| `appsettings.json` | 默认配置（开发期另见 `appsettings.Development.json`，已被 `.gitignore` 忽略） |

关键代码：

```csharp
// Program.cs —— AOP 宿主必须是 UseServiceContext()
Host.CreateDefaultBuilder(args)
    .UseServiceContext()
    .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());

// Startup.ConfigureServices —— AddModule 是 IBingBuilder 的扩展，不能写成 services.AddModule<T>()
services.EnableAutowired();
services.AddBing().AddModule<AppModule>();

// Startup.Configure
app.UseBing();
```

```csharp
// Modules/AppModule.cs
[Description("应用程序模块")]
[DependsOnModule(typeof(AspNetCoreModule))]
public class AppModule : AspNetCoreBingModule
{
    public override ModuleLevel Level => ModuleLevel.Application;

    public override IServiceCollection AddServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddControllersAsServices(); // 让控制器由 AspectCore 服务容器创建，执行属性注入
        return services;
    }

    public override void UseModule(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}
```

## 3. 运行

```bash
dotnet run --project samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj
```

启动后访问 `http://localhost:5128/sample/lazy`，应返回 `200`，其中 `Injected` 为 `true`、`Value` 为 `lazy-service-ready`。该请求由 `SampleController` 处理，控制器的 `ILazyServiceProvider` 由 AspectCore 适配器注入，并在请求中实际调用 `LazyGetRequiredService`。验证完成后通过宿主 `StopAsync` 停止应用，监听端口应释放。

## 4. 从它出发加东西

| 想加 | 怎么做 |
| --- | --- |
| 一个接口 | 新建 Controller 继承自 `BingControllerBase` / `ApiControllerBase`（命名空间 `Bing.AspNetCore.Mvc`），并在 `UseModule` 里映射 `app.UseEndpoints(e => e.MapControllers())` |
| 数据访问 | 在 `AppModule` 的 `[DependsOnModule]` 上加 `EntityFrameworkCoreModule`（或对应 Provider 模块），并按 [架构文档 §12](../../docs/architecture/架构文档.md) 调用 `AddXxxUnitOfWork` |
| 鉴权 | 加 `Bing.AspNetCore.Authentication.JwtBearer` 引用，`services.AddJwt(Configuration)` |
| 统一响应/异常处理 | 已由 `AspNetCoreModule` 提供（`ApiResult`、`BingExceptionHandlingMiddleware`），无需额外注册 |

## 5. 注意

- `AddModule<T>()` 是 **`IBingBuilder`** 的扩展方法，不是 `IServiceCollection` 的——写成 `services.AddModule<T>()` 会编译不过。详见 [FAQ 与排错](../../docs/getting-started/FAQ与排错.md)。
- 模块排序只按 `Level → Order → FullName`，**不是拓扑排序**；`[DependsOnModule]` 只负责"递归纳入"，不决定顺序。
- 本工程 `Nullable` / `ImplicitUsings` 均为 `enable`，与部分框架工程（netstandard2.0）不同，复制代码时注意差异。
