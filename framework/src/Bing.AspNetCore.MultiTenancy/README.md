# Bing.AspNetCore.MultiTenancy
[![NuGet](https://img.shields.io/nuget/v/Bing.AspNetCore.MultiTenancy.svg)](https://www.nuget.org/packages/Bing.AspNetCore.MultiTenancy/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AspNetCore.MultiTenancy.svg)](https://www.nuget.org/packages/Bing.AspNetCore.MultiTenancy/) ![TFM](https://img.shields.io/badge/TFM-net6.0-blue.svg)

> 多租户中间件与租户解析贡献者链。
>
> **目标框架**：`net6.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：12
>
> **上游 Bing 包**：`Bing.AspNetCore`、`Bing.MultiTenancy`
>
> **三方依赖**：`Bing.Utils.Text`

---

## 这是什么

在 ASP.NET Core 管道中解析当前租户（`Bing.MultiTenancy` 的 Web 侧落地）。

## 安装

```bash
dotnet add package Bing.AspNetCore.MultiTenancy
```

## 与其它包的关系

**本包依赖**：`Bing.AspNetCore`、`Bing.MultiTenancy`

**三方 NuGet**：`Bing.Utils.Text`

## 注意事项

⚠ 框架**没有** `AddMultiTenancy` / `UseMultiTenancy` 注册入口。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AspNetCore.MultiTenancy

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
