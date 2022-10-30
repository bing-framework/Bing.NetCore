namespace Bing.Http;

/// <summary>
/// 远程服务错误响应
/// </summary>
public class RemoteServiceErrorResponse
{
    /// <summary>
    /// 远程服务错误信息
    /// </summary>
    public RemoteServiceErrorInfo Error { get; set; }

    /// <summary>
    /// 初始化一个<see cref="RemoteServiceErrorResponse"/>类型的实例
    /// </summary>
    /// <param name="error">远程服务错误信息</param>
    public RemoteServiceErrorResponse(RemoteServiceErrorInfo error) => Error = error;
}
