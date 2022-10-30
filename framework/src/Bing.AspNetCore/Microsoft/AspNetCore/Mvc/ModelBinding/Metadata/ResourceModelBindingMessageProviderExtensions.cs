using System;

namespace Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

/// <summary>
/// 模型绑定消息提供程序(<see cref="ModelBindingMessageProvider"/>)扩展
/// </summary>
public static class ResourceModelBindingMessageProviderExtensions
{
    /// <summary>
    /// 应用翻译资源
    /// </summary>
    /// <param name="messageProvider">模型绑定消息提供程序</param>
    public static void UseTranslatedResources(this DefaultModelBindingMessageProvider messageProvider)
    {
        messageProvider.SetMissingBindRequiredValueAccessor(One(MvcModelBindingResource.MissingBindRequiredValueAccessor));
        messageProvider.SetMissingKeyOrValueAccessor(Zero(MvcModelBindingResource.MissingKeyOrValueAccessor));
        messageProvider.SetMissingRequestBodyRequiredValueAccessor(Zero(MvcModelBindingResource.MissingRequestBodyRequiredValueAccessor));
        messageProvider.SetValueMustNotBeNullAccessor(One(MvcModelBindingResource.ValueMustNotBeNullAccessor));
        messageProvider.SetAttemptedValueIsInvalidAccessor(Two(MvcModelBindingResource.AttemptedValueIsInvalidAccessor));
        messageProvider.SetNonPropertyAttemptedValueIsInvalidAccessor(One(MvcModelBindingResource.NonPropertyAttemptedValueIsInvalidAccessor));
        messageProvider.SetUnknownValueIsInvalidAccessor(One(MvcModelBindingResource.UnknownValueIsInvalidAccessor));
        messageProvider.SetNonPropertyUnknownValueIsInvalidAccessor(Zero(MvcModelBindingResource.NonPropertyUnknownValueIsInvalidAccessor));
        messageProvider.SetValueIsInvalidAccessor(One(MvcModelBindingResource.ValueIsInvalidAccessor));
        messageProvider.SetValueMustBeANumberAccessor(One(MvcModelBindingResource.ValueMustBeANumberAccessor));
        messageProvider.SetNonPropertyValueMustBeANumberAccessor(Zero(MvcModelBindingResource.NonPropertyValueMustBeANumberAccessor));
    }

    /// <summary>
    /// 0个参字符串格式化
    /// </summary>
    private static Func<string> Zero(string resource) => () => resource;

    /// <summary>
    /// 1个参数字符串格式化
    /// </summary>
    private static Func<string, string> One(string resource) => arg => string.Format(resource, arg);

    /// <summary>
    /// 2个参数字符串格式化
    /// </summary>
    private static Func<string, string, string> Two(string resource) => (arg1, arg2) => string.Format(resource, arg1, arg2);
}