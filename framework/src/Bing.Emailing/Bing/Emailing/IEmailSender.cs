using System.Net.Mail;

namespace Bing.Emailing;

/// <summary>
/// 定义同步和异步发送电子邮件的能力。
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// 使用默认发件人配置发送邮件。
    /// </summary>
    /// <param name="to">收件人地址或分隔的收件人地址列表。</param>
    /// <param name="subject">邮件主题。</param>
    /// <param name="body">邮件正文。</param>
    /// <param name="isBodyHtml">是否将正文按 HTML 内容发送，默认值为 <c>true</c>。</param>
    void Send(string to, string subject, string body, bool isBodyHtml = true);

    /// <summary>
    /// 使用默认发件人配置异步发送邮件。
    /// </summary>
    /// <param name="to">收件人地址或分隔的收件人地址列表。</param>
    /// <param name="subject">邮件主题。</param>
    /// <param name="body">邮件正文。</param>
    /// <param name="isBodyHtml">是否将正文按 HTML 内容发送，默认值为 <c>true</c>。</param>
    Task SendAsync(string to, string subject, string body, bool isBodyHtml = true);

    /// <summary>
    /// 使用指定发件人地址发送邮件。
    /// </summary>
    /// <param name="from">发件人邮箱地址。</param>
    /// <param name="to">收件人地址或分隔的收件人地址列表。</param>
    /// <param name="subject">邮件主题。</param>
    /// <param name="body">邮件正文。</param>
    /// <param name="isBodyHtml">是否将正文按 HTML 内容发送，默认值为 <c>true</c>。</param>
    void Send(string from, string to, string subject, string body, bool isBodyHtml = true);

    /// <summary>
    /// 使用指定发件人地址异步发送邮件。
    /// </summary>
    /// <param name="from">发件人邮箱地址。</param>
    /// <param name="to">收件人地址或分隔的收件人地址列表。</param>
    /// <param name="subject">邮件主题。</param>
    /// <param name="body">邮件正文。</param>
    /// <param name="isBodyHtml">是否将正文按 HTML 内容发送，默认值为 <c>true</c>。</param>
    Task SendAsync(string from, string to, string subject, string body, bool isBodyHtml = true);

    /// <summary>
    /// 根据邮件消息模型发送邮件。
    /// </summary>
    /// <param name="box">包含收件人、主题、正文和附件的邮件消息模型。</param>
    void Send(EmailBox box);

    /// <summary>
    /// 根据邮件消息模型异步发送邮件。
    /// </summary>
    /// <param name="box">包含收件人、主题、正文和附件的邮件消息模型。</param>
    Task SendAsync(EmailBox box);

    /// <summary>
    /// 发送已构造的 <see cref="MailMessage"/>。
    /// </summary>
    /// <param name="mail">要发送的邮件消息。</param>
    /// <param name="normalize">是否补全发件人信息并将邮件编码规范化为 UTF-8，默认值为 <c>true</c>。</param>
    void Send(MailMessage mail, bool normalize = true);

    /// <summary>
    /// 异步发送已构造的 <see cref="MailMessage"/>。
    /// </summary>
    /// <param name="mail">要发送的邮件消息。</param>
    /// <param name="normalize">是否补全发件人信息并将邮件编码规范化为 UTF-8，默认值为 <c>true</c>。</param>
    Task SendAsync(MailMessage mail, bool normalize = true);
}
