# 配方 5：支付接入与回调

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

> 目标：用 `Bing.Biz.Payments` 完成**下单**与**回调验签**，覆盖支付宝与微信的常见支付场景。
>
> 这个包在 `components/src`（不是 `framework/src`），约有 96 个源文件，是框架里比较完整的一块。

## 1. 装包

```bash
dotnet add package Bing.Biz.Payments
```

## 2. 注册

```csharp
// 注册整套支付（返回 void，⚠ 不能链式调用，要单独一行）
services.AddPay(x =>
{
    // 支付宝商户配置
    // 微信支付商户配置
});

// 也可以只注册其中一种
services.AddAlipay<MyAlipayConfigProvider>();
services.AddWechatpay<MyWechatpayConfigProvider>();
```

> ⚠ **`AddPay` 返回 `void`**，别写 `services.AddPay(...).AddXxx()`。

## 3. 下单（用工厂创建支付服务）

`IPayFactory` 提供按场景创建支付服务的方法：

| 方法 | 用途 |
| --- | --- |
| `CreatePayService(PayWay way)` | 按支付方式创建通用支付服务 |
| `CreateAlipayPagePayService()` | 支付宝电脑网站支付 |
| `CreateAlipayWapPayService()` | 支付宝手机网站支付 |
| `CreateAlipayAppPayService()` | 支付宝 App 支付 |
| `CreateAlipayQrCodePayService()` | 支付宝当面付（二维码） |
| `CreateAlipayBarcodePayService()` | 支付宝条码支付 |
| `CreateWechatpayJsApiPayService()` | 微信 JSAPI（公众号/小程序内） |
| `CreateWechatpayMiniProgramPayService()` | 微信小程序支付 |
| `CreateH5PayService()` | 微信 H5 支付 |
| `CreateWechatpayAppPayService()` | 微信 App 支付 |
| `CreateWechatpayBarcodePayService()` | 微信付款码支付 |

```csharp
public class PayAppService : AppServiceBase
{
    private readonly IPayFactory _factory;
    public PayAppService(IPayFactory factory) => _factory = factory;

    public async Task<string> PayByAlipayPageAsync(OrderDto order)
    {
        var service = _factory.CreateAlipayPagePayService();
        var result = await service.PayAsync(new AlipayPagePayParam
        {
            OrderId      = order.Id.ToString(),
            OrderName    = "订单 " + order.OrderNo,
            Money        = order.Amount,
            NotifyUrl    = "https://your.domain/api/pay/alipay/notify",
            ReturnUrl    = "https://your.domain/pay/result"
        });
        return result.Result;      // 支付宝页面支付返回可跳转的表单/URL
    }
}
```

> 各 `PayAsync` 的入参类型（如 `AlipayPagePayParam`）以源码为准，参数名可能与示例略有差异。

## 4. 回调：验签 + 幂等

```csharp
[ApiController, Route("api/pay")]
public class PayNotifyController : ApiControllerBase
{
    private readonly IPayFactory _factory;
    private readonly IOrderStore _store;

    [HttpPost("alipay/notify")]
    public async Task<string> AlipayNotify()
    {
        var notify = _factory.CreateAlipayNotifyService();
        var trade = await notify.GetTradeAsync();     // ⚠ 内部完成验签

        // 🔴 幂等：支付平台会重复通知
        var order = await _store.FindByIdAsync(Guid.Parse(trade.OrderId));
        if (order is null || order.IsPaid) return "success";

        // 金额校验 + 商户号校验，再改订单状态
        if (trade.Money != order.Amount) return "fail";

        order.MarkPaid(trade.TradeNo);
        await _store.UpdateAsync(order);
        await UnitOfWorkManager.CommitAsync();

        return "success";          // 返回给支付宝的文本，按平台要求
    }
}
```

| 回调服务 | 方法 |
| --- | --- |
| `CreateAlipayNotifyService()` | 支付宝异步通知 |
| `CreateAlipayReturnService()` | 支付宝同步跳转返回 |
| `CreateWechatpayNotifyService()` | 微信支付结果通知 |
| `CreateWechatpayDownloadBillService()` | 微信下载交易账单（对账用） |

## 注意事项

1. 🔴 **回调必须幂等**：支付平台会重复通知，不做幂等会重复发货/重复记账。
2. 🔴 **验签 + 金额 + 商户号三重校验**：只验签不验金额，攻击者可以用小额订单伪造通知。
3. 🔴 **凭据不要写进配置文件**：商户私钥、API v3 密钥、AppId 等请走环境变量或用户机密。
4. **回调接口要排除在请求日志之外**：回调体里通常含签名、金额、用户信息，而框架的 HTTP 日志**没有字段级脱敏**（见 [安全指南 §4](../../operations/安全指南.md)）。用 `RequestFilter` 排除该路径。
5. **对账**：微信侧可用 `CreateWechatpayDownloadBillService()` 定期拉账单对账。
6. 这个包在 `components/src`，**不在 docfx 元数据范围内**，所以自动生成的 API 站点里查不到它——要看逐方法文档请翻 `components/src/Bing.Biz.Payments/README.md`。

## 相关

- [组件库文档](../组件库文档.md)（支付包的状态与完整说明）
- [安全指南](../../operations/安全指南.md)
- [包索引](../../getting-started/包索引.md)
