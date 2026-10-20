namespace Bing.Emailing;

/// <summary>
/// 表示待发送的电子邮件消息。
/// </summary>
public class EmailBox
{
    /// <summary>
    /// 获取或设置邮件附件集合。
    /// </summary>
    /// <remarks>
    /// 默认初始化为空集合。
    /// </remarks>
    public List<IAttachment> Attachments { get; set; } = new List<IAttachment>();

    /// <summary>
    /// 获取或设置邮件正文。
    /// </summary>
    /// <remarks>
    /// 未设置时可以为 <c>null</c>。
    /// </remarks>
    public string Body { get; set; }

    /// <summary>
    /// 获取或设置抄送收件人地址集合。
    /// </summary>
    /// <remarks>
    /// 默认初始化为空集合。
    /// </remarks>
    public List<string> Cc { get; set; } = new List<string>();

    /// <summary>
    /// 获取或设置正文是否按 HTML 内容发送。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>true</c>。
    /// </remarks>
    public bool IsBodyHtml { get; set; } = true;

    /// <summary>
    /// 获取或设置邮件主题。
    /// </summary>
    /// <remarks>
    /// 未设置时可以为 <c>null</c>。
    /// </remarks>
    public string Subject { get; set; }

    /// <summary>
    /// 获取或设置主收件人地址集合。
    /// </summary>
    /// <remarks>
    /// 默认初始化为空集合。
    /// </remarks>
    public List<string> To { get; set; } = new List<string>();

    /// <summary>
    /// 获取或设置密送收件人地址集合。
    /// </summary>
    /// <remarks>
    /// 密送地址不应向其他收件人暴露。
    /// 默认初始化为空集合。
    /// </remarks>
    public List<string> Bcc { get; set; } = new List<string>();
}