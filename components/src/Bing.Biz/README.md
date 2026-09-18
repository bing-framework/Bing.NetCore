# Bing.Biz
[![NuGet](https://img.shields.io/nuget/v/Bing.Biz.svg)](https://www.nuget.org/packages/Bing.Biz/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Biz.svg)](https://www.nuget.org/packages/Bing.Biz/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 公共业务基础类型：地址、联系人与常用枚举。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：业务组件（components/） ｜ **当前版本**：7.0.0 ｜ **源文件**：7
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

很小的纯类型库，只有：

- `Addresses/` —— `Address`、`AddressInfo`（含扩展方法）、`NullAddress`；
- `Contacts/` —— `Contact`；
- `Enums/` —— `Gender`、`Nation`。

**没有也不需要注册**，直接当类型/DTO 用。

## 安装

```bash
dotnet add package Bing.Biz
```

## 快速上手

```csharp
var address = new Address { Province = "广东省", City = "深圳市" };
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `Address` / `AddressInfo` / `NullAddress` | `Bing.Biz.Addresses` | 地址模型 |
| `Contact` | `Bing.Biz.Contacts` | 联系人模型 |
| `Gender` / `Nation` | `Bing.Biz.Enums` | 性别 / 民族枚举 |

## 与其它包的关系

## 注意事项

⚠ 本包位于 `components/src`，**不在** `framework/src`，因此不参与 docfx 的 API 元数据生成。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [组件库文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/组件库文档.md) —— `components/` 下各包的详细说明与状态徽章
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Biz

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
