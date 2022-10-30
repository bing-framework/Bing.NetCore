using System.Net.Mail;
using System.Threading.Tasks;

namespace Bing.Emailing;

/// <summary>
/// 空电子邮件发送器
/// </summary>
public class NullEmailSender : EmailSenderBase
{
    /// <summary>
    /// 初始化一个<see cref="NullEmailSender"/>类型的实例
    /// </summary>
    /// <param name="provider">电子邮件配置提供器</param>
    public NullEmailSender(IEmailConfigProvider provider) : base(provider)
    {
    }

    /// <summary>
    /// 发送邮件
    /// </summary>
    /// <param name="mail">邮件</param>
    protected override void SendEmail(MailMessage mail)
    {
    }

    /// <summary>
    /// 发送邮件
    /// </summary>
    /// <param name="mail">邮件</param>
    protected override Task SendEmailAsync(MailMessage mail) => Task.FromResult(0);
}