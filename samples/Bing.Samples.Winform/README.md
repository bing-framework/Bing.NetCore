# Bing.Samples.Winform — 非 Web 宿主（WinForms）

> 上级：[samples 总览](../README.md) ｜ 配套：[架构文档 §8 模块系统](../../docs/architecture/架构文档.md) ｜ [使用文档](../../docs/getting-started/使用文档.md)

## 1. 它演示什么

**在完全没有 ASP.NET Core 管道的进程里使用 Bing 框架**：手工搭 `ServiceCollection` → `AddBing()` → `BuildServiceProvider()` → 从容器里取 `Form1` 并 `Application.Run()`。

| 项 | 值 |
| --- | --- |
| TFM | `net6.0-windows`（`OutputType=WinExe`，`UseWindowsForms=true`） |
| 发布方式 | `PublishSingleFile=true`、`SelfContained=true`、`RuntimeIdentifier=win-x64` |
| 框架引用 | `framework/src/Bing.Core`（**只引这一个**） |
| 三方包 | `Microsoft.Extensions.Configuration.Json` 6.0.0、`Microsoft.Extensions.DependencyInjection` 6.0.1、`Microsoft.Extensions.Logging` 6.0.0 |
| 注册的模块 | **无** |
| 在 `Bing.All.sln` 中 | ❌ **未纳入**（不会被整体构建覆盖） |

## 2. 启动链路

```csharp
// Program.Main
ApplicationConfiguration.Initialize();
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);   // 解决 GBK 等编码
var serviceProvider = GetServiceProvider();
using var scope = serviceProvider.CreateScope();
Application.Run(scope.ServiceProvider.GetService<Form1>());      // 窗体也从容器取

static IServiceProvider GetServiceProvider()
{
    var services = new ServiceCollection();
    var configuration = InitConfiguration(services);
    services.AddSingleton(configuration);
    AddServices(services, configuration);   // 预留：目前是空的
    services.AddBing();                     // ← 框架入口（非 Web 形态）
    return services.BuildServiceProvider();
}
```

配置：`ConfigurationBuilder` + `SetBasePath(AppContext.BaseDirectory)` + `AddJsonFile("appsettings.json", optional: true)`。

## 3. ⚠ 三个必须知道的坑

### 3.1 只调了 `AddBing()`，没有调 `UseBing()`

Web 形态用 `app.UseBing()`；非 Web 形态应当用 **`serviceProvider.UseBing()`**（见 [架构文档 §8](../../docs/architecture/架构文档.md)）。本样例**两者都没调**，因此模块的第二阶段（`UseModule` 管道装配）不会执行。

- 当前没有注册任何模块，所以不影响运行；
- **一旦你往里加模块且模块依赖 `UseModule`，必须补上 `serviceProvider.UseBing()`**，否则会出现"服务注册了但没生效"的诡异现象。

### 3.2 没有任何模块被注册

`services.AddBing()` 后面没有 `.AddModule<T>()`，`AddServices(...)` 方法体也是空的。所以：

- 约定式 DI（`DependencyModule` 扫描程序集）**不会**被触发；
- `IScopedDependency` 等标记接口不会自动注册；
- `Form1` 能被解析出来是因为 WinForms 类型由 `ApplicationConfiguration.Initialize()` 侧注册，或走了容器的兜底创建——不要把它当成"约定式 DI 生效"的证据。

### 3.3 不在解决方案里

`Bing.All.sln` 中**没有**这个工程（只有 `Bing.Samples.WebApi` 与 `Bing.Samples.Hangfire`）。改了 `Bing.Core` 后，整体构建不会帮你发现这里的编译问题，需要单独编译验证。

## 4. 运行

Windows + `win-x64` 环境：

```bash
dotnet run --project samples/Bing.Samples.Winform/Bing.Samples.Winform.csproj
```

会弹出一个空白 `Form1`——`Form1.cs` 只有构造函数 + `InitializeComponent()`，是纯空壳，本样例的价值在 `Program.cs` 的宿主搭建方式。

## 5. 想往里加东西

| 想加 | 怎么做 |
| --- | --- |
| 用上约定式 DI | `services.AddBing().AddModule<YourModule>()`，并在 `BuildServiceProvider()` 之后调用 `serviceProvider.UseBing()` |
| 用配置文件 | 在 `AddServices(services, configuration)` 里写 `services.Configure<XxxOptions>(configuration.GetSection("Xxx"))` |
| 用日志 | 加 `Bing.Logging`（或 `Bing.Logging.Serilog`）工程引用，再 `services.AddBingLogging(...)` |
| 数据访问 | 加对应的 EF Core / FreeSQL / Dapper Provider 工程引用，并按 [架构文档 §12](../../docs/architecture/架构文档.md) 的入口注册 |
