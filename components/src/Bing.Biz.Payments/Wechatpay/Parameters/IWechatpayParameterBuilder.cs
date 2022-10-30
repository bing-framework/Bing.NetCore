namespace Bing.Biz.Payments.Wechatpay.Parameters;

/// <summary>
/// 微信支付参数生成器
/// </summary>
public interface IWechatpayParameterBuilder
{
    /// <summary>
    /// 获取签名
    /// </summary>
    string GetSign();

    /// <summary>
    /// 获取Xml结果，包含签名
    /// </summary>
    string ToXml();

    /// <summary>
    /// 获取Xml结果，不包含签名
    /// </summary>
    string ToXmlNoContainsSign();

    /// <summary>
    /// 获取Json结果，包含签名
    /// </summary>
    string ToJson();
}

/// <summary>
/// 微信支付参数生成器
/// </summary>
public interface IWechatpayParameterBuilder<out TParameterBuilder> : IWechatpayParameterBuilder
    where TParameterBuilder : IWechatpayParameterBuilder<TParameterBuilder>
{
    /// <summary>
    /// 添加参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    TParameterBuilder Add(string name, object value);

    /// <summary>
    /// 设置签名参数名称
    /// </summary>
    /// <param name="name">参数名</param>
    TParameterBuilder SignParamName(string name);
}