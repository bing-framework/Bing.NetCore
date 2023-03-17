namespace Bing.Threading;

/// <summary>
/// 环境数据上下文
/// </summary>
/// <remarks>
/// 应用程序中共享上下文信息的模式，允许从一个地方设置和存储上下文，在整个应用程序中使用该上下文，而无需显示地将其传递给每个方法和类。
/// </remarks>
public interface IAmbientDataContext
{
    /// <summary>
    /// 设置数据
    /// </summary>
    /// <param name="key">键名</param>
    /// <param name="value">对象值</param>
    void SetData(string key, object value);

    /// <summary>
    /// 获取数据
    /// </summary>
    /// <param name="key">键名</param>
    /// <returns>对象值</returns>
    object GetData(string key);
}
