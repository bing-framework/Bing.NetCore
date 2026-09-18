# Bing.Security
[![NuGet](https://img.shields.io/nuget/v/Bing.Security.svg)](https://www.nuget.org/packages/Bing.Security/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Security.svg)](https://www.nuget.org/packages/Bing.Security/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 安全基件：当前用户 `ICurrentUser`、身份与权限基础类型。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：33
>
> **上游 Bing 包**：`Bing.Core`

---

## 这是什么

提供"当前登录用户"的抽象 `ICurrentUser`，以及身份/权限相关的基础类型。`Bing.Permissions` 在此基础上实现权限模块。

## 安装

```bash
dotnet add package Bing.Security
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `ICurrentUser` | `Bing.Security.Users` | 当前用户（随核心模块自动注册，直接注入即可） |

## 与其它包的关系

**本包依赖**：`Bing.Core`

**依赖本包**：`Bing.AspNetCore`、`Bing.MultiTenancy`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Security

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
