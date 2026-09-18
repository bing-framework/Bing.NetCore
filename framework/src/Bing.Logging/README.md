# Bing.Logging
[![NuGet](https://img.shields.io/nuget/v/Bing.Logging.svg)](https://www.nuget.org/packages/Bing.Logging/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Logging.svg)](https://www.nuget.org/packages/Bing.Logging/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 日志抽象：`ILog<T>` / `ILogFactory` / `ILogContextAccessor`。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：25
>
> **上游 Bing 包**：`Bing.Core`

---

## 这是什么

框架自己的日志抽象，支持结构化打点（`ExtraProperty`）、作用域（`BeginScope`）、标签（`Tags`）等链式写法。`Bing.Logging.Serilog` 提供 Serilog 实现。

## 安装

```bash
dotnet add package Bing.Logging
```

## 快速上手

```csharp
services.AddBingLogging();

// 链式打点
log.ExtraProperty("UserId", userId)
   .Message("订单创建成功")
   .Tags("order")
   .LogInformation();

using (log.BeginScope(new Dictionary<string, object> { ["OperationType"] = "update" }))
{
    log.Message("带作用域的日志").LogDebug();
}
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddBingLogging(Action<BingLoggingOptions>)` | `Bing.Logging` | 注册 `ILogFactory` / `ILogContextAccessor` / `ILog<>` |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ILog<T>` | `Bing.Logging` | 日志门面 |
| `ILogFactory` / `ILogContextAccessor` | `Bing.Logging` | 日志工厂与上下文 |

## 与其它包的关系

**本包依赖**：`Bing.Core`

**依赖本包**：`Bing.AspNetCore`、`Bing.Logging.Serilog`、`Bing.Logging.Sinks.Exceptionless`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Logging

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
