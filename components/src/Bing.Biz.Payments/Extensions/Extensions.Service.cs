using System;
using Bing.Biz.Payments.Alipay.Abstractions;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Alipay.Services;
using Bing.Biz.Payments.Factories;
using Bing.Biz.Payments.Wechatpay.Abstractions;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Biz.Payments.Extensions;

/// <summary>
/// 支付扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 注册支付操作
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static void AddPay(this IServiceCollection services, Action<PayOptions> setupAction)
    {
        var options = new PayOptions();
        setupAction?.Invoke(options);
        services.TryAddSingleton<IAlipayConfigProvider>(new AlipayConfigProvider(options.AlipayOptions));
        services.TryAddSingleton<IWechatpayConfigProvider>(new WechatpayConfigProvider(options.WechatpayOptions));
        services.TryAddScoped<IPayFactory, PayFactory>();
        services.TryAddScoped<IAlipayNotifyService, AlipayNotifyService>();
        services.TryAddScoped<IAlipayReturnService, AlipayReturnService>();
        services.TryAddScoped<IWechatpayNotifyService, WechatpayNotifyService>();
    }

    /// <summary>
    /// 注册支付操作
    /// </summary>
    /// <typeparam name="TAlipayConfigProvider">支付宝配置提供器</typeparam>
    /// <typeparam name="TWechatpayConfigProvider">微信支付配置提供器</typeparam>
    /// <param name="services">服务集合</param>
    public static void AddPay<TAlipayConfigProvider, TWechatpayConfigProvider>(this IServiceCollection services)
        where TAlipayConfigProvider : class, IAlipayConfigProvider
        where TWechatpayConfigProvider : class, IWechatpayConfigProvider
    {
        services.TryAddScoped<IAlipayConfigProvider, TAlipayConfigProvider>();
        services.TryAddScoped<IWechatpayConfigProvider, TWechatpayConfigProvider>();
        services.TryAddScoped<IPayFactory, PayFactory>();
        services.TryAddScoped<IAlipayNotifyService, AlipayNotifyService>();
        services.TryAddScoped<IAlipayReturnService, AlipayReturnService>();
        services.TryAddScoped<IWechatpayNotifyService, WechatpayNotifyService>();
    }

    /// <summary>
    /// 注册支付宝支付操作
    /// </summary>
    /// <typeparam name="TAlipayConfigProvider">支付宝配置提供器</typeparam>
    /// <param name="services">服务集合</param>
    public static void AddAlipay<TAlipayConfigProvider>(this IServiceCollection services)
        where TAlipayConfigProvider : class, IAlipayConfigProvider
    {
        services.TryAddScoped<IAlipayConfigProvider, TAlipayConfigProvider>();
        services.TryAddScoped<IPayFactory, PayFactory>();
        services.TryAddScoped<IAlipayNotifyService, AlipayNotifyService>();
        services.TryAddScoped<IAlipayReturnService, AlipayReturnService>();
    }

    /// <summary>
    /// 注册微信支付操作
    /// </summary>
    /// <typeparam name="TWechatpayConfigProvider">微信支付配置提供器</typeparam>
    /// <param name="services">服务集合</param>
    public static void AddWechatpay<TWechatpayConfigProvider>(this IServiceCollection services)
        where TWechatpayConfigProvider : class, IWechatpayConfigProvider
    {
        services.TryAddScoped<IWechatpayConfigProvider, TWechatpayConfigProvider>();
        services.TryAddScoped<IPayFactory, PayFactory>();
        services.TryAddScoped<IWechatpayNotifyService, WechatpayNotifyService>();
    }
}