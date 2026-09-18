# Bing.MultiTenancy
[![NuGet](https://img.shields.io/nuget/v/Bing.MultiTenancy.svg)](https://www.nuget.org/packages/Bing.MultiTenancy/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.MultiTenancy.svg)](https://www.nuget.org/packages/Bing.MultiTenancy/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 多租户：`ICurrentTenant`、租户解析与切换。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：10
>
> **上游 Bing 包**：`Bing.Security`、`Bing.MultiTenancy.Abstractions`

---

## 这是什么

提供当前租户抽象与租户解析/切换能力。Web 侧的租户解析中间件在 `Bing.AspNetCore.MultiTenancy`。

## 安装

```bash
dotnet add package Bing.MultiTenancy
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ICurrentTenant` | `Bing.MultiTenancy` | 当前租户（随核心模块自动注册） |

## 与其它包的关系

**本包依赖**：`Bing.Security`、`Bing.MultiTenancy.Abstractions`

**依赖本包**：`Bing.AspNetCore.MultiTenancy`

## 注意事项

⚠ 框架**没有** `AddMultiTenancy` / `UseMultiTenancy` 注册入口。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.MultiTenancy

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
