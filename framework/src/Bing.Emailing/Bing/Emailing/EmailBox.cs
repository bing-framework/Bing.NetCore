using System.Collections.Generic;

namespace Bing.Emailing;

/// <summary>
/// 电子邮件
/// </summary>
public class EmailBox
{
    /// <summary>
    /// 附件列表
    /// </summary>
    public List<IAttachment> Attachments { get; set; } = new List<IAttachment>();

    /// <summary>
    /// 正文
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// 抄送人
    /// </summary>
    public List<string> Cc { get; set; } = new List<string>();

    /// <summary>
    /// 是否Html内容
    /// </summary>
    public bool IsBodyHtml { get; set; } = true;

    /// <summary>
    /// 主题
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// 收件人
    /// </summary>
    public List<string> To { get; set; } = new List<string>();

    /// <summary>
    /// 秘密抄送人
    /// </summary>
    public List<string> Bcc { get; set; } = new List<string>();
}