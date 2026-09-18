# Bing.Localization
[![NuGet](https://img.shields.io/nuget/v/Bing.Localization.svg)](https://www.nuget.org/packages/Bing.Localization/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Localization.svg)](https://www.nuget.org/packages/Bing.Localization/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 本地化实现：JSON 文件资源与自定义存储两种模式。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：14
>
> **上游 Bing 包**：`Bing.Localization.Abstractions`
>
> **三方依赖**：`Microsoft.Extensions.Caching.Memory`

---

## 这是什么

`AddJsonLocalization()` 从 `*.{culture}.json` 读取资源；`AddStoreLocalization<TStore>()` 让你从数据库 / 缓存等自定义来源读取。

## 安装

```bash
dotnet add package Bing.Localization
```

## 快速上手

```csharp
services.AddJsonLocalization(resourcesPath: "Resources");
// 或
services.AddStoreLocalization<MyLocalizationStore>();
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddJsonLocalization()` / `(resourcesPath)` / `(Action<JsonLocalizationOptions>)` | `Bing.Localization` | JSON 文件本地化 |
| `AddStoreLocalization<TStore>(...)` | `Bing.Localization` | 自定义存储型本地化 |

## 与其它包的关系

**本包依赖**：`Bing.Localization.Abstractions`

**三方 NuGet**：`Microsoft.Extensions.Caching.Memory`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Localization

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
