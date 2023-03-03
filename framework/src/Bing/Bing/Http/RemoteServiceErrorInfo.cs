namespace Bing.Http;

/// <summary>
/// 远程服务错误信息
/// </summary>
[Serializable]
public class RemoteServiceErrorInfo
{
    /// <summary>
    /// 错误码
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 错误详情
    /// </summary>
    public string Details { get; set; }

    /// <summary>
    /// 数据
    /// </summary>
    public IDictionary Data { get; set; }

    /// <summary>
    /// 验证错误
    /// </summary>
    public RemoteServiceValidationErrorInfo[] ValidationErrors { get; set; }

    /// <summary>
    /// 初始化一个<see cref="RemoteServiceErrorInfo"/>类型的实例
    /// </summary>
    public RemoteServiceErrorInfo() { }

    /// <summary>
    /// 初始化一个<see cref="RemoteServiceErrorInfo"/>类型的实例
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="details">错误详情</param>
    /// <param name="code">错误码</param>
    /// <param name="data">数据</param>
    public RemoteServiceErrorInfo(string message, string details = null, string code = null, IDictionary data = null)
    {
        Message = message;
        Details = details;
        Code = code;
        Data = data;
    }
}
