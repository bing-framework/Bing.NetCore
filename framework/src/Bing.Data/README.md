# Bing.Data
[![NuGet](https://img.shields.io/nuget/v/Bing.Data.svg)](https://www.nuget.org/packages/Bing.Data/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Data.svg)](https://www.nuget.org/packages/Bing.Data/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 查询对象、分页与条件封装：`Query<TEntity>` / `Pager` / `ICondition`。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：59
>
> **上游 Bing 包**：`Bing.Core`
>
> **三方依赖**：`System.Linq.Dynamic.Core`

---

## 这是什么

提供 ORM 无关的**查询参数模型**：把"前端传来的查询条件"变成对象，交给仓储或应用服务消费。包含 `Query<TEntity>` / `IQuery`、`Pager` / `PagerList<T>`、以及各类 `ICondition` 实现。

## 安装

```bash
dotnet add package Bing.Data
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `Query<TEntity>` / `IQuery` | `Bing.Data.Queries` | 查询对象基类 |
| `Pager` / `PagerList<T>` | `Bing.Data` | 分页参数与分页结果 |
| `ICondition` | `Bing.Data` | 查询条件抽象 |

## 与其它包的关系

**本包依赖**：`Bing.Core`

**依赖本包**：`Bing.Events`、`Bing.MultiTenancy.Abstractions`

**三方 NuGet**：`System.Linq.Dynamic.Core`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Data

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
