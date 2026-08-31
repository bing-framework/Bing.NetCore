using System.Net;
using System.Net.Mail;
using Bing.Extensions;

namespace Bing.Emailing.Smtp;

/// <summary>
/// 使用 SMTP 协议发送邮件的发送器。
/// </summary>
public class SmtpEmailSender : EmailSenderBase, ISmtpEmailSender
{
    /// <summary>
    /// 提供当前 SMTP 连接和认证配置的提供器。
    /// </summary>
    private readonly IEmailConfigProvider _configProvider;

    /// <summary>
    /// 使用邮件配置提供器初始化 <see cref="SmtpEmailSender"/> 的实例。
    /// </summary>
    /// <param name="provider">提供当前 SMTP 连接和认证配置的提供器。</param>
    public SmtpEmailSender(IEmailConfigProvider provider) : base(provider) => _configProvider = provider;

    /// <summary>
    /// 使用临时 SMTP 客户端同步发送邮件。
    /// </summary>
    /// <param name="mail">要发送的邮件消息。</param>
    /// <remarks>无论发送成功或失败，SMTP 客户端都会在方法返回前释放；连接和发送异常会向调用方传播。</remarks>
    protected override void SendEmail(MailMessage mail)
    {
        using var smtpClient = BuildClient();
        smtpClient.Send(mail);
    }

    /// <summary>
    /// 使用临时 SMTP 客户端异步发送邮件。
    /// </summary>
    /// <param name="mail">要发送的邮件消息。</param>
    /// <remarks>异步发送完成或失败后都会释放 SMTP 客户端；连接和协议异常不会被吞没。</remarks>
    protected override async Task SendEmailAsync(MailMessage mail)
    {
        using var smtpClient = BuildClient();
        await smtpClient.SendMailAsync(mail);
    }

    /// <inheritdoc />
    /// <remarks>调用方负责在使用后释放返回的客户端；配置过程失败时会先释放新建客户端，再传播原始异常。</remarks>
    public SmtpClient BuildClient()
    {
        var config = _configProvider.GetConfig();
        var host = config.Host;
        var port = config.Port;

        var smtpClient = new SmtpClient(host, port);
        try
        {
            if (config.EnableSsl) 
                smtpClient.EnableSsl = true;
            if (config.UseDefaultCredentials)
                smtpClient.UseDefaultCredentials = true;
            else
            {
                smtpClient.UseDefaultCredentials = false;
                var userName = config.UserName;
                if (!userName.IsEmpty())
                {
                    var password = config.Password;
                    var domain = config.Domain;
                    smtpClient.Credentials = !domain.IsEmpty()
                        ? new NetworkCredential(userName, password, domain)
                        : new NetworkCredential(userName, password);
                }
            }
            return smtpClient;
        }
        catch
        {
            smtpClient.Dispose();
            throw;
        }
    }
}