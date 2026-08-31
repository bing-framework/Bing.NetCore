namespace Bing.Application.Dtos;

/// <summary>
/// 定义具有字符串标识的数据传输对象契约。
/// </summary>
public interface IKey
{
    /// <summary>
    /// 获取或设置对象的字符串标识。
    /// </summary>
    string Id { get; set; }
}