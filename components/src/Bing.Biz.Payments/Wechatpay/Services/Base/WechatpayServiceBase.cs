using System.Text;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters;
using Bing.Biz.Payments.Wechatpay.Results;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Biz.Payments.Wechatpay.Services.Base;

/// <summary>
/// 微信支付服务
/// </summary>
/// <typeparam name="TRequest">请求参数类型</typeparam>
/// <typeparam name="TParameterBuilder">微信支付参数生成器</typeparam>
public abstract class WechatpayServiceBase<TRequest, TParameterBuilder>
    where TRequest : IVerifyModel
    where TParameterBuilder : IWechatpayParameterBuilder
{
    /// <summary>
    /// 微信支付配置提供程序
    /// </summary>
    protected readonly IWechatpayConfigProvider ConfigProvider;

    /// <summary>
    /// 日志工厂
    /// </summary>
    protected ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// 日志
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// 初始化一个<see cref="WechatpayServiceBase{TRequest,TParameterBuilder}"/>类型的实例
    /// </summary>
    /// <param name="configProvider">微信支付配置提供程序</param>
    /// <param name="loggerFactory">日志工厂</param>
    protected WechatpayServiceBase(IWechatpayConfigProvider configProvider, ILoggerFactory loggerFactory)
    {
        configProvider.CheckNull(nameof(configProvider));
        ConfigProvider = configProvider;
        LoggerFactory = loggerFactory;
        Logger = loggerFactory?.CreateLogger(GetType().FullName) ?? NullLogger.Instance;
    }

    /// <summary>
    /// 是否发送请求
    /// </summary>
    public bool IsSend { get; set; } = true;

    /// <summary>
    /// 请求
    /// </summary>
    /// <param name="param">请求参数</param>
    protected virtual async Task<WechatpayResult> Request(TRequest param)
    {
        var config = await ConfigProvider.GetConfigAsync(param);
        Validate(config, param);
        var builder = CreateParameterBuilder(config);
        ConfigBuilder(builder, param);
        return await RequestResult(config, builder);
    }

    /// <summary>
    /// 验证
    /// </summary>
    /// <param name="config">微信支付配置</param>
    /// <param name="param">请求参数</param>
    protected void Validate(WechatpayConfig config, TRequest param)
    {
        config.CheckNull(nameof(config));
        param.CheckNull(nameof(param));
        config.Validate();
        param.Validate();
        ValidateParam(param);
    }

    /// <summary>
    /// 验证参数
    /// </summary>
    /// <param name="param">请求参数</param>
    protected virtual void ValidateParam(TRequest param)
    {
    }

    /// <summary>
    /// 创建参数生成器
    /// </summary>
    /// <param name="config">微信支付配置</param>
    protected abstract TParameterBuilder CreateParameterBuilder(WechatpayConfig config);

    /// <summary>
    /// 配置参数生成器
    /// </summary>
    /// <param name="builder">微信支付参数生成器</param>
    /// <param name="param">请求参数</param>
    protected abstract void ConfigBuilder(TParameterBuilder builder, TRequest param);

    /// <summary>
    /// 请求结果
    /// </summary>
    /// <param name="config">微信支付配置</param>
    /// <param name="builder">微信支付参数生成器</param>
    protected virtual async Task<WechatpayResult> RequestResult(WechatpayConfig config, TParameterBuilder builder)
    {
        var response = await SendRequest(config, builder);
        var result = new WechatpayResult(ConfigProvider, response, config, builder);
        WriteLog(config, builder, result);
        return result;
    }

    /// <summary>
    /// 发送请求
    /// </summary>
    /// <param name="config">微信支付配置</param>
    /// <param name="builder">微信支付参数生成器</param>
    protected virtual async Task<string> SendRequest(WechatpayConfig config, TParameterBuilder builder)
    {
        if (IsSend == false)
            return string.Empty;
        return await Web.Client()
            .Post(GetUrl(config))
            .XmlData(builder.ToXml())
            .ResultAsync();
    }

    /// <summary>
    /// 获取接口地址
    /// </summary>
    /// <param name="config">微信支付配置</param>
    protected abstract string GetUrl(WechatpayConfig config);

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="config">微信支付配置</param>
    /// <param name="builder">微信支付参数生成器</param>
    /// <param name="result">微信支付结果</param>
    protected void WriteLog(WechatpayConfig config, TParameterBuilder builder, WechatpayResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[微信支付]支付网关：{GetUrl(config)}");
        sb.AppendLine($"请求参数: [{builder.ToXml()}]");
        sb.AppendLine($"返回结果: [{result.GetParams()}]");
        sb.AppendLine($"原始响应: [{result.Raw}]");
        Logger.LogTrace(sb.ToString());
    }
}
