using Bing.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bing.AspNetCore.Mvc.ModelBinding;

/// <summary>
/// <see cref="StringTrimModelBinder"/>提供程序。提供对字符串前后空白进行Trim操作的模型绑定能力
/// </summary>
public class StringTrimModelBinderProvider : IModelBinderProvider
{
    /// <inheritdoc />
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        Check.NotNull(context, nameof(context));
        if (context.Metadata.UnderlyingOrModelType == typeof(string))
            return new StringTrimModelBinder();
        return null;
    }
}