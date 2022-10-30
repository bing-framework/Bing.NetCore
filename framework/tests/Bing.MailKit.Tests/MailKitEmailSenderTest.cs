using System.Collections.Generic;
using Bing.Emailing;
using Bing.Emailing.Attachments;
using Bing.MailKit.Configs;
using MailKit.Security;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MailKit.Tests;

/// <summary>
/// MailKit邮件发送器测试
/// </summary>
public class MailKitEmailSenderTest : TestBase
{
    /// <summary>
    /// 电子邮件配置提供器
    /// </summary>
    private readonly IEmailConfigProvider _emailConfigProvider;

    /// <summary>
    /// MailKit配置提供器
    /// </summary>
    private readonly IMailKitConfigProvider _mailKitConfigProvider;

    /// <summary>
    /// MailKit邮件发送器
    /// </summary>
    private readonly IMailKitEmailSender _mailKitEmailSender;

    /// <summary>
    /// 收件人
    /// </summary>
    private readonly List<string> _to;

    public MailKitEmailSenderTest(ITestOutputHelper output) : base(output)
    {
        _emailConfigProvider = new DefaultEmailConfigProvider(new EmailConfig
        {
            DisplayName = "简玄冰",
            Host = "smtp.126.com",
            Port = 25,
            UserName = "@126.com",
            Password = "",
            FromAddress = "@126.com"
        });
        _mailKitConfigProvider = new DefaultMailKitConfigProvider(new MailKitConfig
        {
            SecureSocketOption = SecureSocketOptions.StartTlsWhenAvailable,
            ServerCertificateValidationCallback = true
        });
        _to = new List<string> { "" };
        _mailKitEmailSender = new MailKitEmailSender(_emailConfigProvider,
            new DefaultMailKitSmtpBuilder(_emailConfigProvider, _mailKitConfigProvider));
    }

    /// <summary>
    /// 测试发送邮件
    /// </summary>
    [Fact(Skip = "需要提供配置")]
    public void Test_SendEmail()
    {
        var box = new EmailBox
        {
            Subject = "MailKit 测试发送邮件",
            To = _to,
            Body = "<p style='color:red'>测试一下红色字体的邮件</p>",
            IsBodyHtml = true,
        };
        this._mailKitEmailSender.Send(box);
    }

    /// <summary>
    /// 测试发送邮件以及附件
    /// </summary>
    [Fact(Skip = "需要提供配置")]
    public void Test_SendEmail_Attachment()
    {
        var box = new EmailBox()
        {
            Subject = "MailKit 测试发送邮件以及附件",
            To = _to,
            Body = "<p style='color:red'>测试一下红色字体的邮件</p>",
            IsBodyHtml = true,
        };
        box.Attachments.Add(new PhysicalFileAttachment("D:\\123.xlsx"));
        this._mailKitEmailSender.Send(box);
    }

    /// <summary>
    /// 测试发送邮件以及附件_中文文件名
    /// </summary>
    [Fact(Skip = "需要提供配置")]
    public void Test_SendEmail_Attachment_ChineseFileName()
    {
        var box = new EmailBox()
        {
            Subject = "MailKit 测试发送邮件以及附件_中文文件名",
            To = _to,
            Body = "<p style='color:red'>测试一下红色字体的邮件</p>",
            IsBodyHtml = true,
        };
        box.Attachments.Add(new PhysicalFileAttachment("D:\\测试文件.xlsx"));
        this._mailKitEmailSender.Send(box);
    }

    /// <summary>
    /// 测试发送邮件以及附件_多个文件
    /// </summary>
    [Fact(Skip = "需要提供配置")]
    public void Test_SendEmail_Attachment_MultiFile()
    {
        var box = new EmailBox()
        {
            Subject = "MailKit 测试发送邮件以及附件_多个文件",
            To = _to,
            Body = "<p style='color:red'>测试一下红色字体的邮件</p>",
            IsBodyHtml = true,
        };
        box.Attachments.Add(new PhysicalFileAttachment("D:\\123.xlsx"));
        box.Attachments.Add(new PhysicalFileAttachment("D:\\测试文件.doc"));
        this._mailKitEmailSender.Send(box);
    }

    /// <summary>
    /// 测试发送邮件以及附件_长文件名
    /// </summary>
    [Fact(Skip = "需要提供配置")]
    public void Test_SendEmail_Attachment_LongLengthFileName()
    {
        var box = new EmailBox()
        {
            Subject = "MailKit 测试发送邮件以及附件_多个文件",
            To = _to,
            Body = "<p style='color:red'>测试一下红色字体的邮件</p>",
            IsBodyHtml = true,
        };
        box.Attachments.Add(new PhysicalFileAttachment("D:\\测试文件123456789012345678901234567894444564645666666666666.doc"));
        this._mailKitEmailSender.Send(box);
    }
}