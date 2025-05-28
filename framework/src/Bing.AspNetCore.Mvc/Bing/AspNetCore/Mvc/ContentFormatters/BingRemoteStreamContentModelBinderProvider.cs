using Bing.Content;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bing.AspNetCore.Mvc.ContentFormatters;

/// <summary>
/// 远程流内容 - 模型绑定器提供程序
/// </summary>
/// <remarks>
/// 用于处理 <see cref="RemoteStreamContent"/> 类型及其接口 <see cref="IRemoteStreamContent"/> 的绑定。
/// </remarks>
public class BingRemoteStreamContentModelBinderProvider : IModelBinderProvider
{
    /// <summary>
    /// 根据绑定上下文返回一个模型绑定器，用于绑定 <see cref="RemoteStreamContent"/> 或 <see cref="IRemoteStreamContent"/> 类型的模型。
    /// </summary>
    /// <param name="context">模型绑定器提供器上下文，包含绑定目标类型的元数据。</param>
    /// <returns>
    /// 如果目标类型为 <see cref="RemoteStreamContent"/> 或其接口 <see cref="IRemoteStreamContent"/>，则返回相应的模型绑定器；
    /// 如果类型不匹配，返回 null。
    /// </returns>
    /// <exception cref="ArgumentNullException">如果 context 参数为 null，则抛出此异常。</exception>
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (context.Metadata.ModelType == typeof(RemoteStreamContent) || typeof(IEnumerable<RemoteStreamContent>).IsAssignableFrom(context.Metadata.ModelType))
            return new BingRemoteStreamContentModelBinder<RemoteStreamContent>();
        if (context.Metadata.ModelType == typeof(IRemoteStreamContent) || typeof(IEnumerable<IRemoteStreamContent>).IsAssignableFrom(context.Metadata.ModelType))
            return new BingRemoteStreamContentModelBinder<IRemoteStreamContent>();
        return null;
    }
}
