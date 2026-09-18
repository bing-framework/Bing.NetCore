# Bing.EventBus
[![NuGet](https://img.shields.io/nuget/v/Bing.EventBus.svg)](https://www.nuget.org/packages/Bing.EventBus/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.EventBus.svg)](https://www.nuget.org/packages/Bing.EventBus/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> ⚠ 遗留事件总线实现（新项目请勿使用）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：横切 / 事件 ｜ **当前版本**：7.0.0 ｜ **源文件**：26
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

早期风格的事件总线（`IEventBus` / `ILocalEventBus` / `IDistributedEventBus` + 多种 HandlerFactory），已被 `Bing.Events` 取代。当前仓库内**只有它自己的测试工程引用**。

## 安装

```bash
dotnet add package Bing.EventBus
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IEventBus` / `ILocalEventBus` / `IDistributedEventBus` | `Bing.EventBus` | ⚠ 与 `Bing.Events` 的 `IEventBus` 同名，勿混用 |

## 与其它包的关系

## 注意事项

⚠⚠ **不要在新代码中使用**。它的 `IEventBus` / `IMessageEventBus` 与 `Bing.Events` 撞名，同时 using 会产生歧义编译错误。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.EventBus

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
