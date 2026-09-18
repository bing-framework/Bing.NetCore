# Bing.MiniProfiler
[![NuGet](https://img.shields.io/nuget/v/Bing.MiniProfiler.svg)](https://www.nuget.org/packages/Bing.MiniProfiler/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.MiniProfiler.svg)](https://www.nuget.org/packages/Bing.MiniProfiler/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> MiniProfiler 性能监控集成（含 `MiniProfilerModule`）。
>
> **目标框架**：`net6.0` ｜ **分层**：横切 / 可观测性 ｜ **当前版本**：7.0.0 ｜ **源文件**：2
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

把 MiniProfiler 接入框架模块体系，可采样 SQL 与请求耗时。

## 安装

```bash
dotnet add package Bing.MiniProfiler
```

## 快速上手

```csharp
services.AddBing().AddModule<MiniProfilerModule>();
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `MiniProfilerModule` | `Bing.MiniProfiler` | 框架内置的 5 个模块类之一 |

## 与其它包的关系

## 注意事项

框架内置模块类只有 5 个：`BingCoreModule`、`DependencyModule`、`AspNetCoreModule` / `AspNetCoreBingModule`、`MiniProfilerModule`。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.MiniProfiler

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
