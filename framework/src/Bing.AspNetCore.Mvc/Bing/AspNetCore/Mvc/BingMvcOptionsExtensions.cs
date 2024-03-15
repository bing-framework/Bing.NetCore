using Bing.AspNetCore.Mvc.ContentFormatters;
using Bing.Content;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// MVC配置选项(<see cref="MvcOptions"/>) 扩展
/// </summary>
public static class BingMvcOptionsExtensions
{
    /// <summary>
    /// 为MVC配置选项扩展自定义设置。
    /// </summary>
    /// <param name="options">MVC配置选项</param>
    /// <param name="services">服务集合</param>
    public static void AddBing(this MvcOptions options, IServiceCollection services)
    {
        AddModelBinders(options);
        AddMetadataProviders(options, services);
        AddFormatters(options);
    }

    /// <summary>
    /// 添加自定义输出格式化器
    /// </summary>
    /// <param name="options">MVC配置选项</param>
    private static void AddFormatters(MvcOptions options)
    {
        options.OutputFormatters.Insert(0, new RemoteStreamContentOutputFormatter());
    }

    /// <summary>
    /// 添加模型绑定器提供程序
    /// </summary>
    /// <param name="options">MVC配置选项</param>
    private static void AddModelBinders(MvcOptions options)
    {
        options.ModelBinderProviders.Insert(2, new BingRemoteStreamContentModelBinderProvider());
    }

    /// <summary>
    /// 添加模型元数据细节提供程序
    /// </summary>
    /// <param name="options">MVC配置选项</param>
    /// <param name="services">服务集合</param>
    private static void AddMetadataProviders(MvcOptions options, IServiceCollection services)
    {
        // 为 IRemoteStreamContent 类型及其集合指定表单文件作为绑定源
        options.ModelMetadataDetailsProviders.Add(new BindingSourceMetadataProvider(typeof(IRemoteStreamContent), BindingSource.FormFile));
        options.ModelMetadataDetailsProviders.Add(new BindingSourceMetadataProvider(typeof(IEnumerable<IRemoteStreamContent>), BindingSource.FormFile));
        // 为 RemoteStreamContent 类型及其集合指定表单文件作为绑定源
        options.ModelMetadataDetailsProviders.Add(new BindingSourceMetadataProvider(typeof(RemoteStreamContent), BindingSource.FormFile));
        options.ModelMetadataDetailsProviders.Add(new BindingSourceMetadataProvider(typeof(IEnumerable<RemoteStreamContent>), BindingSource.FormFile));
        // 禁止对 IRemoteStreamContent 和 RemoteStreamContent 类型执行子属性验证
        options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(IRemoteStreamContent)));
        options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(RemoteStreamContent)));
    }
}
