# Bing.AspNetCore.Mvc.Contracts
[![NuGet](https://img.shields.io/nuget/v/Bing.AspNetCore.Mvc.Contracts.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.Contracts/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AspNetCore.Mvc.Contracts.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.Contracts/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 打包壳工程：无源码，转发 `Bing.Ddd.Application` 契约。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L6 表现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：0
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

这是一个**转发用的空壳包**，自身不含源码，只是把应用层契约以 `Bing.AspNetCore.Mvc.Contracts` 的名字发布出来。

## 安装

```bash
dotnet add package Bing.AspNetCore.Mvc.Contracts
```

## 与其它包的关系

## 注意事项

cs=0，无源码。如果你要的是契约内容，直接引用 `Bing.Ddd.Application.Contracts`。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AspNetCore.Mvc.Contracts

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
