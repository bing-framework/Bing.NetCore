# Bing.Samples.WebApi — 最小 Web 宿主骨架

> 上级：[samples 总览](../README.md) ｜ 配套：[使用文档](../../docs/getting-started/使用文档.md) ｜ [架构文档 §8 模块系统](../../docs/architecture/架构文档.md)

## 1. 它是什么

**一个"能跑起来的空壳"**，用来展示 Bing 框架的最小启动链路。它只引用 `Bing.AspNetCore` 一个框架工程，零外部中间件、零数据库、零 Controller。

如果你的目标是"学习某个具体能力怎么用"，这里给不了答案；如果你的目标是"确认框架怎么被宿主起来"，看这一个文件就够了。

| 项 | 值 |
| --- | --- |
| TFM | `net6.0`（`Microsoft.NET.Sdk.Web`） |
| 框架引用 | `framework/src/Bing.AspNetCore` |
| 三方包 | `AspectCore.Extensions.Hosting` 2.4.0 |
| 外部依赖 | **无** |
| 启动地址 | `http://localhost:5128`（见 `Properties/launchSettings.json`） |

## 2. 启动链路（4 个文件）

| 文件 | 职责 |
| --- | --- |
| `Program.cs` | `Host.CreateDefaultBuilder` → `.UseServiceContext()`（AspectCore AOP 宿主）→ `ConfigureWebHostDefaults` → `UseStartup<Startup>()` |
| `Startup.cs` | `services.AddBing().AddModule<AppModule>()`；`app.UseBing()` |
| `Modules/AppModule.cs` | `AppModule : AspNetCoreBingModule`，`[DependsOnModule(typeof(AspNetCoreModule))]`，`Level = ModuleLevel.Application` |
| `appsettings.json` | 默认配置（开发期另见 `appsettings.Development.json`，已被 `.gitignore` 忽略） |

关键代码：

```csharp
// Program.cs —— AOP 宿主必须是 UseServiceContext()
Host.CreateDefaultBuilder(args)
    .UseServiceContext()
    .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());

// Startup.ConfigureServices —— AddModule 是 IBingBuilder 的扩展，不能写成 services.AddModule<T>()
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
        services.AddControllers();     // 注册了 MVC，但本工程没有 Controller
        return services;
    }

    public override void UseModule(IApplicationBuilder app)
    {
        app.UseRouting();              // 只挂了路由，未挂 UseEndpoints / 鉴权
    }
}
```

## 3. 运行

```bash
dotnet run --project samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj
```

启动后访问 `http://localhost:5128`。因为没有任何 Controller 与终结点，请求会穿过框架中间件后返回 404——**这是预期行为**，本样例的价值在于进程能起来、模块能加载、管道能装配。

## 4. 从它出发加东西

| 想加 | 怎么做 |
| --- | --- |
| 一个接口 | 新建 Controller 继承自 `BingControllerBase` / `ApiControllerBase`（命名空间 `Bing.AspNetCore.Mvc`），并在 `UseModule` 里补 `app.UseEndpoints(e => e.MapControllers())` |
| 数据访问 | 在 `AppModule` 的 `[DependsOnModule]` 上加 `EntityFrameworkCoreModule`（或对应 Provider 模块），并按 [架构文档 §12](../../docs/architecture/架构文档.md) 调用 `AddXxxUnitOfWork` |
| 鉴权 | 加 `Bing.AspNetCore.Authentication.JwtBearer` 引用，`services.AddJwt(Configuration)` |
| 统一响应/异常处理 | 已由 `AspNetCoreModule` 提供（`ApiResult`、`BingExceptionHandlingMiddleware`），无需额外注册 |

## 5. 注意

- `AddModule<T>()` 是 **`IBingBuilder`** 的扩展方法，不是 `IServiceCollection` 的——写成 `services.AddModule<T>()` 会编译不过。详见 [FAQ 与排错](../../docs/getting-started/FAQ与排错.md)。
- 模块排序只按 `Level → Order → FullName`，**不是拓扑排序**；`[DependsOnModule]` 只负责"递归纳入"，不决定顺序。
- 本工程 `Nullable` / `ImplicitUsings` 均为 `enable`，与部分框架工程（netstandard2.0）不同，复制代码时注意差异。
