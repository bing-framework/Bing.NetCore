using System.Collections.Generic;
using Bing.Emailing;
using Bing.Emailing.Smtp;
using Xunit.Abstractions;

namespace Bing.Tests.Net.Mail.Smtp;

/// <summary>
/// Smtp邮件发送器测试
/// </summary>
public class SmtpEmailSenderTest:TestBase
{
    /// <summary>
    /// 配置提供程序
    /// </summary>
    private readonly IEmailConfigProvider _configProvider;
    /// <summary>
    /// Smtp邮件发送器
    /// </summary>
    private readonly ISmtpEmailSender _smtpEmailSender;

    /// <summary>
    /// 收件人
    /// </summary>
    private readonly List<string> _to;

    public SmtpEmailSenderTest(ITestOutputHelper output) : base(output)
    {
        _configProvider=new DefaultEmailConfigProvider(new EmailConfig()
        {
            DisplayName = "简玄冰",
            Host = "smtp.126.com",
            Port = 25,
            UserName = "",
            Password = "",
            FromAddress = ""
        });
        _smtpEmailSender = new SmtpEmailSender(_configProvider);
        _to=new List<string>(){""};
    }

    ///// <summary>
    ///// 测试发送邮件
    ///// </summary>
    //[Fact]
    //public void Test_SendEmail()
    //{
    //    var box = new EmailBox()
    //    {
    //        Subject = "测试发送邮件",
    //        To = _to,
    //        Body = "<p style='color:red'>测试一下红色字体的邮件</p>",
    //        IsBodyHtml = true,
    //    };
    //    this._smtpEmailSender.Send(box);
    //}

    ///// <summary>
    ///// 测试发送邮件以及附件
    ///// </summary>
    //[Fact]
    //public void Test_SendEmail_Attachment()
    //{
    //    var box = new EmailBox()
    //    {
    //        Subject = "测试发送邮件以及附件",
    //        To = _to,
    //        Body = "<p style='color:red'>测试一下红色字体的邮件</p>",
    //        IsBodyHtml = true,
    //    };
    //    box.Attachments.Add(new PhysicalFileAttachment("D:\\123.xlsx"));
    //    this._smtpEmailSender.Send(box);
    //}

    ///// <summary>
    ///// 测试发送邮件以及附件_中文文件名
    ///// </summary>
    //[Fact]
    //public void Test_SendEmail_Attachment_ChineseFileName()
    //{
    //    var box = new EmailBox()
    //    {
    //        Subject = "测试发送邮件以及附件_中文文件名",
    //        To = _to,
    //        Body = "<p style='color:red'>测试一下红色字体的邮件</p>",
    //        IsBodyHtml = true,
    //    };
    //    box.Attachments.Add(new PhysicalFileAttachment("D:\\测试文件.doc"));
    //    this._smtpEmailSender.Send(box);
    //}

    ///// <summary>
    ///// 测试发送邮件以及附件_多个文件
    ///// </summary>
    //[Fact]
    //public void Test_SendEmail_Attachment_MultiFile()
    //{
    //    var box = new EmailBox()
    //    {
    //        Subject = "测试发送邮件以及附件_多个文件",
    //        To = _to,
    //        Body = "<p style='color:red'>测试一下红色字体的邮件</p>",
    //        IsBodyHtml = true,
    //    };
    //    box.Attachments.Add(new PhysicalFileAttachment("D:\\123.xlsx"));
    //    box.Attachments.Add(new PhysicalFileAttachment("D:\\测试文件.doc"));
    //    this._smtpEmailSender.Send(box);
    //}
}