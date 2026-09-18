# Bing.Ddd.Application
[![NuGet](https://img.shields.io/nuget/v/Bing.Ddd.Application.svg)](https://www.nuget.org/packages/Bing.Ddd.Application/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Ddd.Application.svg)](https://www.nuget.org/packages/Bing.Ddd.Application/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 应用服务基类：`AppServiceBase` / `QueryAppServiceBase` / `CrudAppServiceBase`。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L4 领域层 ｜ **当前版本**：7.0.0 ｜ **源文件**：5
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

把"查询对象 → 仓储查询 → DTO"这套重复劳动封装成基类，接入 `IAsyncQueryableExecuter` 支持异步。

## 安装

```bash
dotnet add package Bing.Ddd.Application
```

## 快速上手

```csharp
public class OrderAppService
    : CrudAppServiceBase<Order, OrderDto, OrderQuery, Guid>, IOrderAppService
{
    public OrderAppService(IRepository<Order, Guid> repo, IObjectMapper mapper) : base(repo, mapper) { }
}
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `AppServiceBase` / `QueryAppServiceBase` / `CrudAppServiceBase` / `DeleteAppServiceBase` | `Bing.Application.Services` | 应用服务基类 |
| `IAsyncQueryableExecuter` | `Bing.Linq` | `IQueryable` 异步执行抽象 |

## 与其它包的关系

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Ddd.Application

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
