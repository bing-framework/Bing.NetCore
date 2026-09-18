# Bing.Permissions
[![NuGet](https://img.shields.io/nuget/v/Bing.Permissions.svg)](https://www.nuget.org/packages/Bing.Permissions/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Permissions.svg)](https://www.nuget.org/packages/Bing.Permissions/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> 权限模块（反向依赖 `Bing.AspNetCore`，集成 HTTP/授权）。
>
> **目标框架**：`net6.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：31
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

在 `Bing.Security` 基础类型之上实现权限体系，并与 ASP.NET Core 的授权集成。

## 安装

```bash
dotnet add package Bing.Permissions
```

## 与其它包的关系

## 注意事项

⚠ 框架**没有** `AddPermissions` 注册入口。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Permissions

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
