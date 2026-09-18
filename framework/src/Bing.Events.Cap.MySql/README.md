# Bing.Events.Cap.MySql
[![NuGet](https://img.shields.io/nuget/v/Bing.Events.Cap.MySql.svg)](https://www.nuget.org/packages/Bing.Events.Cap.MySql/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Events.Cap.MySql.svg)](https://www.nuget.org/packages/Bing.Events.Cap.MySql/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.1-blue.svg)

> ⚠ CAP 2.6 的 MySql 存储源码镜像（升级 MySqlConnector，用于支持 FreeSQL）。
>
> **目标框架**：`netstandard2.1` ｜ **分层**：横切 / 事件 ｜ **当前版本**：7.0.0 ｜ **源文件**：10
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

这是 **CAP 2.6 官方 MySql 存储的一份源码拷贝**（`00-Source/`），把 MySQL 驱动换成 MySqlConnector，目的是解决 FreeSQL 场景下的驱动冲突。

⚠ 当前仓库内**没有任何工程引用它**——唯一引用点在 `Bing.Admin.Data.FreeSQL.csproj` 里，且被注释掉。

## 安装

```bash
dotnet add package Bing.Events.Cap.MySql
```

## 快速上手

```csharp
// 启用后（需自行打开 ProjectReference 并评估兼容性）：
o.UseMySql(connectionString);
// 注意：本包也把 UseEntityFramework<TContext> 改写成走 MySql 存储
o.UseEntityFramework<AdminUnitOfWork>();
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `UseMySql(...)` / `UseEntityFramework<TContext>(...)` | `Microsoft.Extensions.DependencyInjection` | CAP 的 `CapOptions` 扩展 |

## 与其它包的关系

## 注意事项

⚠⚠ 这是 **CAP 2.6** 的源码，而 `Bing.Events` 用的是 **CAP 8.0.1**——版本跨度很大，**直接启用大概率需要适配**。只有「FreeSQL + MySQL + CAP」场景才可能需要它。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Events.Cap.MySql

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
