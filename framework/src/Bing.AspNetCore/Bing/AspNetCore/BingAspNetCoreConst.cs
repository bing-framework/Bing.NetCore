namespace Bing.AspNetCore;

/// <summary>
/// AspNetCore常量
/// </summary>
public static class BingAspNetCoreConst
{
    /// <summary>
    /// 框架默认 API 路由前缀，值为 <c>api</c>。
    /// </summary>
    public const string DefaultApiPrefix = "api";

    /// <summary>
    /// 框架默认集成服务 API 路由前缀，值为 <c>integration-api</c>。
    /// </summary>
    public const string DefaultIntegrationServiceApiPrefix = "integration-api";

    /// <summary>
    /// 标识脚本 nonce 的稳定键名，值为 <c>ScriptNonce</c>。
    /// </summary>
    public const string ScriptNonceKey = "ScriptNonce";
}
