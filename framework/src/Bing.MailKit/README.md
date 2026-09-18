# Bing.MailKit
[![NuGet](https://img.shields.io/nuget/v/Bing.MailKit.svg)](https://www.nuget.org/packages/Bing.MailKit/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.MailKit.svg)](https://www.nuget.org/packages/Bing.MailKit/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 基于 MailKit 的邮件实现（推荐）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：横切 / 工具 ｜ **当前版本**：7.0.0 ｜ **源文件**：11
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

`IMailKitEmailSender` / `MailKitEmailSender`，支持 `SecureSocketOptions` 等现代 SMTP 选项，并提供 `IMailKitSmtpBuilder` 供自定义连接构建。

## 安装

```bash
dotnet add package Bing.MailKit
```

## 快速上手

```csharp
using Bing.MailKit.Extensions;

services.AddMailKit(o =>
{
    o.EmailConfig.Host = "smtp.example.com";
    o.EmailConfig.Port = 465;
    o.EmailConfig.UserName = "user";
    o.EmailConfig.Password = "***";
    o.MailKitConfig.SecureSocketOption = SecureSocketOptions.SslOnConnect;
});

await emailSender.SendAsync("a@b.com", "主题", "正文");
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddMailKit(Action<EmailOptions>)` | `Bing.MailKit.Extensions` | ⚠ 返回 `void`，不能链式调用 |
| `AddMailKit<TEmailConfigProvider, TMailKitConfigProvider>()` | `Bing.MailKit.Extensions` | 自定义配置提供器版本 |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IMailKitEmailSender` / `MailKitEmailSender` | `Bing.MailKit` | 发送实现 |
| `EmailOptions` | `Bing.MailKit.Extensions` | 含 `EmailConfig` + `MailKitConfig` |
| `IMailKitSmtpBuilder` / `DefaultMailKitSmtpBuilder` | `Bing.MailKit` | SmtpClient 构建器（可替换） |

## 与其它包的关系

## 注意事项

⚠ `Bing.Emailing.Smtp` 命名空间下**也有**一个 `AddMailKit<T>()`，它注册的是 `ISmtpEmailSender`（走 System.Net.Mail）。using 写错会拿到另一套实现。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.MailKit

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
