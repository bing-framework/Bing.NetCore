namespace Bing.Biz.Payments.Wechatpay.Configs;

/// <summary>
/// 微信支付常量
/// </summary>
public static class WechatpayConst
{
    /// <summary>
    /// 微信支付跟踪日志名
    /// </summary>
    public const string TraceLogName = "WechatpayTraceLog";

    /// <summary>
    /// 应用标识
    /// </summary>
    public const string AppId = "appid";

    /// <summary>
    /// 商户号
    /// </summary>
    public const string MerchantId = "mch_id";

    /// <summary>
    /// 标题
    /// </summary>
    public const string Body = "body";

    /// <summary>
    /// 商户订单号
    /// </summary>
    public const string OutTradeNo = "out_trade_no";

    /// <summary>
    /// 微信支付订单号
    /// </summary>
    public const string TransactionId = "transaction_id";

    /// <summary>
    /// 货币类型
    /// </summary>
    public const string FeeType = "fee_type";

    /// <summary>
    /// 总金额
    /// </summary>
    public const string TotalFee = "total_fee";

    /// <summary>
    /// 回调通知Url
    /// </summary>
    public const string NotifyUrl = "notify_url";

    /// <summary>
    /// 终端IP
    /// </summary>
    public const string SpbillCreateIp = "spbill_create_ip";

    /// <summary>
    /// 交易类型
    /// </summary>
    public const string TradeType = "trade_type";

    /// <summary>
    /// 签名类型
    /// </summary>
    public const string SignType = "sign_type";

    /// <summary>
    /// 签名
    /// </summary>
    public const string Sign = "sign";

    /// <summary>
    /// 成功
    /// </summary>
    public const string Success = "SUCCESS";

    /// <summary>
    /// 失败
    /// </summary>
    public const string Fail = "FAIL";

    /// <summary>
    /// 成功
    /// </summary>
    public const string Ok = "OK";

    /// <summary>
    /// 返回状态码
    /// </summary>
    public const string ReturnCode = "return_code";

    /// <summary>
    /// 业务结果代码
    /// </summary>
    public const string ResultCode = "result_code";

    /// <summary>
    /// 返回消息
    /// </summary>
    public const string ReturnMessage = "return_msg";

    /// <summary>
    /// 伙伴标识
    /// </summary>
    public const string PartnerId = "partnerid";

    /// <summary>
    /// 时间戳
    /// </summary>
    public const string Timestamp = "timestamp";

    /// <summary>
    /// 包
    /// </summary>
    public const string Package = "package";

    /// <summary>
    /// 错误码
    /// </summary>
    public const string ErrorCode = "err_code";

    /// <summary>
    /// 错误码和描述
    /// </summary>
    public const string ErrorCodeDescription = "err_code_des";

    /// <summary>
    /// 附加数据
    /// </summary>
    public const string Attach = "attach";

    /// <summary>
    /// 用户标识
    /// </summary>
    public const string OpenId = "openid";

    /// <summary>
    /// 用户授权码
    /// </summary>
    public const string AuthCode = "auth_code";

    /// <summary>
    /// 对账单日期
    /// </summary>
    public const string BillDate = "bill_date";

    /// <summary>
    /// 账单类型
    /// </summary>
    public const string BillType = "bill_type";

    /// <summary>
    /// 压缩账单
    /// </summary>
    public const string TarType = "tar_type";

    /// <summary>
    /// 资金账户类型
    /// </summary>
    public const string AccountType = "account_type";

    /// <summary>
    /// 随机字符串
    /// </summary>
    public const string NonceStr = "nonce_str";
}