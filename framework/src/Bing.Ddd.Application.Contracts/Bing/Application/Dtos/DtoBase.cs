namespace Bing.Application.Dtos;

/// <summary>
/// 提供应用层数据传输对象的基础标识和请求能力。
/// </summary>
public abstract class DtoBase : RequestBase, IDto
{
    /// <summary>
    /// 获取或设置数据传输对象的字符串标识。
    /// </summary>
    public string Id { get; set; }
}