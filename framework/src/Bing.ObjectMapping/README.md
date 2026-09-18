# Bing.ObjectMapping
[![NuGet](https://img.shields.io/nuget/v/Bing.ObjectMapping.svg)](https://www.nuget.org/packages/Bing.ObjectMapping/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.ObjectMapping.svg)](https://www.nuget.org/packages/Bing.ObjectMapping/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 对象映射抽象 `IObjectMapper`（具体实现在 `Bing.AutoMapper`）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：5
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

只定义映射契约，让领域层/应用层不依赖任何具体映射库。需要真正使用时请一并安装 `Bing.AutoMapper`。

## 安装

```bash
dotnet add package Bing.ObjectMapping
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IObjectMapper` | `Bing.ObjectMapping` | 对象映射抽象 |

## 与其它包的关系

**依赖本包**：`Bing.AutoMapper`

## 注意事项

单独装这个包**不能**真正做映射，必须配合 `Bing.AutoMapper`。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.ObjectMapping

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
