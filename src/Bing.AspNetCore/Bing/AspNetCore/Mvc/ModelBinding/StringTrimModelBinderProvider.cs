using Bing.Helpers;
using Bing.Utils.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bing.AspNetCore.Mvc.ModelBinding
{
    /// <summary>
    /// <see cref="StringTrimModelBinder"/>提供程序。提供对字符串前后空白进行Trim操作的模型绑定能力
    /// </summary>
    public class StringTrimModelBinderProvider : IModelBinderProvider
    {
        /// <summary>
        /// 获取绑定器
        /// </summary>
        /// <param name="context">模型绑定提供程序上下文</param>
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            Check.NotNull(context, nameof(context));
            if (context.Metadata.UnderlyingOrModelType == typeof(string))
                return new StringTrimModelBinder();
            return null;
        }
    }
}
