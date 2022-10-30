using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Helpers;

namespace Bing.Biz.Payments.Wechatpay.Parameters;

/// <summary>
/// 微信支付参数生成器(<see cref="IWechatpayParameterBuilder{TParameterBuilder}"/>) 扩展
/// </summary>
public static class WechatpayParameterBuilderExtensions
{
    #region AppId(设置应用标识)

    /// <summary>
    /// 设置应用标识
    /// </summary>
    /// <typeparam name="TParameterBuilder">微信支付参数生成器</typeparam>
    /// <param name="builder">微信支付参数生成器</param>
    /// <param name="appId">应用标识</param>
    public static TParameterBuilder AppId<TParameterBuilder>(this TParameterBuilder builder, string appId)
        where TParameterBuilder : IWechatpayParameterBuilder<TParameterBuilder>
    {
        builder.Add(WechatpayConst.AppId, appId);
        return builder;
    }

    #endregion

    #region MerchantId(设置商户号)

    /// <summary>
    /// 设置商户号
    /// </summary>
    /// <typeparam name="TParameterBuilder">微信支付参数生成器</typeparam>
    /// <param name="builder">微信支付参数生成器</param>
    /// <param name="merchantId">商户号</param>
    public static TParameterBuilder MerchantId<TParameterBuilder>(this TParameterBuilder builder, string merchantId)
        where TParameterBuilder : IWechatpayParameterBuilder<TParameterBuilder>
    {
        builder.Add(WechatpayConst.MerchantId, merchantId);
        return builder;
    }

    #endregion

    #region NonceStr(设置随机字符串)

    /// <summary>
    /// 设置随机字符串
    /// </summary>
    /// <typeparam name="TParameterBuilder">微信支付参数生成器</typeparam>
    /// <param name="builder">微信支付参数生成器</param>
    public static TParameterBuilder NonceStr<TParameterBuilder>(this TParameterBuilder builder)
        where TParameterBuilder : IWechatpayParameterBuilder<TParameterBuilder>
    {
        builder.Add(WechatpayConst.NonceStr, Id.Guid());
        return builder;
    }

    /// <summary>
    /// 设置随机字符串
    /// </summary>
    /// <typeparam name="TParameterBuilder">微信支付参数生成器</typeparam>
    /// <param name="builder">微信支付参数生成器</param>
    /// <param name="nonceStr">随机字符串</param>
    public static TParameterBuilder NonceStr<TParameterBuilder>(this TParameterBuilder builder, string nonceStr)
        where TParameterBuilder : IWechatpayParameterBuilder<TParameterBuilder>
    {
        builder.Add(WechatpayConst.NonceStr, nonceStr);
        return builder;
    }

    #endregion

    #region SignType(设置签名类型)

    /// <summary>
    /// 设置签名类型
    /// </summary>
    /// <typeparam name="TParameterBuilder">微信支付参数生成器</typeparam>
    /// <param name="builder">微信支付参数生成器</param>
    /// <param name="type">签名类型</param>
    public static TParameterBuilder SignType<TParameterBuilder>(this TParameterBuilder builder, string type)
        where TParameterBuilder : IWechatpayParameterBuilder<TParameterBuilder>
    {
        builder.Add(WechatpayConst.SignType, type);
        return builder;
    }

    #endregion
}