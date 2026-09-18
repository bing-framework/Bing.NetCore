# Bing.Emailing
[![NuGet](https://img.shields.io/nuget/v/Bing.Emailing.svg)](https://www.nuget.org/packages/Bing.Emailing/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Emailing.svg)](https://www.nuget.org/packages/Bing.Emailing/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 邮件抽象与 Smtp 实现：配置、附件、邮件队列。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：横切 / 工具 ｜ **当前版本**：7.0.0 ｜ **源文件**：20
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

提供 `IEmailSender` 抽象、`EmailConfig`、`IAttachment`（内存流 / 物理文件两种实现）以及**邮件队列**（`IMailQueueManager` / `IMailQueueService`）。命名空间 `Bing.Emailing.Smtp` 下还有基于 `System.Net.Mail.SmtpClient` 的实现。

## 安装

```bash
dotnet add package Bing.Emailing
```

## 快速上手

```csharp
using Bing.Emailing.Smtp;   // ⚠ 这个命名空间下的 AddMailKit 注册的是 ISmtpEmailSender

services.AddSmtpEmail(o => { o.Host = "smtp.example.com"; o.Port = 25; });

// 使用
await emailSender.SendAsync("a@b.com", "主题", "正文");
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddSmtpEmail(Action<EmailConfig>)` | `Bing.Emailing.Smtp` | 基于 `System.Net.Mail.SmtpClient` |
| `AddMailKit<TEmailConfigProvider>()` | `Bing.Emailing.Smtp` | ⚠ 在 **Smtp** 命名空间下，注册的也是 `ISmtpEmailSender` |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IEmailSender` / `EmailSenderBase` / `NullEmailSender` | `Bing.Emailing` | 发送抽象与兜底实现 |
| `IAttachment` / `MemoryStreamAttachment` / `PhysicalFileAttachment` | `Bing.Emailing` / `.Attachments` | 附件 |
| `IMailQueueManager` / `IMailQueueService` | `Bing.Emailing` | 邮件队列（需自己调度） |

## 与其它包的关系

## 注意事项

⚠ `IMailQueueManager` 只负责队列存取，**没有**内置后台自动发送，调度要你自己接。生产推荐改用 `Bing.MailKit`。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [横切能力文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/横切能力文档.md) —— 邮件 / 模板 / 锁 / 本地化 / 事件总线的详解与选型
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Emailing

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
