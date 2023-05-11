using Bing.Emailing;
using Microsoft.Extensions.Logging;

namespace Bing.MailKit;

/// <summary>
/// Mailkit邮件队列管理器
/// </summary>
public class MailKitMailQueueManager : MailQueueManagerBase, IMailQueueManager
{
    /// <summary>
    /// MailKit电子邮件发送器
    /// </summary>
    private readonly IMailKitEmailSender _mailKitEmailSender;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger<MailKitMailQueueManager> _logger;

    /// <summary>
    /// 初始化一个<see cref="MailKitMailQueueManager"/>类型的实例
    /// </summary>
    /// <param name="emailConfigProvider">电子邮件配置提供器</param>
    /// <param name="mailQueueProvider">邮件队列提供程序</param>
    /// <param name="mailKitEmailSender">MailKit电子邮件发送器</param>
    /// <param name="logger">日志</param>
    public MailKitMailQueueManager(
        IEmailConfigProvider emailConfigProvider,
        IMailQueueProvider mailQueueProvider,
        IMailKitEmailSender mailKitEmailSender,
        ILogger<MailKitMailQueueManager> logger)
        : base(emailConfigProvider, mailQueueProvider)
    {
        _mailKitEmailSender = mailKitEmailSender;
        _logger = logger;
    }

    /// <summary>
    /// 发送邮件
    /// </summary>
    /// <param name="box">电子邮件</param>
    protected override void SendMail(EmailBox box) => _mailKitEmailSender.Send(box);

    /// <summary>
    /// 写入日志
    /// </summary>
    /// <param name="log">日志</param>
    /// <param name="level">日志等级</param>
    protected override void WriteLog(string log, LogLevel level)
    {
        _logger.Log(level, log);
    }
}
