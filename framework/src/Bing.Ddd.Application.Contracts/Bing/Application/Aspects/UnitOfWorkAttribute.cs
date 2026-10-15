namespace Bing.Application.Aspects;

/// <summary>
/// 标记方法或类型需要在调用成功后提交工作单元。
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface,
    AllowMultiple = false,
    Inherited = true)]
public sealed class UnitOfWorkAttribute : Attribute
{
    /// <summary>
    /// 获取或设置拦截器排序值。
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 获取或设置工作单元拦截范围。
    /// </summary>
    public BingUnitOfWorkScope Scope { get; set; } = BingUnitOfWorkScope.Aspect;
}
