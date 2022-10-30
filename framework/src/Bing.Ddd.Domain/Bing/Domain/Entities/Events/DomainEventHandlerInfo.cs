using System;
using System.Reflection;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 领域事件处理器信息
/// </summary>
public class DomainEventHandlerInfo
{
    /// <summary>
    /// 类型
    /// </summary>
    private readonly Type _type;

    /// <summary>
    /// 类型
    /// </summary>
    public Type Type => _type;

    /// <summary>
    /// 方法
    /// </summary>
    public MethodInfo Method { get; private set; }

    /// <summary>
    /// 初始化一个<see cref="DomainEventHandlerInfo"/>类型的实例
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="method">方法</param>
    public DomainEventHandlerInfo(Type type, MethodInfo method)
    {
        _type = type;
        Method = method;
    }

    /// <summary>
    /// 获取哈希编码
    /// </summary>
    public override int GetHashCode() => _type.GetHashCode();
}