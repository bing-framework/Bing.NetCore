# samples — 示例与最小宿主骨架

> 配套文档：[使用文档](../docs/getting-started/使用文档.md) ｜ [架构文档](../docs/architecture/架构文档.md) ｜ [FAQ 与排错](../docs/getting-started/FAQ与排错.md)

本目录包含 3 个**最小可运行工程**，用途是演示「Bing 框架怎么被宿主起来」，**不是**业务最佳实践示例。它们的共同套路只有两行：

```csharp
// Startup.ConfigureServices
services.AddBing().AddModule<AppModule>();   // 注意：是 IBingBuilder，不是 IServiceCollection 原生方法

// Startup.Configure / Program
app.UseBing();                                // Web；非 Web 用 serviceProvider.UseBing()
```

---

## 1. 三个样例总览

| 工程 | TFM | 演示点 | 注册的模块 | 外部依赖 | 在 `Bing.All.sln` 中 |
| --- | --- | --- | --- | --- | --- |
| [`Bing.Samples.WebApi`](Bing.Samples.WebApi/README.md) | `net6.0` | **最小 Web 宿主骨架**：模块系统 + AOP 宿主 + `UseBing` 管道 | `AppModule`（依赖 `AspNetCoreModule`） | 无 | ✅ |
| [`Bing.Samples.Hangfire`](Bing.Samples.Hangfire/README.md) | `net6.0` | 后台任务：Hangfire 与框架 DI 打通、TraceId 跨任务传递、Serilog + Exceptionless 日志 | `AppModule` / `LogModule` / `HangfireModule` | ⚠ Exceptionless 服务（默认指向内网地址） | ✅ |
| [`Bing.Samples.Winform`](Bing.Samples.Winform/README.md) | `net6.0-windows` | 非 Web 宿主：WinForms 里手工搭 `ServiceCollection` + `AddBing()` | **无**（只调 `AddBing()`，未注册任何模块） | 无 | ❌ **未纳入解决方案** |

---

## 2. 运行方式

统一模板（在**仓库根目录**执行）：

```bash
dotnet run --project samples/<工程名>/<工程名>.csproj
```

| 工程 | 命令 | 启动后访问 |
| --- | --- | --- |
| Bing.Samples.WebApi | `dotnet run --project samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj` | `http://localhost:5128` |
| Bing.Samples.Hangfire | `dotnet run --project samples/Bing.Samples.Hangfire/Bing.Samples.Hangfire.csproj` | `https://localhost:5001`（HTTP `:5000`），Hangfire 面板 `/hangfire` |
| Bing.Samples.Winform | `dotnet run --project samples/Bing.Samples.Winform/Bing.Samples.Winform.csproj` | Windows 桌面窗口（需 Windows + `win-x64`） |

构建 SDK 由根目录 `global.json` 固定为 **8.0.424**，样例本身目标框架是 `net6.0`——请先 `dotnet --version` 确认。

---

## 3. 我应该看哪一个？

| 你的目的 | 建议 |
| --- | --- |
| 第一次跑通框架，看最小启动路径 | **WebApi**（零依赖，改一行就能看到 `AddBing` + `UseBing` 全链路） |
| 想知道模块怎么拆、模块里能干什么 | **Hangfire**（三个模块、两种 `ModuleLevel`，有真实的 `UseModule` 管道装配） |
| 想在控制台 / 桌面 / 非 Web 进程里用框架 | **Winform**（`AddBing()` 后自行 `BuildServiceProvider()`，注意它**没有**调用 `UseBing()`） |
| 想看完整的分层业务实现 | 不要看 samples，看 [`modules/admin`](../modules/admin/README.md) |

---

## 4. 已知注意事项

- **samples 不是 API 演示**：`Bing.Samples.WebApi` 虽然调了 `services.AddControllers()`，但目录里**没有任何 Controller**，启动后只有框架默认的中间件在跑。
- **Hangfire 样例硬编码了 Exceptionless 服务地址**（`http://10.186.135.27:5100`）与 ApiKey，属内网调试遗留；本地运行前请先改成你自己的服务，否则日志写入会失败（不影响进程启动）。
- **Winform 样例不在 `Bing.All.sln` 里**，不会被整体构建覆盖，改框架后请单独编译验证。
- 样例引用的都是 `framework/src/` 下的**工程引用**，不是 NuGet 包——因此版本号以仓库 `version.props` 为准。
