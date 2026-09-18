# Bing.EasyCaching
[![NuGet](https://img.shields.io/nuget/v/Bing.EasyCaching.svg)](https://www.nuget.org/packages/Bing.EasyCaching/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.EasyCaching.svg)](https://www.nuget.org/packages/Bing.EasyCaching/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> ⚠ 实验性：EasyCaching 适配（当前入口缺失，不建议依赖）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：8
>
> **上游 Bing 包**：`Bing.Caching`
>
> **三方依赖**：`EasyCaching.Core`、`EasyCaching.Bus.Redis`、`EasyCaching.HybridCache`、`EasyCaching.InMemory`、`EasyCaching.Redis`、`EasyCaching.Serialization.SystemTextJson`

---

## 这是什么

⚠ 实验性：EasyCaching 适配（当前入口缺失，不建议依赖）。

## 安装

```bash
dotnet add package Bing.EasyCaching
```

## 与其它包的关系

**本包依赖**：`Bing.Caching`

**三方 NuGet**：`EasyCaching.Core`、`EasyCaching.Bus.Redis`、`EasyCaching.HybridCache`、`EasyCaching.InMemory`、`EasyCaching.Redis`、`EasyCaching.Serialization.SystemTextJson`

## 注意事项

⚠⚠ **当前不可用**：`AddCaching` 扩展整段被注释，`CachingOptions` 还是 `internal` 类。需要缓存请用 `Bing.Caching.CSRedis`。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.EasyCaching

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
