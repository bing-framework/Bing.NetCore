# Bing.Validation
[![NuGet](https://img.shields.io/nuget/v/Bing.Validation.svg)](https://www.nuget.org/packages/Bing.Validation/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Validation.svg)](https://www.nuget.org/packages/Bing.Validation/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 基于 DataAnnotations 的校验框架，附带中文业务验证特性。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：23
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

在标准 DataAnnotations 之上补充了一批**中国业务场景**常用特性（身份证、手机号、中文等），并提供统一的 `DataAnnotationValidation.Validate(...)` 入口。

## 安装

```bash
dotnet add package Bing.Validation
```

## 快速上手

```csharp
var result = DataAnnotationValidation.Validate(target);
if (!result.IsValid) { /* 处理错误 */ }

// 中文业务特性
[IdCard]  public string IdCardNo { get; set; }
[TelNoOfChina] public string Tel { get; set; }
[Chinese] public string Name { get; set; }
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `DataAnnotationValidation` | `Bing.Validation` | 校验入口 |
| `ChineseAttribute` / `IdCardAttribute` 等 | `Bing.Validation` | 中文业务验证特性 |

## 与其它包的关系

**依赖本包**：`Bing.Aop.AspectCore`、`Bing.Biz.OAuthLogin`

## 注意事项

在应用服务参数上标记 `[Valid]` 可配合 AOP 自动触发校验（需 `Bing.Aop.AspectCore`）。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Validation

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
