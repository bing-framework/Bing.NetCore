# Bing.Data.Sql.Analyzers
[![NuGet](https://img.shields.io/nuget/v/Bing.Data.Sql.Analyzers.svg)](https://www.nuget.org/packages/Bing.Data.Sql.Analyzers/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Data.Sql.Analyzers.svg)](https://www.nuget.org/packages/Bing.Data.Sql.Analyzers/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> `Bing.Data.Sql` 的编译期分析器：拦截未参数化的插值 SQL。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L3 数据访问实现层 ｜ **当前版本**：7.0.0 ｜ **源文件**：2
>
> **上游 Bing 包**：无（本包是叶子/基础包）
>
> **三方依赖**：`Microsoft.CodeAnalysis.Analyzers`、`Microsoft.CodeAnalysis.CSharp`

---

## 这是什么

Roslyn 分析器，在**编译期**发现"把插值字符串传入普通 SQL `string` 入口"这类会拼接值、可能引发注入的写法。

## 安装

```bash
dotnet add package Bing.Data.Sql.Analyzers
```

## 快速上手

```csharp
// 触发 BINGSQL002 警告的写法
query.Sql($"select * from User where Name = {name}");   // ⚠ 会拼接值

// 正确写法
query.SqlInterpolated($"select * from User where Name = {name}");
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| 诊断 `BINGSQL002` | `Bing.Data.Sql.Analyzers` | 「插值 SQL 未参数化」，默认 `Warning`，分类 `Bing.Data.Sql.Security` |

## 与其它包的关系

**依赖本包**：`Bing.Data.Sql`

**三方 NuGet**：`Microsoft.CodeAnalysis.Analyzers`、`Microsoft.CodeAnalysis.CSharp`

## 注意事项

出现该警告时**不要直接 `NoWarn` 掉**，应改为参数化写法。作为 `Bing.Data.Sql` 的依赖自动引入，一般无需单独安装。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Data.Sql.Analyzers

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
