# Bing.Biz.Payments
[![NuGet](https://img.shields.io/nuget/v/Bing.Biz.Payments.svg)](https://www.nuget.org/packages/Bing.Biz.Payments/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Biz.Payments.svg)](https://www.nuget.org/packages/Bing.Biz.Payments/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 支付宝与微信支付：统一的支付服务工厂与回调处理。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：业务组件（components/） ｜ **当前版本**：7.0.0 ｜ **源文件**：96
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

框架里体量最大的业务组件（约 96 个源文件），覆盖：

**支付宝**：条码付 / 二维码付 / App 付 / 电脑网站付 / 手机网站付 + 回调通知与同步跳转；
**微信支付**：付款码 / JsApi / Native / App / H5 / 小程序 + 回调通知、退款、下载账单。

内置签名管理器（MD5 / HMAC-SHA256）与账单 CSV 映射，不依赖额外三方支付 SDK。

## 安装

```bash
dotnet add package Bing.Biz.Payments
```

## 快速上手

```csharp
using Bing.Biz.Payments.Extensions;   // ← 必须，AddPay 在这个命名空间

services.AddPay(o =>
{
    o.AlipayOptions = new AlipayConfig { /* AppId / 私钥 / 公钥 等 */ };
    o.WechatpayOptions = new WechatpayConfig { /* AppId / 商户号 / 密钥 等 */ };
});

// 使用：注入 IPayFactory 按 PayWay 创建服务
var service = _payFactory.CreatePayService(PayWay.AlipayPagePay);
var result = await service.PayAsync(param);
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddPay(Action<PayOptions>)` | `Bing.Biz.Payments.Extensions` | 注册支付宝 + 微信两套配置与工厂 |
| `AddPay<TAlipayConfigProvider, TWechatpayConfigProvider>()` | `Bing.Biz.Payments.Extensions` | 用自定义配置提供器 |
| `AddAlipay<TAlipayConfigProvider>()` | `Bing.Biz.Payments.Extensions` | 只注册支付宝 |
| `AddWechatpay<TWechatpayConfigProvider>()` | `Bing.Biz.Payments.Extensions` | 只注册微信 |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IPayFactory` / `PayFactory` | `Bing.Biz.Payments` | 按 `PayWay` 创建各渠道服务 |
| `IPayService` / `PayParam` / `PayResult` | `Bing.Biz.Payments.Core` | 支付核心契约 |
| `IAlipayNotifyService` / `IAlipayReturnService` | `Bing.Biz.Payments.Alipay.Abstractions` | 支付宝回调 / 同步跳转 |
| `IWechatpayNotifyService` / `IWechatpayRefundService` | `Bing.Biz.Payments.Wechatpay.Abstractions` | 微信回调 / 退款 |

## 与其它包的关系

## 注意事项

⚠ 三个要点：
1. `AddPay` 系列返回 **`void`**，**不能链式调用**（与多数 `AddXxx` 返回 `IServiceCollection` 的习惯不同）；
2. 必须 `using Bing.Biz.Payments.Extensions;`，否则找不到扩展方法；
3. `CreatePayService(PayWay)` 遇到未实现的支付方式会抛 `NotImplementedException`。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [组件库文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/组件库文档.md) —— `components/` 下各包的详细说明与状态徽章
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Biz.Payments

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
