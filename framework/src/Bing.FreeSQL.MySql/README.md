# Bing.FreeSQL.MySql
[![NuGet](https://img.shields.io/nuget/v/Bing.FreeSQL.MySql.svg)](https://www.nuget.org/packages/Bing.FreeSQL.MySql/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.FreeSQL.MySql.svg)](https://www.nuget.org/packages/Bing.FreeSQL.MySql/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> FreeSQL MySql Provider（当前唯一，可作为扩展其他库的模板）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L3 数据访问实现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：5
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

构建 `IFreeSql`（`DataType.MySql`），注入审计 AOP 与软删除全局过滤，注册单例 `FreeSqlWrapper` 与作用域工作单元。

## 安装

```bash
dotnet add package Bing.FreeSQL.MySql
```

## 快速上手

```csharp
services.AddMySqlUnitOfWork<IAdminUnitOfWork, AdminUnitOfWork>(connectionString);
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddMySqlUnitOfWork<TUnitOfWork,TImpl>(services, connection, ...)` | **`Bing.FreeSQL`** | ⚠ 命名空间是 `Bing.FreeSQL`，与 EF 的同名方法不是同一个 |

## 与其它包的关系

## 注意事项

扩展其他数据库：复制本工程 → 换 `FreeSql.Provider.*` 包 → 改 `DataType.MySql` → 重命名 `Add*UnitOfWork`。核心库 `Bing.FreeSQL` 无需改动。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.FreeSQL.MySql

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
