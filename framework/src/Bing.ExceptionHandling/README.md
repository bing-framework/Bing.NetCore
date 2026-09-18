# Bing.ExceptionHandling
[![NuGet](https://img.shields.io/nuget/v/Bing.ExceptionHandling.svg)](https://www.nuget.org/packages/Bing.ExceptionHandling/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.ExceptionHandling.svg)](https://www.nuget.org/packages/Bing.ExceptionHandling/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 异常处理：异常 → 错误信封的转换契约。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：9
>
> **上游 Bing 包**：`Bing.Core`

---

## 这是什么

定义 `IExceptionToErrorInfoConverter` 与 `RemoteServiceErrorInfo`，把框架异常统一转成可序列化的错误信封，供 Web 层中间件消费。

## 安装

```bash
dotnet add package Bing.ExceptionHandling
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `UseBingExceptionHandling()` | `Microsoft.AspNetCore.Builder`（`Bing.AspNetCore`） | Web 端异常处理中间件（幂等） |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IExceptionToErrorInfoConverter` | `Bing.AspNetCore.ExceptionHandling` | 异常 → 错误信封转换 |
| `RemoteServiceErrorInfo` | `Bing.Http` | 错误信封模型 |

## 与其它包的关系

**本包依赖**：`Bing.Core`

**依赖本包**：`Bing.AspNetCore`

## 注意事项

没有 `AddExceptionHandling`——只有 `UseBingExceptionHandling()` 中间件。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.ExceptionHandling

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
