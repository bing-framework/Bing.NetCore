# Bing.Ddd.Application.Contracts
[![NuGet](https://img.shields.io/nuget/v/Bing.Ddd.Application.Contracts.svg)](https://www.nuget.org/packages/Bing.Ddd.Application.Contracts/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Ddd.Application.Contracts.svg)](https://www.nuget.org/packages/Bing.Ddd.Application.Contracts/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 应用层契约：`IAppService` / `ICrudAppService`、DTO 基类与 `[UnitOfWork]`。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L4 领域层 ｜ **当前版本**：7.0.0 ｜ **源文件**：27
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

定义应用服务的接口与 DTO 约定，供表现层与客户端引用。

## 安装

```bash
dotnet add package Bing.Ddd.Application.Contracts
```

## 快速上手

```csharp
public interface IOrderAppService : ICrudAppService<OrderDto, OrderQuery> { }

public class OrderQuery : Query<OrderDto> { public string OrderNo { get; set; } }
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IAppService` / `ICrudAppService` / `IQueryAppService` | `Bing.Application.Services` | 应用服务契约 |
| `DtoBase` / `RequestBase` | `Bing.Application.Services` | DTO 与请求基类 |
| `[UnitOfWork]` | `Bing.Application.Services` | 工作单元拦截特性（需 AOP） |

## 与其它包的关系

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Ddd.Application.Contracts

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
