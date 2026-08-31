namespace Bing.Data;

/// <summary>
/// 定义具有字符串数据标识的对象契约。
/// </summary>
public interface IDataKey
{
    /// <summary>
    /// 获取或设置对象的业务标识。
    /// </summary>
    string Id { get; set; }
}
