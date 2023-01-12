﻿using Bing.Biz.Payments.Alipay.Configs;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Utils.Parameters;
using Bing.Utils.Signatures;
using Bing.Validation;

namespace Bing.Biz.Payments.Alipay.Services.Base;

/// <summary>
/// 支付宝通知服务
/// </summary>
public abstract class AlipayNotifyServiceBase
{
    /// <summary>
    /// 参数生成器
    /// </summary>
    private readonly UrlParameterBuilder _builder;

    /// <summary>
    /// 配置提供器
    /// </summary>
    private readonly IAlipayConfigProvider _configProvider;

    /// <summary>
    /// 是否已加载
    /// </summary>
    private bool _isLoad;

    /// <summary>
    /// 商户订单号
    /// </summary>
    public string OrderId => GetParam(AlipayConst.OutTradeNo);

    /// <summary>
    /// 支付订单号
    /// </summary>
    public string TradeNo => GetParam(AlipayConst.TradeNo);

    /// <summary>
    /// 支付金额
    /// </summary>
    public decimal Money => GetParam(AlipayConst.TotalAmount).ToDecimal();

    /// <summary>
    /// 买家支付宝用户号
    /// </summary>
    public string BuyerId => GetParam(AlipayConst.BuyerId);

    /// <summary>
    /// 签名
    /// </summary>
    public string Sign => GetParam(AlipayConst.Sign);

    /// <summary>
    /// 初始化一个<see cref="AlipayNotifyServiceBase"/>类型的实例
    /// </summary>
    /// <param name="configProvider">配置提供器</param>
    protected AlipayNotifyServiceBase(IAlipayConfigProvider configProvider)
    {
        configProvider.CheckNotNull(nameof(configProvider));
        _configProvider = configProvider;
        _builder = new UrlParameterBuilder();
        _isLoad = false;
    }

    /// <summary>
    /// 获取参数
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="name">参数名</param>
    public T GetParam<T>(string name) => Conv.To<T>(GetParam(name));

    /// <summary>
    /// 获取参数
    /// </summary>
    /// <param name="name">参数名</param>
    public string GetParam(string name)
    {
        Init();
        return _builder.GetValue(name).SafeString();
    }

    /// <summary>
    /// 初始化
    /// </summary>
    private void Init()
    {
        if (_isLoad)
            return;
        Load(_builder);
        _isLoad = true;
        WriteLog();
    }

    /// <summary>
    /// 加载参数
    /// </summary>
    /// <param name="builder">参数生成器</param>
    protected abstract void Load(UrlParameterBuilder builder);

    /// <summary>
    /// 写日志
    /// </summary>
    protected void WriteLog()
    {
        //var log = Log.GetLog(AlipayConst.TraceLogName);
        //if (log.IsTraceEnabled == false)
        //    return;
        //log.Class(GetType().FullName)
        //    .Caption(GetCaption())
        //    .Content("原始参数:")
        //    .Content(GetParams())
        //    .Trace();
    }

    /// <summary>
    /// 获取日志标题
    /// </summary>
    protected virtual string GetCaption()
    {
        return string.Empty;
    }

    /// <summary>
    /// 获取参数集合
    /// </summary>
    public IDictionary<string, string> GetParams()
    {
        Init();
        return _builder.GetDictionary().ToDictionary(t => t.Key, t => t.Value.SafeString());
    }

    /// <summary>
    /// 验证
    /// </summary>
    public async Task<ValidationResultCollection> ValidateAsync()
    {
        Init();
        var isValid = await VerifySign();
        if (isValid == false)
            return new ValidationResultCollection("签名失败");
        return Validate();
    }

    /// <summary>
    /// 验证签名
    /// </summary>
    private async Task<bool> VerifySign()
    {
        var config = await _configProvider.GetConfigAsync();
        var signManager = new SignManager(new SignKey(config.PrivateKey, config.PublicKey), CreateVerifyBuilder());
        return signManager.Verify(Sign);
    }

    /// <summary>
    /// 创建验签生成器
    /// </summary>
    private UrlParameterBuilder CreateVerifyBuilder()
    {
        var builder = new UrlParameterBuilder(_builder);
        builder.Remove(AlipayConst.Sign);
        builder.Remove(AlipayConst.SignType);
        return builder;
    }

    /// <summary>
    /// 验证
    /// </summary>
    protected virtual ValidationResultCollection Validate() => ValidationResultCollection.Success;
}
