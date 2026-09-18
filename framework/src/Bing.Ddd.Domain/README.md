# Bing.Ddd.Domain
[![NuGet](https://img.shields.io/nuget/v/Bing.Ddd.Domain.svg)](https://www.nuget.org/packages/Bing.Ddd.Domain/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Ddd.Domain.svg)](https://www.nuget.org/packages/Bing.Ddd.Domain/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> DDD 领域层：实体 / 聚合根 / 值对象、仓储接口、领域事件与变更跟踪。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L4 领域层 ｜ **当前版本**：7.0.0 ｜ **源文件**：63
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

提供领域建模的全部基础设施：

- 实体基类 `Entity` / `AggregateRoot` / `ValueObject` 与审计接口；
- 仓储与存储契约 `IRepository<TEntity,TKey>` / `IStore<TEntity,TKey>` / `IQueryStore`；
- 领域事件分发 `AddDomainEventDispatcher(...)`。

⚠ **运行时命名空间与包名不同**：仓储接口在 **`Bing.Domain.Repositories`**，不在 `Bing.Data`。

## 安装

```bash
dotnet add package Bing.Ddd.Domain
```

## 快速上手

```csharp
public class Order : AggregateRoot<Guid>
{
    public string OrderNo { get; private set; }
    public void ChangeNo(string no) { OrderNo = no; AddDomainEvent(new OrderNoChangedEvent(Id, no)); }
}

// 仓储注入（接口在 Bing.Domain.Repositories）
public class OrderService(IRepository<Order, Guid> repo) { }
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddDomainEventDispatcher(services)` / `(services, Type[])` / `(services, Assembly[])` | `Bing.Domain.Entities.Events` | 扫描并注册领域事件处理器分发器 |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `Entity` / `AggregateRoot` / `ValueObject` | `Bing.Domain.Entities` | 实体与聚合根基类 |
| `IRepository<TEntity,TKey>` / `IStore<TEntity,TKey>` | `Bing.Domain.Repositories` | ⚠ 仓储契约在 **Domain** 包，不在 `Bing.Data` |
| `IHasErrorCode` / 审计接口 | `Bing.Domain.Entities` | 实体能力接口 |

## 与其它包的关系

**依赖本包**：`Bing.EntityFrameworkCore`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Ddd.Domain

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
