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
    /// 初始化 <see cref="RemoteServiceValidationErrorInfo"/> 的空实例。
    /// </summary>
    public RemoteServiceValidationErrorInfo(){}

    /// <summary>
    /// 使用验证消息初始化 <see cref="RemoteServiceValidationErrorInfo"/> 的实例。
    /// </summary>
    /// <param name="message">验证失败的说明消息。</param>
    public RemoteServiceValidationErrorInfo(string message) => Message = message;

    /// <summary>
    /// 使用验证消息和无效成员列表初始化 <see cref="RemoteServiceValidationErrorInfo"/> 的实例。
    /// </summary>
    /// <param name="message">验证失败的说明消息。</param>
    /// <param name="members">触发验证错误的字段或属性名称列表。</param>
    public RemoteServiceValidationErrorInfo(string message, string[] members) : this(message) => Members = members;

    /// <summary>
    /// 使用验证消息和单个无效成员名称初始化 <see cref="RemoteServiceValidationErrorInfo"/> 的实例。
    /// </summary>
    /// <param name="message">验证失败的说明消息。</param>
    /// <param name="member">触发验证错误的字段或属性名称。</param>
    public RemoteServiceValidationErrorInfo(string message, string member) : this(message, new[] {member}) { }
}
