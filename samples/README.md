# samples — 示例与最小宿主骨架

> 配套文档：[使用文档](../docs/getting-started/使用文档.md) ｜ [架构文档](../docs/architecture/架构文档.md) ｜ [FAQ 与排错](../docs/getting-started/FAQ与排错.md)

本目录包含最小宿主骨架和分层业务样例，分别演示框架宿主、后台任务和多数据源工作单元。WebApi/Hangfire 样例使用模块宿主；SqlUnitOfWork 样例直接注册 SQL 服务并运行 HTTP、Worker 和 Hangfire 业务入口。

```csharp
// Startup.ConfigureServices
services.AddBing().AddModule<AppModule>();   // 注意：是 IBingBuilder，不是 IServiceCollection 原生方法

// Startup.Configure / Program
app.UseBing();                                // Web；非 Web 用 serviceProvider.UseBing()
```

---

## 1. 样例总览

| 工程 | TFM | 演示点 | 注册的模块 | 外部依赖 | 在 `Bing.All.sln` 中 |
| --- | --- | --- | --- | --- | --- |
| [`Bing.Samples.WebApi`](Bing.Samples.WebApi/README.md) | `net6.0` | **最小 Web 宿主骨架**：模块系统 + AOP 宿主 + `UseBing` 管道 | `AppModule`（依赖 `AspNetCoreModule`） | 无 | ✅ |
| [`Bing.Samples.Hangfire`](Bing.Samples.Hangfire/README.md) | `net6.0` | 后台任务：Hangfire 与框架 DI 打通、TraceId 跨任务传递、Serilog + Exceptionless 日志 | `AppModule` / `LogModule` / `HangfireModule` | 可选 Exceptionless 服务（通过配置注入） | ✅ |
| [`Bing.Samples.SqlUnitOfWork`](Bing.Samples.SqlUnitOfWork/README.md) | `net6.0` | Dapper / EF Shared 多数据源 Uow；HTTP、BackgroundService、Hangfire Server；两个 SQLite 文件 | 原生 Host 服务注册 | 本地 SQLite 与 Hangfire MemoryStorage | ✅ |
| `Bing.Samples.SqlUnitOfWork.Consumer` | `net6.0` | 每个 MySQL 数据源独立 CAP runtime；跨进程消费、持久幂等和重试 | 原生 Generic Host | 任务专属 MySQL 与 RabbitMQ Compose | ✅ |
| [`Bing.Samples.Winform`](Bing.Samples.Winform/README.md) | `net6.0-windows` | 非 Web 宿主：WinForms 里手工搭 `ServiceCollection` + `AddBing()` | **无**（只调 `AddBing()`，未注册任何模块） | 无 | ❌ **未纳入解决方案** |

`Bing.Samples.WebApi.Tests` 验证 WebApi 属性注入与端口释放；`Bing.Samples.SqlUnitOfWork.Tests` 在 net6.0/net8.0 上验证双库事务、EF bridge、后台 worker 和 Hangfire Server。

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
| Bing.Samples.SqlUnitOfWork | `dotnet run --project samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj` | `http://localhost:5000`；`EfBridge` 用 `-- --SqlUow:Storage=EfBridge` |
| Bing.Samples.Winform | `dotnet run --project samples/Bing.Samples.Winform/Bing.Samples.Winform.csproj` | Windows 桌面窗口（需 Windows + `win-x64`） |

构建 SDK 由根目录 `global.json` 固定为 **8.0.424**，样例本身目标框架是 `net6.0`——请先 `dotnet --version` 确认。

---

## 3. 我应该看哪一个？

| 你的目的 | 建议 |
| --- | --- |
| 第一次跑通框架，看最小启动路径 | **WebApi**（零依赖，改一行就能看到 `AddBing` + `UseBing` 全链路） |
| 想知道模块怎么拆、模块里能干什么 | **Hangfire**（三个模块、两种 `ModuleLevel`，有真实的 `UseModule` 管道装配） |
| 想在控制台 / 桌面 / 非 Web 进程里用框架 | **Winform**（`AddBing()` 后自行 `BuildServiceProvider()`，注意它**没有**调用 `UseBing()`） |
| 想看多数据源事务、EF/Dapper 协作与后台执行 | **SqlUnitOfWork**（真实双库、HTTP、Worker、Hangfire） |
| 想看完整领域业务模块 | [`modules/admin`](../modules/admin/README.md) |

---

## 4. 已知注意事项

- **WebApi 是最小宿主示例**：`Bing.Samples.WebApi` 提供 `SampleController` 验证 `ILazyServiceProvider` 属性注入；多数据源事务链路见 `Bing.Samples.SqlUnitOfWork`。
- **Hangfire 样例的 Exceptionless 为可选配置**：通过 `Exceptionless:ApiKey`、`Exceptionless:ServerUrl` 或对应环境变量注入；空 API Key 时只使用本地日志，不会连接远程服务。
- **Winform 样例不在 `Bing.All.sln` 里**，不会被整体构建覆盖，改框架后请单独编译验证。
- 样例引用的都是 `framework/src/` 下的**工程引用**，不是 NuGet 包——因此版本号以仓库 `version.props` 为准。
