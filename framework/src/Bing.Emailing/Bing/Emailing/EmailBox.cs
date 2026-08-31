namespace Bing.Emailing;

/// <summary>
/// 表示待发送的电子邮件消息。
/// </summary>
public class EmailBox
{
    /// <summary>
    /// 获取或设置邮件附件集合，默认初始化为空集合。
    /// </summary>
    public List<IAttachment> Attachments { get; set; } = new List<IAttachment>();

    /// <summary>
    /// 获取或设置邮件正文；未设置时可以为 <c>null</c>。
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// 获取或设置抄送收件人地址集合，默认初始化为空集合。
    /// </summary>
    public List<string> Cc { get; set; } = new List<string>();

    /// <summary>
    /// 获取或设置正文是否按 HTML 内容发送，默认值为 <c>true</c>。
    /// </summary>
    public bool IsBodyHtml { get; set; } = true;

    /// <summary>
    /// 获取或设置邮件主题；未设置时可以为 <c>null</c>。
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// 获取或设置主收件人地址集合，默认初始化为空集合。
    /// </summary>
    public List<string> To { get; set; } = new List<string>();

    /// <summary>
    /// 获取或设置密送收件人地址集合，默认初始化为空集合。
    /// </summary>
    /// <remarks>密送地址不应向其他收件人暴露。</remarks>
    public List<string> Bcc { get; set; } = new List<string>();
}