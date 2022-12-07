namespace Bing.EventBus;

/// <summary>
/// 可继承的泛型参数事件数据
/// </summary>
public interface IEventDataWithInheritableGenericArgument
{
    /// <summary>
    /// 获取构造函数参数
    /// </summary>
    object[] GetConstructorArgs();
}