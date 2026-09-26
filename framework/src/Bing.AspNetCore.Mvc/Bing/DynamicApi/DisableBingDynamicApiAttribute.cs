using System;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 阻止应用服务或方法被动态 API 暴露。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface, Inherited = true)]
public sealed class DisableBingDynamicApiAttribute : Attribute
{
}
