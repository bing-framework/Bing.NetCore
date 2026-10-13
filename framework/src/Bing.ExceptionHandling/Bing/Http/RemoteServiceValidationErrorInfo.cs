namespace Bing.Http;

/// <summary>
/// 表示远程服务返回的单项字段验证错误。
/// </summary>
[Serializable]
public class RemoteServiceValidationErrorInfo
{
    /// <summary>
    /// 获取或设置验证失败的说明消息。
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 获取或设置触发该验证错误的成员名称列表，可以是字段或属性名。
    /// </summary>
    public string[] Members{ get; set; }

    /// <summary>
    /// 初始化一个 <see cref="RemoteServiceValidationErrorInfo"/> 类型的实例。
    /// </summary>
    public RemoteServiceValidationErrorInfo(){}

    /// <summary>
    /// 初始化一个 <see cref="RemoteServiceValidationErrorInfo"/> 类型的实例。
    /// </summary>
    /// <param name="message">验证失败的说明消息。</param>
    public RemoteServiceValidationErrorInfo(string message) => Message = message;

    /// <summary>
    /// 初始化一个 <see cref="RemoteServiceValidationErrorInfo"/> 类型的实例。
    /// </summary>
    /// <param name="message">验证失败的说明消息。</param>
    /// <param name="members">触发验证错误的字段或属性名称列表。</param>
    public RemoteServiceValidationErrorInfo(string message, string[] members) : this(message) => Members = members;

    /// <summary>
    /// 初始化一个 <see cref="RemoteServiceValidationErrorInfo"/> 类型的实例。
    /// </summary>
    /// <param name="message">验证失败的说明消息。</param>
    /// <param name="member">触发验证错误的字段或属性名称。</param>
    public RemoteServiceValidationErrorInfo(string message, string member) : this(message, new[] {member}) { }
}
