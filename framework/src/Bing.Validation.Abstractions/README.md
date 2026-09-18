# Bing.Validation.Abstractions
[![NuGet](https://img.shields.io/nuget/v/Bing.Validation.Abstractions.svg)](https://www.nuget.org/packages/Bing.Validation.Abstractions/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Validation.Abstractions.svg)](https://www.nuget.org/packages/Bing.Validation.Abstractions/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 校验相关抽象接口，供领域层 / 应用层引用。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：4
>
> **上游 Bing 包**：无（本包是叶子/基础包）
>
> **三方依赖**：`System.ComponentModel.Annotations`

---

## 这是什么

把校验契约从实现里剥出来，让上层工程不必依赖具体的校验实现。

## 安装

```bash
dotnet add package Bing.Validation.Abstractions
```

## 与其它包的关系

**三方 NuGet**：`System.ComponentModel.Annotations`

## 注意事项

只是契约工程，不含运行时实现。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Validation.Abstractions

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
