# Bing.Logging.Sinks.Exceptionless
[![NuGet](https://img.shields.io/nuget/v/Bing.Logging.Sinks.Exceptionless.svg)](https://www.nuget.org/packages/Bing.Logging.Sinks.Exceptionless/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Logging.Sinks.Exceptionless.svg)](https://www.nuget.org/packages/Bing.Logging.Sinks.Exceptionless/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> Exceptionless 日志接收器（Serilog Sink）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：5
>
> **上游 Bing 包**：`Bing.Logging`

---

## 这是什么

把日志送进 Exceptionless。入口**不在 `IServiceCollection` 上**，而是 Serilog 的 `WriteTo.Exceptionless(...)`，且扩展类的命名空间是 **`Serilog`**（不是 `Bing.*`）。

## 安装

```bash
dotnet add package Bing.Logging.Sinks.Exceptionless
```

## 快速上手

```csharp
using Serilog;   // ← 注意：扩展方法在 Serilog 命名空间下

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Exceptionless(additionalOperation: builder =>
    {
        builder.Target.AddTags(TraceIdContext.Current?.TraceId ?? string.Empty);
        return builder;
    })
    .ReadFrom.Configuration(configuration)
    .CreateLogger();
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `WriteTo.Exceptionless(...)` | **`Serilog`** | Serilog Sink，挂在 `LoggerSinkConfiguration` 上 |

## 与其它包的关系

**本包依赖**：`Bing.Logging`

## 注意事项

⚠ 找不到 `services.AddExceptionless` 是正常的——它本来就不在 `IServiceCollection` 上。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Logging.Sinks.Exceptionless

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
