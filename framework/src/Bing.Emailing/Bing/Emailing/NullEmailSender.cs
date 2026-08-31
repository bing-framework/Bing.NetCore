using System.Net.Mail;

namespace Bing.Emailing;

/// <summary>
/// 不执行实际邮件发送的空对象发送器。
/// </summary>
public class NullEmailSender : EmailSenderBase
{
    /// <summary>
    /// 使用邮件配置提供器初始化 <see cref="NullEmailSender"/> 的实例。
    /// </summary>
    /// <param name="provider">提供邮件配置的提供器。</param>
    public NullEmailSender(IEmailConfigProvider provider) : base(provider)
    {
    }

    /// <inheritdoc />
    /// <remarks>当前实现不发送邮件，也不修改邮件消息。</remarks>
    protected override void SendEmail(MailMessage mail)
    {
    }

    /// <inheritdoc />
    /// <remarks>当前实现立即完成，不发送邮件，也不修改邮件消息。</remarks>
    protected override Task SendEmailAsync(MailMessage mail) => Task.FromResult(0);
}