# Bing.Authorization.Abstractions
[![NuGet](https://img.shields.io/nuget/v/Bing.Authorization.Abstractions.svg)](https://www.nuget.org/packages/Bing.Authorization.Abstractions/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Authorization.Abstractions.svg)](https://www.nuget.org/packages/Bing.Authorization.Abstractions/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 授权抽象接口（`IFunction` / `IFunctionHandler` 等）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：3
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

授权相关契约，供权限与安全模块引用。当前文档覆盖较少，使用前建议直接看源码。

## 安装

```bash
dotnet add package Bing.Authorization.Abstractions
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IFunction` / `IFunctionHandler` | `Bing.Authorization` | 功能权限契约 |

## 与其它包的关系

## 注意事项

⚠ 这是当前文档覆盖最薄弱的工程之一（仓库文档中几乎零出现），使用前请先读源码确认。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Authorization.Abstractions

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
