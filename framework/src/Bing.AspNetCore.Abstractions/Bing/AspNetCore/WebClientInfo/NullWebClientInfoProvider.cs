namespace Bing.AspNetCore.WebClientInfo;

/// <summary>
/// 提供一个默认实现的 Web 客户端信息提供者，返回空值或默认值。
/// </summary>
public class NullWebClientInfoProvider : IWebClientInfoProvider
{
    /// <inheritdoc />
    public virtual string? BrowserInfo => string.Empty;

    /// <inheritdoc />
    public virtual string? ClientIpAddress => string.Empty;

    /// <inheritdoc />
    public virtual string? DeviceInfo => string.Empty;
}
