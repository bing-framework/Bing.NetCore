namespace Bing.Auditing;

/// <summary>
/// 标记不参与自动审计的类型或成员。
/// </summary>
/// <remarks>
/// 标记此特性的类、方法或属性会被审计框架忽略，不会自动填充创建、修改、删除时间及操作人字段。
/// 定义在 Bing.Auditing.Contracts 中，使领域层可直接引用而无需依赖审计实现。
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
public class DisableAuditingAttribute : Attribute
{
}
