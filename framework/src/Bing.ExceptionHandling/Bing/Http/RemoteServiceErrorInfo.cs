namespace Bing.Http;

/// <summary>
/// 表示远程服务调用失败时返回的结构化错误信息。
/// </summary>
[Serializable]
public class RemoteServiceErrorInfo
{
    /// <summary>
    /// 获取或设置可供客户端识别的错误码。
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// 获取或设置面向调用方的错误消息。
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 获取或设置错误的补充详情；不应包含未脱敏的敏感信息。
    /// </summary>
    public string Details { get; set; }

    /// <summary>
    /// 获取或设置与错误关联的附加数据字典。
    /// </summary>
    public IDictionary Data { get; set; }

    /// <summary>
    /// 获取或设置远程服务返回的字段级验证错误列表。
    /// </summary>
    public RemoteServiceValidationErrorInfo[] ValidationErrors { get; set; }

    /// <summary>
    /// 初始化 <see cref="RemoteServiceErrorInfo"/> 的空实例。
    /// </summary>
    public RemoteServiceErrorInfo() { }

    /// <summary>
    /// 使用消息、详情、错误码和附加数据初始化 <see cref="RemoteServiceErrorInfo"/> 的实例。
    /// </summary>
    /// <param name="message">面向调用方的错误消息。</param>
    /// <param name="details">错误补充详情，可为空。</param>
    /// <param name="code">可选的客户端错误码。</param>
    /// <param name="data">可选的附加错误数据。</param>
    public RemoteServiceErrorInfo(string message, string details = null, string code = null, IDictionary data = null)
    {
        Message = message;
        Details = details;
        Code = code;
        Data = data;
    }
}
