using System.Reflection;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 保存领域事件处理器的目标类型和处理方法反射信息。
/// </summary>
public class DomainEventHandlerInfo
{
    /// <summary>
    /// 处理器目标类型。
    /// </summary>
    private readonly Type _type;

    /// <summary>
    /// 获取处理器目标类型。
    /// </summary>
    public Type Type => _type;

    /// <summary>
    /// 获取或设置处理领域事件的方法信息。
    /// </summary>
    public MethodInfo Method { get; private set; }

    /// <summary>
    /// 使用处理器类型和方法信息初始化 <see cref="DomainEventHandlerInfo"/> 的实例。
    /// </summary>
    /// <param name="type">注册处理器的目标类型。</param>
    /// <param name="method">用于处理领域事件的方法反射信息。</param>
    public DomainEventHandlerInfo(Type type, MethodInfo method)
    {
        _type = type;
        Method = method;
    }

    /// <summary>
    /// 返回基于处理器目标类型的哈希代码，用于按处理器类型参与集合比较。
    /// </summary>
    /// <returns>处理器目标类型的哈希代码。</returns>
    public override int GetHashCode() => _type.GetHashCode();
}