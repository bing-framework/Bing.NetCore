namespace Bing.EventBus;

/// <summary>
/// 可继承的泛型参数事件数据
/// </summary>
public interface IEventDataWithInheritableGenericArgument
{
    /// <summary>
    /// 获取构造函数参数
    /// </summary>
    /// <returns>用于创建基础泛型事件数据的构造函数参数数组。</returns>
    object[] GetConstructorArgs();
}