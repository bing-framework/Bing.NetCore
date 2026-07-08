using System.Net.Mail;
using Bing.MailKit.Extensions;
using Shouldly;
using Xunit;

namespace Bing.MailKit.Tests;

/// <summary>
/// <see cref="EmailExtensions.ToMimeMessage"/> 单元测试。
/// 验证 System.Net.Mail.MailMessage → MimeKit.MimeMessage 转换的关键行为，
/// 不依赖 SMTP 连接，纯内存转换。
/// </summary>
public class EmailExtensionsTest
{
    /// <summary>
    /// 测试目的：ToMimeMessage(null) 应抛出 ArgumentNullException，防止空引用。
    /// </summary>
    [Fact]
    public void ToMimeMessage_NullMail_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((MailMessage)null).ToMimeMessage());
    }

    /// <summary>
    /// 测试目的：邮件主题应正确映射到 MimeMessage.Subject。
    /// </summary>
    [Fact]
    public void ToMimeMessage_WithSubject_ShouldMapSubject()
    {
        // Arrange
        var mail = new MailMessage
        {
            From = new MailAddress("sender@example.com"),
            Subject = "Hello World"
        };

        // Act
        var message = mail.ToMimeMessage();

        // Assert
        message.Subject.ShouldBe("Hello World");
    }

    /// <summary>
    /// 测试目的：发件人地址应正确映射到 MimeMessage.From。
    /// </summary>
    [Fact]
    public void ToMimeMessage_WithFrom_ShouldMapFromAddress()
    {
        // Arrange
        var mail = new MailMessage
        {
            From = new MailAddress("sender@example.com", "Sender Name"),
            Subject = "Test"
        };

        // Act
        var message = mail.ToMimeMessage();

        // Assert
        message.From.Mailboxes.ShouldContain(m => m.Address == "sender@example.com");
    }

    /// <summary>
    /// 测试目的：收件人列表应正确映射到 MimeMessage.To。
    /// </summary>
    [Fact]
    public void ToMimeMessage_WithToRecipients_ShouldMapToList()
    {
        // Arrange
        var mail = new MailMessage
        {
            From = new MailAddress("sender@example.com"),
            Subject = "Test"
        };
        mail.To.Add("alice@example.com");
        mail.To.Add("bob@example.com");

        // Act
        var message = mail.ToMimeMessage();

        // Assert
        message.To.Mailboxes.Count().ShouldBe(2);
        message.To.Mailboxes.ShouldContain(m => m.Address == "alice@example.com");
        message.To.Mailboxes.ShouldContain(m => m.Address == "bob@example.com");
    }

    /// <summary>
    /// 测试目的：抄送列表应正确映射到 MimeMessage.Cc。
    /// </summary>
    [Fact]
    public void ToMimeMessage_WithCc_ShouldMapCcList()
    {
        // Arrange
        var mail = new MailMessage { From = new MailAddress("sender@example.com"), Subject = "Test" };
        mail.CC.Add("cc@example.com");

        // Act
        var message = mail.ToMimeMessage();

        // Assert
        message.Cc.Mailboxes.ShouldContain(m => m.Address == "cc@example.com");
    }

    /// <summary>
    /// 测试目的：密送列表应正确映射到 MimeMessage.Bcc。
    /// </summary>
    [Fact]
    public void ToMimeMessage_WithBcc_ShouldMapBccList()
    {
        // Arrange
        var mail = new MailMessage { From = new MailAddress("sender@example.com"), Subject = "Test" };
        mail.Bcc.Add("bcc@example.com");

        // Act
        var message = mail.ToMimeMessage();

        // Assert
        message.Bcc.Mailboxes.ShouldContain(m => m.Address == "bcc@example.com");
    }

    /// <summary>
    /// 测试目的：HTML 正文应被映射为 text/html Content-Type。
    /// </summary>
    [Fact]
    public void ToMimeMessage_WithHtmlBody_ShouldHaveHtmlContentType()
    {
        // Arrange
        var mail = new MailMessage
        {
            From = new MailAddress("sender@example.com"),
            Subject = "HTML Test",
            Body = "<h1>Hello</h1>",
            IsBodyHtml = true
        };

        // Act
        var message = mail.ToMimeMessage();

        // Assert
        message.Body.ShouldNotBeNull();
        message.HtmlBody.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// 测试目的：纯文本正文应被映射为 text/plain Content-Type。
    /// </summary>
    [Fact]
    public void ToMimeMessage_WithPlainTextBody_ShouldHaveTextContentType()
    {
        // Arrange
        var mail = new MailMessage
        {
            From = new MailAddress("sender@example.com"),
            Subject = "Plain Text Test",
            Body = "Hello, plain text!",
            IsBodyHtml = false
        };

        // Act
        var message = mail.ToMimeMessage();

        // Assert
        message.Body.ShouldNotBeNull();
        message.TextBody.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// 测试目的：无 From/无 Subject 的空邮件也应能成功转换，不抛异常。
    /// </summary>
    [Fact]
    public void ToMimeMessage_EmptyMail_ShouldNotThrow()
    {
        // Arrange
        var mail = new MailMessage();

        // Act & Assert
        Should.NotThrow(() => mail.ToMimeMessage());
    }
}
