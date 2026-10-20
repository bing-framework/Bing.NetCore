using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 按契约参数名称绑定动态 API 路径值。
/// </summary>
internal sealed class BingDynamicApiRouteValueModelBinder : IModelBinder
{
    /// <inheritdoc />
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = GetRouteValue(bindingContext);
        if (value.Value == ValueProviderResult.None)
            return Task.CompletedTask;

        var modelName = value.Key;
        if (!string.IsNullOrEmpty(modelName))
            bindingContext.ModelState.SetModelValue(modelName, value.Value);

        try
        {
            var modelType = Nullable.GetUnderlyingType(bindingContext.ModelType) ?? bindingContext.ModelType;
            var rawValue = value.Value.FirstValue;
            object convertedValue;
            if (modelType == typeof(object) || modelType == typeof(string))
            {
                convertedValue = rawValue;
            }
            else if (string.IsNullOrEmpty(rawValue) && Nullable.GetUnderlyingType(bindingContext.ModelType) != null)
            {
                convertedValue = null;
            }
            else
            {
                var converter = TypeDescriptor.GetConverter(modelType);
                convertedValue = converter.CanConvertFrom(typeof(string))
                    ? converter.ConvertFrom(null, CultureInfo.InvariantCulture, rawValue)
                    : Convert.ChangeType(rawValue, modelType, CultureInfo.InvariantCulture);
            }

            bindingContext.Result = ModelBindingResult.Success(convertedValue);
        }
        catch (Exception exception) when (exception is FormatException || exception is NotSupportedException
                                          || exception is ArgumentException || exception is OverflowException)
        {
            bindingContext.ModelState.TryAddModelError(
                value.Key ?? bindingContext.ModelName ?? string.Empty,
                exception,
                bindingContext.ModelMetadata);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 查找参数对应的路径值。
    /// </summary>
    /// <param name="bindingContext">当前模型绑定上下文。</param>
    /// <returns>匹配的名称和值；未匹配时值为 ValueProviderResult.None。</returns>
    private static KeyValuePair<string, ValueProviderResult> GetRouteValue(ModelBindingContext bindingContext)
    {
        var descriptorParameter = bindingContext.ActionContext.ActionDescriptor.Parameters
            .FirstOrDefault(parameter => string.Equals(
                parameter.Name,
                bindingContext.ModelMetadata.ParameterName,
                StringComparison.OrdinalIgnoreCase));
        var candidateNames = new[]
        {
            bindingContext.ModelName,
            bindingContext.ModelMetadata.BinderModelName,
            descriptorParameter?.BindingInfo?.BinderModelName,
            bindingContext.ModelMetadata.ParameterName,
        }.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var name in candidateNames)
        {
            var value = bindingContext.ValueProvider.GetValue(name);
            if (value != ValueProviderResult.None)
                return new KeyValuePair<string, ValueProviderResult>(name, value);

            if (bindingContext.ActionContext.RouteData.Values.TryGetValue(name, out var routeValue)
                && routeValue != null)
            {
                return new KeyValuePair<string, ValueProviderResult>(name, new ValueProviderResult(
                    Convert.ToString(routeValue, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
            }
        }

        return new KeyValuePair<string, ValueProviderResult>(null, ValueProviderResult.None);
    }
}
