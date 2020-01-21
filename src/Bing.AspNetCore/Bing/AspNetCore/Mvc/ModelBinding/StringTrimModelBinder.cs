using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Bing.Utils.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bing.AspNetCore.Mvc.ModelBinding
{
    /// <summary>
    /// 字符串前后空白进行Trim操作的模型绑定器
    /// </summary>
    public class StringTrimModelBinder : IModelBinder
    {
        /// <summary>
        /// 绑定模型
        /// </summary>
        /// <param name="bindingContext">模型绑定上下文</param>
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            Check.NotNull(bindingContext, nameof(bindingContext));
            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
                return Task.CompletedTask;
            var underlyingOrModelType = bindingContext.ModelMetadata.UnderlyingOrModelType;
            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

            try
            {
                var firstValue = valueProviderResult.FirstValue;
                object model;
                if (string.IsNullOrWhiteSpace(firstValue))
                    model = null;
                else
                {
                    if (underlyingOrModelType != typeof(string))
                        throw new MulticastNotSupportedException();
                    model = firstValue.Trim();
                }
                bindingContext.Result = ModelBindingResult.Success(model);
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                var exception = e;
                if (!(exception is FormatException) && exception.InnerException != null)
                    exception = ExceptionDispatchInfo.Capture(exception.InnerException).SourceException;
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, exception,
                    bindingContext.ModelMetadata);
                return Task.CompletedTask;
            }
        }
    }
}
