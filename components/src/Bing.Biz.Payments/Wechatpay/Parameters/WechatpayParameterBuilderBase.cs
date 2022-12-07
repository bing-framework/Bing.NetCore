using System.Xml;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Signatures;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Utils.Parameters;

namespace Bing.Biz.Payments.Wechatpay.Parameters;

/// <summary>
/// 微信支付参数生成器基类
/// </summary>
/// <typeparam name="TParameterBuilder">微信支付参数生成器</typeparam>
public abstract class WechatpayParameterBuilderBase<TParameterBuilder>
    where TParameterBuilder : IWechatpayParameterBuilder<TParameterBuilder>
{
    /// <summary>
    /// 参数生成器
    /// </summary>
    private readonly ParameterBuilder _builder;

    /// <summary>
    /// 签名参数名称
    /// </summary>
    private string _signName;

    /// <summary>
    /// 初始化一个<see cref="WechatpayParameterBuilderBase{TParameterBuilder}"/>类型的实例
    /// </summary>
    /// <param name="config">微信支付配置</param>
    protected WechatpayParameterBuilderBase(WechatpayConfig config)
    {
        config.CheckNull(nameof(config));
        Config = config;
        _builder = new ParameterBuilder();
    }

    /// <summary>
    /// 微信支付配置
    /// </summary>
    public WechatpayConfig Config { get; }

    /// <summary>
    /// 添加参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    public virtual TParameterBuilder Add(string name, object value)
    {
        _builder.Add(name, value);
        return This();
    }

    /// <summary>
    /// 获取签名
    /// </summary>
    public virtual string GetSign() => SignManagerFactory.Create(Config, _builder).Sign();

    /// <summary>
    /// 设置签名参数名称
    /// </summary>
    /// <param name="name">参数名</param>
    public virtual TParameterBuilder SignParamName(string name)
    {
        _signName = name;
        return This();
    }

    /// <summary>
    /// 获取Xml结果，包含签名
    /// </summary>
    public virtual string ToXml() => ToXmlDocument(GetSignBuilder()).OuterXml;

    /// <summary>
    /// 获取Xml文档
    /// </summary>
    /// <param name="builder">参数生成器</param>
    private XmlDocument ToXmlDocument(ParameterBuilder builder)
    {
        var xml = new Xml();
        foreach (var param in builder.GetDictionary())
            AddNode(xml, param.Key, param.Value);
        return xml.Document;
    }

    /// <summary>
    /// 添加Xml节点
    /// </summary>
    /// <param name="xml">Xml操作</param>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    private void AddNode(Xml xml, string key, object value)
    {
        if (key.SafeString().ToLower() == WechatpayConst.TotalFee)
        {
            xml.AddNode(key, value);
            return;
        }

        xml.AddCDataNode(value, key);
    }

    /// <summary>
    /// 获取签名的参数生成器
    /// </summary>
    private ParameterBuilder GetSignBuilder()
    {
        var builder = new ParameterBuilder(_builder);
        builder.Add(GetSingName(), GetSign());
        return builder;
    }

    /// <summary>
    /// 获取签名参数名称
    /// </summary>
    private string GetSingName() => _signName.IsEmpty() ? WechatpayConst.Sign : _signName;

    /// <summary>
    /// 获取Xml结果，不包含签名
    /// </summary>
    public virtual string ToXmlNoContainsSign() => ToXmlDocument(_builder).OuterXml;

    /// <summary>
    /// 获取Json结果，包含签名
    /// </summary>
    public virtual string ToJson() => GetSignBuilder().ToJson();

    /// <summary>
    /// 输出结果
    /// </summary>
    public override string ToString() => ToXml();

    /// <summary>
    /// 返回自身
    /// </summary>
    private TParameterBuilder This() => (TParameterBuilder)(object)this;
}