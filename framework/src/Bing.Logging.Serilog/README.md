# Bing.Logging.Serilog
[![NuGet](https://img.shields.io/nuget/v/Bing.Logging.Serilog.svg)](https://www.nuget.org/packages/Bing.Logging.Serilog/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Logging.Serilog.svg)](https://www.nuget.org/packages/Bing.Logging.Serilog/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> Serilog 日志实现与配置绑定。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：10
>
> **上游 Bing 包**：`Bing.Logging`
>
> **三方依赖**：`Serilog`、`Serilog.Extensions.Logging`、`Serilog.Settings.Configuration`

---

## 这是什么

把 Serilog 接到 `Bing.Logging` 抽象上，并提供 `ConfigLogLevel(configuration)` 把 `Logging:LogLevel` 配置节映射到 Serilog 最低级别。

## 安装

```bash
dotnet add package Bing.Logging.Serilog
```

## 快速上手

```csharp
services.AddBingLogging();
services.AddLogging(b => b.AddSerilog());

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(configuration)
    .ConfigLogLevel(configuration)
    .CreateLogger();
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `ConfigLogLevel(IConfiguration)` | `Bing.Logging.Serilog` | 绑定 `Logging:LogLevel` 到 Serilog 级别 |
| `UseBingSerilogEnrichers(app)` | `Bing.AspNetCore.Serilog` | Web 侧注入租户/用户/关联 ID（须在 `UseAuthentication()` 之后） |

## 与其它包的关系

**本包依赖**：`Bing.Logging`

**三方 NuGet**：`Serilog`、`Serilog.Extensions.Logging`、`Serilog.Settings.Configuration`

## 注意事项

没有 `AddSerilog` / `AddBingSerilog` 入口。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Logging.Serilog

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
