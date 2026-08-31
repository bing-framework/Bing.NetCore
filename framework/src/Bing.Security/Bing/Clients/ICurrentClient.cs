namespace Bing.Clients;

/// <summary>
/// 定义当前安全主体对应客户端的信息访问契约。
/// </summary>
public interface ICurrentClient
{
    /// <summary>
    /// 获取当前客户端标识；未找到时返回 <see langword="null"/>。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 获取当前客户端是否存在有效标识。
    /// </summary>
    /// <returns>客户端标识不为空时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    bool IsAuthenticated { get; }
}