# Bing.AspNetCore.Abstractions
[![NuGet](https://img.shields.io/nuget/v/Bing.AspNetCore.Abstractions.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Abstractions/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AspNetCore.Abstractions.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Abstractions/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0%3Bnetstandard2.1%3Bnet6.0-blue.svg)

> ASP.NET Core 抽象（被 JwtBearer 等工程引用）。
>
> **目标框架**：`netstandard2.0;netstandard2.1;net6.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：3
>
> **上游 Bing 包**：`Bing.Core`

---

## 这是什么

把 Web 层契约单独成包，供 `Bing.AspNetCore.Authentication.JwtBearer` 等工程引用，避免循环依赖。

## 安装

```bash
dotnet add package Bing.AspNetCore.Abstractions
```

## 与其它包的关系

**本包依赖**：`Bing.Core`

**依赖本包**：`Bing.AspNetCore`、`Bing.AspNetCore.Authentication.JwtBearer`

## 注意事项

多目标工程：`netstandard2.0;netstandard2.1;net6.0`。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AspNetCore.Abstractions

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
