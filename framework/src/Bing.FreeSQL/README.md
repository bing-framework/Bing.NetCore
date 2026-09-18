# Bing.FreeSQL
[![NuGet](https://img.shields.io/nuget/v/Bing.FreeSQL.svg)](https://www.nuget.org/packages/Bing.FreeSQL/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.FreeSQL.svg)](https://www.nuget.org/packages/Bing.FreeSQL/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> FreeSQL 数据访问核心：工作单元、乐观锁与审计 AOP、软删除全局过滤。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L3 数据访问实现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：13
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

把 FreeSQL 接入框架：`UnitOfWorkBase` 继承 `FreeSql.DbContext` 并实现 `IUnitOfWork` / `IDatabase`，乐观锁异常会被包装成框架的 `ConcurrencyException`。

## 安装

```bash
dotnet add package Bing.FreeSQL
```

## 快速上手

```csharp
// 具体库由 Provider 包提供（当前仅 MySql）
services.AddMySqlUnitOfWork<IAdminUnitOfWork, AdminUnitOfWork>(connection);
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddMySqlUnitOfWork<TUnitOfWork,TImpl>(...)` | **`Bing.FreeSQL`**（由 Provider 包提供） | ⚠ 与 EF Core 的 `AddMySqlUnitOfWork` **同名但不同方法**，参数签名也不同 |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `UnitOfWorkBase` | `Bing.Uow` | 继承 `FreeSql.DbContext`，实现 `IUnitOfWork` / `IDatabase` |

## 与其它包的关系

## 注意事项

⚠ FreeSQL 目前**只有 MySql Provider**（`Bing.FreeSQL.MySql`），扩展其他数据库按 Provider 工程模板复制。

⚠ **异步查询会退化成同步**：`FreeSqlAsyncQueryableProvider.CanExecute<T>()` 恒返回 `false`，其异步方法全部 `throw new NotImplementedException()`。实际运行时由 `AsyncQueryableExecuter` 在找不到可用 Provider 时回退为 `Task.FromResult(queryable.ToList())`——**不会抛异常，但会同步阻塞调用线程**。高频异步路径请改用仓储自带的异步方法（走 FreeSql 原生异步），或改用 EF Core 支。详见 [能力矩阵 §4.3](../../../docs/getting-started/能力矩阵.md)。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.FreeSQL

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
