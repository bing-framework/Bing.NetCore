using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebApiClientCore;

namespace Bing.DynamicApi.WebApiClient;

/// <summary>
/// 注册 Bing 动态 API 的 WebApiClientCore 客户端。
/// </summary>
public static class BingWebApiClientServiceCollectionExtensions
{
    /// <summary>
    /// 注册独立的声明式 HTTP API 接口。
    /// </summary>
    /// <typeparam name="TApi">使用 WebApiClient 特性声明路由和参数绑定的接口。</typeparam>
    /// <param name="services">依赖注入服务集合。</param>
    /// <param name="configure">基础地址和上下文请求头配置。</param>
    /// <returns>接口对应的 WebApiClient 客户端构建器。</returns>
    /// <remarks>可通过返回的构建器继续配置认证、日志和其他处理器。</remarks>
    /// <exception cref="ArgumentNullException">服务集合或配置委托为空。</exception>
    /// <exception cref="ArgumentException">契约不是接口或请求头名称无效。</exception>
    /// <exception cref="InvalidOperationException">基础地址无效。</exception>
    public static IHttpClientBuilder AddBingWebApiClient<TApi>(
        this IServiceCollection services,
        Action<BingWebApiClientOptions> configure)
        where TApi : class
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        if (!typeof(TApi).IsInterface)
            throw new ArgumentException($"WebApiClient 类型 '{typeof(TApi).FullName}' 必须是接口。", nameof(TApi));

        var options = new BingWebApiClientOptions();
        configure(options);
        options.ValidateHeaderNames();
        var baseAddress = options.GetValidatedBaseAddress();

        services.TryAddSingleton<IBingWebApiClientRequestContextAccessor, EmptyBingWebApiClientRequestContextAccessor>();

        var builder = services.AddHttpApi<TApi>();
        builder.ConfigureHttpApi(httpApiOptions => httpApiOptions.HttpHost ??= baseAddress);
        return builder.AddHttpMessageHandler(serviceProvider => new BingWebApiClientRequestContextHandler(
                serviceProvider.GetRequiredService<IBingWebApiClientRequestContextAccessor>(),
                options.TenantHeaderName,
                options.CorrelationIdHeaderName,
                options.LanguageHeaderName));
    }
}
