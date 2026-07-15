using System.Net.Mail;
using System.Text;
using Bing.Emailing;
using Bing.Emailing.Attachments;
using Bing.MailKit.Configs;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.MailKit.Tests;

/// <summary>
/// <see cref="EmailBox"/> 默认值与属性赋值测试。
/// 验证集合属性初始化、IsBodyHtml 默认值及字符串属性读写。
/// </summary>
public class EmailBoxTest
{
    /// <summary>
    /// 测试目的：默认构造后，集合属性应为空列表而非 null，防止空引用异常。
    /// </summary>
    [Fact]
    public void Default_Collections_ShouldBeEmptyNotNull()
    {
        // Arrange & Act
        var box = new EmailBox();

        // Assert
        box.Attachments.ShouldNotBeNull();
        box.Attachments.ShouldBeEmpty();
        box.To.ShouldNotBeNull();
        box.To.ShouldBeEmpty();
        box.Cc.ShouldNotBeNull();
        box.Cc.ShouldBeEmpty();
        box.Bcc.ShouldNotBeNull();
        box.Bcc.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：IsBodyHtml 默认值应为 true，符合现代邮件发送惯例。
    /// </summary>
    [Fact]
    public void IsBodyHtml_Default_ShouldBeTrue()
    {
        // Arrange & Act
        var box = new EmailBox();

        // Assert
        box.IsBodyHtml.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：Body 和 Subject 默认值应为 null，由调用方按需填充。
    /// </summary>
    [Fact]
    public void StringProperties_Default_ShouldBeNull()
    {
        // Arrange & Act
        var box = new EmailBox();

        // Assert
        box.Body.ShouldBeNull();
        box.Subject.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：属性赋值后可正确读取，确保 getter/setter 对称。
    /// </summary>
    [Fact]
    public void Properties_SetAndGet_ShouldRoundtrip()
    {
        // Arrange & Act
        var box = new EmailBox
        {
            Subject = "Test Subject",
            Body = "<h1>Hello</h1>",
            IsBodyHtml = false
        };

        // Assert
        box.Subject.ShouldBe("Test Subject");
        box.Body.ShouldBe("<h1>Hello</h1>");
        box.IsBodyHtml.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：To/Cc/Bcc 可添加成员，验证集合可写。
    /// </summary>
    [Fact]
    public void Collections_CanAddRecipients()
    {
        // Arrange
        var box = new EmailBox();

        // Act
        box.To.Add("alice@example.com");
        box.Cc.Add("cc@example.com");
        box.Bcc.Add("bcc@example.com");

        // Assert
        box.To.ShouldContain("alice@example.com");
        box.Cc.ShouldContain("cc@example.com");
        box.Bcc.ShouldContain("bcc@example.com");
    }
}

/// <summary>
/// <see cref="EmailConfig"/> 配置默认值测试。
/// 验证 Port/SleepInterval 默认值及其余属性的零值/null 初始化。
/// </summary>
public class EmailConfigTest
{
    /// <summary>
    /// 测试目的：Port 默认值应为标准 SMTP 端口 25。
    /// </summary>
    [Fact]
    public void Port_Default_ShouldBe25()
    {
        // Arrange & Act
        var config = new EmailConfig();

        // Assert
        config.Port.ShouldBe(25);
    }

    /// <summary>
    /// 测试目的：SleepInterval 默认值应为 3000ms，即 3 秒轮询间隔。
    /// </summary>
    [Fact]
    public void SleepInterval_Default_ShouldBe3000()
    {
        // Arrange & Act
        var config = new EmailConfig();

        // Assert
        config.SleepInterval.ShouldBe(3000);
    }

    /// <summary>
    /// 测试目的：EnableSsl 和 UseDefaultCredentials 默认为 false，需显式启用。
    /// </summary>
    [Fact]
    public void BoolProperties_Default_ShouldBeFalse()
    {
        // Arrange & Act
        var config = new EmailConfig();

        // Assert
        config.EnableSsl.ShouldBeFalse();
        config.UseDefaultCredentials.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：所有字符串属性默认为 null，防止依赖方误用空字符串默认值。
    /// </summary>
    [Fact]
    public void StringProperties_Default_ShouldBeNull()
    {
        // Arrange & Act
        var config = new EmailConfig();

        // Assert
        config.Host.ShouldBeNull();
        config.UserName.ShouldBeNull();
        config.Password.ShouldBeNull();
        config.Domain.ShouldBeNull();
        config.DisplayName.ShouldBeNull();
        config.FromAddress.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：属性赋值后可正确读取，确保 getter/setter 对称。
    /// </summary>
    [Fact]
    public void Properties_SetAndGet_ShouldRoundtrip()
    {
        // Arrange & Act
        var config = new EmailConfig
        {
            Host = "smtp.example.com",
            Port = 587,
            UserName = "user",
            Password = "pass",
            EnableSsl = true
        };

        // Assert
        config.Host.ShouldBe("smtp.example.com");
        config.Port.ShouldBe(587);
        config.UserName.ShouldBe("user");
        config.EnableSsl.ShouldBeTrue();
    }
}

/// <summary>
/// <see cref="DefaultEmailConfigProvider"/> 测试。
/// 验证 GetConfig/GetConfigAsync 返回构造时注入的配置对象（引用相等）。
/// </summary>
public class DefaultEmailConfigProviderTest
{
    /// <summary>
    /// 测试目的：GetConfig 应返回构造时传入的配置对象引用，无复制行为。
    /// </summary>
    [Fact]
    public void GetConfig_ShouldReturnSameConfig()
    {
        // Arrange
        var config = new EmailConfig { Host = "smtp.example.com", Port = 587 };
        var provider = new DefaultEmailConfigProvider(config);

        // Act & Assert
        provider.GetConfig().ShouldBeSameAs(config);
    }

    /// <summary>
    /// 测试目的：GetConfigAsync 应异步返回构造时传入的配置对象引用。
    /// </summary>
    [Fact]
    public async Task GetConfigAsync_ShouldReturnSameConfig()
    {
        // Arrange
        var config = new EmailConfig { Host = "smtp.example.com" };
        var provider = new DefaultEmailConfigProvider(config);

        // Act
        var result = await provider.GetConfigAsync();

        // Assert
        result.ShouldBeSameAs(config);
    }
}

/// <summary>
/// <see cref="MemoryStreamAttachment"/> 附件测试。
/// 验证 GetFileStream/GetName/Dispose 行为。
/// </summary>
public class MemoryStreamAttachmentTest
{
    /// <summary>
    /// 测试目的：GetFileStream 应返回构造时传入的 MemoryStream 引用，而不是副本。
    /// </summary>
    [Fact]
    public void GetFileStream_ShouldReturnTheStream()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        using var attachment = new MemoryStreamAttachment(stream, "test.bin");

        // Act & Assert
        attachment.GetFileStream().ShouldBeSameAs(stream);
    }

    /// <summary>
    /// 测试目的：GetName 应返回构造时传入的文件名，原样输出。
    /// </summary>
    [Fact]
    public void GetName_ShouldReturnFileName()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var attachment = new MemoryStreamAttachment(stream, "report.pdf");

        // Act & Assert
        attachment.GetName().ShouldBe("report.pdf");
    }

    /// <summary>
    /// 测试目的：中文文件名应原样返回，不丢失 Unicode 字符。
    /// </summary>
    [Fact]
    public void GetName_ChineseFileName_ShouldRoundtrip()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var attachment = new MemoryStreamAttachment(stream, "测试报告.xlsx");

        // Act & Assert
        attachment.GetName().ShouldBe("测试报告.xlsx");
    }

    /// <summary>
    /// 测试目的：Dispose 后 MemoryStream 应被释放，防止内存泄漏。
    /// </summary>
    [Fact]
    public void Dispose_ShouldDisposeUnderlyingStream()
    {
        // Arrange
        var stream = new MemoryStream();
        var attachment = new MemoryStreamAttachment(stream, "file.bin");

        // Act
        attachment.Dispose();

        // Assert：MemoryStream 被释放后 CanRead=false
        stream.CanRead.ShouldBeFalse();
    }
}

/// <summary>
/// <see cref="MailKitConfig"/> 配置测试。
/// 验证可空属性的默认值及赋值行为。
/// </summary>
public class MailKitConfigTest
{
    /// <summary>
    /// 测试目的：默认构造后，可空属性应为 null，不影响调用方的 HasValue 判断。
    /// </summary>
    [Fact]
    public void Default_NullableProperties_ShouldBeNull()
    {
        // Arrange & Act
        var config = new MailKitConfig();

        // Assert
        config.SecureSocketOption.ShouldBeNull();
        config.ServerCertificateValidationCallback.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：ServerCertificateValidationCallback 赋值 true 后可正确读取。
    /// </summary>
    [Fact]
    public void ServerCertificateValidationCallback_SetTrue_ShouldBeTrue()
    {
        // Arrange & Act
        var config = new MailKitConfig { ServerCertificateValidationCallback = true };

        // Assert
        config.ServerCertificateValidationCallback.ShouldBe(true);
    }

    /// <summary>
    /// 测试目的：ServerCertificateValidationCallback 赋值 false 后可正确读取。
    /// </summary>
    [Fact]
    public void ServerCertificateValidationCallback_SetFalse_ShouldBeFalse()
    {
        // Arrange & Act
        var config = new MailKitConfig { ServerCertificateValidationCallback = false };

        // Assert
        config.ServerCertificateValidationCallback.ShouldBe(false);
    }
}

/// <summary>
/// <see cref="DefaultMailKitConfigProvider"/> 测试。
/// 验证 GetConfig/GetConfigAsync 返回注入的配置引用。
/// </summary>
public class DefaultMailKitConfigProviderTest
{
    /// <summary>
    /// 测试目的：GetConfig 应返回构造时传入的 MailKitConfig 引用，无复制。
    /// </summary>
    [Fact]
    public void GetConfig_ShouldReturnSameConfig()
    {
        // Arrange
        var config = new MailKitConfig { ServerCertificateValidationCallback = true };
        var provider = new DefaultMailKitConfigProvider(config);

        // Act & Assert
        provider.GetConfig().ShouldBeSameAs(config);
    }

    /// <summary>
    /// 测试目的：GetConfigAsync 应异步返回构造时传入的 MailKitConfig 引用。
    /// </summary>
    [Fact]
    public async Task GetConfigAsync_ShouldReturnSameConfig()
    {
        // Arrange
        var config = new MailKitConfig();
        var provider = new DefaultMailKitConfigProvider(config);

        // Act
        var result = await provider.GetConfigAsync();

        // Assert
        result.ShouldBeSameAs(config);
    }
}

/// <summary>
/// <see cref="NullEmailSender"/> 测试（通过 <see cref="EmailSenderBase"/> 行为验证）。
/// NullEmailSender.SendEmail 为空实现，因此不依赖 SMTP，适合纯单元测试。
/// 覆盖：构造校验、ConfigProvider 引用、NormalizeMail 行为、多签名 Send/SendAsync。
/// </summary>
public class NullEmailSenderTest
{
    private static IEmailConfigProvider CreateConfigProvider(
        string fromAddress = "noreply@example.com",
        string displayName = "Test Sender")
    {
        var config = new EmailConfig
        {
            FromAddress = fromAddress,
            DisplayName = displayName
        };
        return new DefaultEmailConfigProvider(config);
    }

    /// <summary>
    /// 测试目的：传入 null provider 时应抛出 ArgumentNullException，防止空引用延迟崩溃。
    /// </summary>
    [Fact]
    public void Constructor_NullProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new NullEmailSender(null));
    }

    /// <summary>
    /// 测试目的：ConfigProvider 属性应引用构造时注入的 provider，保持 DI 透明性。
    /// </summary>
    [Fact]
    public void ConfigProvider_ShouldReferenceInjectedProvider()
    {
        // Arrange
        var provider = CreateConfigProvider();
        var sender = new NullEmailSender(provider);

        // Act & Assert
        sender.ConfigProvider.ShouldBeSameAs(provider);
    }

    /// <summary>
    /// 测试目的：Send(to, subject, body) 不应抛异常（NullEmailSender 空实现）。
    /// </summary>
    [Fact]
    public void Send_ThreeStringParams_ShouldNotThrow()
    {
        // Arrange
        var sender = new NullEmailSender(CreateConfigProvider());

        // Act & Assert
        Should.NotThrow(() => sender.Send("to@example.com", "Subject", "Body"));
    }

    /// <summary>
    /// 测试目的：SendAsync(to, subject, body) 不应抛异常。
    /// </summary>
    [Fact]
    public async Task SendAsync_ThreeStringParams_ShouldNotThrow()
    {
        // Arrange
        var sender = new NullEmailSender(CreateConfigProvider());

        // Act & Assert
        await Should.NotThrowAsync(() => sender.SendAsync("to@example.com", "Subject", "Body"));
    }

    /// <summary>
    /// 测试目的：Send(from, to, subject, body) 不应抛异常。
    /// </summary>
    [Fact]
    public void Send_FourStringParams_ShouldNotThrow()
    {
        // Arrange
        var sender = new NullEmailSender(CreateConfigProvider());

        // Act & Assert
        Should.NotThrow(() => sender.Send("from@example.com", "to@example.com", "Subject", "Body"));
    }

    /// <summary>
    /// 测试目的：NormalizeMail 应对未设置编码的 MailMessage 填充 UTF-8。
    /// 验证 HeadersEncoding/SubjectEncoding/BodyEncoding 均被设为 Encoding.UTF8。
    /// </summary>
    [Fact]
    public void Send_NormalizeMail_ShouldSetUtf8Encodings()
    {
        // Arrange
        var sender = new NullEmailSender(CreateConfigProvider());
        var mail = new MailMessage
        {
            From = new MailAddress("sender@example.com"),
            Subject = "Encoding Test"
        };
        mail.To.Add("to@example.com");

        // Act：normalize=true 触发 NormalizeMail
        sender.Send(mail, normalize: true);

        // Assert
        mail.HeadersEncoding.ShouldBe(Encoding.UTF8);
        mail.SubjectEncoding.ShouldBe(Encoding.UTF8);
        mail.BodyEncoding.ShouldBe(Encoding.UTF8);
    }

    /// <summary>
    /// 测试目的：当 mail.From == null 时，NormalizeMail 应从 EmailConfig 中自动填充发件人地址。
    /// </summary>
    [Fact]
    public void Send_NormalizeMail_WhenFromNull_ShouldFillFromAddressFromConfig()
    {
        // Arrange
        var sender = new NullEmailSender(CreateConfigProvider("noreply@example.com", "Auto Sender"));
        var mail = new MailMessage { Subject = "Auto From Test" };
        mail.To.Add("to@example.com");

        // Act
        sender.Send(mail, normalize: true);

        // Assert
        mail.From.ShouldNotBeNull();
        mail.From.Address.ShouldBe("noreply@example.com");
    }

    /// <summary>
    /// 测试目的：当 mail.From 已设置时，NormalizeMail 不应覆盖发件人地址。
    /// </summary>
    [Fact]
    public void Send_NormalizeMail_WhenFromAlreadySet_ShouldPreserveFrom()
    {
        // Arrange
        var sender = new NullEmailSender(CreateConfigProvider("noreply@example.com"));
        var mail = new MailMessage
        {
            From = new MailAddress("custom@example.com"),
            Subject = "Preserve From"
        };
        mail.To.Add("to@example.com");

        // Act
        sender.Send(mail, normalize: true);

        // Assert：原发件人地址不应被覆盖
        mail.From.Address.ShouldBe("custom@example.com");
    }

    /// <summary>
    /// 测试目的：Send(MailMessage, normalize=false) 不应修改 MailMessage 的任何编码字段。
    /// </summary>
    [Fact]
    public void Send_WithNormalizeFalse_ShouldNotModifyMail()
    {
        // Arrange
        var sender = new NullEmailSender(CreateConfigProvider());
        var mail = new MailMessage { Subject = "No Normalize" };
        mail.To.Add("to@example.com");

        // Act
        sender.Send(mail, normalize: false);

        // Assert：编码未被设置（仍为 null）
        mail.HeadersEncoding.ShouldBeNull();
        mail.SubjectEncoding.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Send(EmailBox) 不应抛异常，验证 EmailBox 路径可走通。
    /// </summary>
    [Fact]
    public void Send_EmailBox_ShouldNotThrow()
    {
        // Arrange
        var sender = new NullEmailSender(CreateConfigProvider());
        var box = new EmailBox
        {
            Subject = "Box Test",
            Body = "<p>Hello</p>",
            To = new System.Collections.Generic.List<string> { "to@example.com" }
        };

        // Act & Assert
        Should.NotThrow(() => sender.Send(box));
    }

    /// <summary>
    /// 测试目的：SendAsync(EmailBox) 不应抛异常，验证异步 EmailBox 路径可走通。
    /// </summary>
    [Fact]
    public async Task SendAsync_EmailBox_ShouldNotThrow()
    {
        // Arrange
        var sender = new NullEmailSender(CreateConfigProvider());
        var box = new EmailBox
        {
            Subject = "Async Box Test",
            Body = "<p>Hello Async</p>",
            To = new System.Collections.Generic.List<string> { "to@example.com" }
        };

        // Act & Assert
        await Should.NotThrowAsync(() => sender.SendAsync(box));
    }
}

/// <summary>
/// <see cref="MailQueueService"/> 测试。
/// 验证 Enqueue 是否正确委托给 <see cref="IMailQueueProvider"/>。
/// </summary>
public class MailQueueServiceTest
{
    /// <summary>
    /// 测试目的：Enqueue 应将邮件委托给 IMailQueueProvider.Enqueue，不做额外转换。
    /// </summary>
    [Fact]
    public void Enqueue_ShouldDelegateToProvider()
    {
        // Arrange
        var mockProvider = new Mock<IMailQueueProvider>();
        var service = new MailQueueService(mockProvider.Object);
        var box = new EmailBox { Subject = "Delegate Test" };

        // Act
        service.Enqueue(box);

        // Assert
        mockProvider.Verify(p => p.Enqueue(box), Times.Once);
    }

    /// <summary>
    /// 测试目的：Enqueue 使用正确的 EmailBox 实例（引用传递，不复制）。
    /// </summary>
    [Fact]
    public void Enqueue_ShouldPassExactBoxReference()
    {
        // Arrange
        EmailBox capturedBox = null;
        var mockProvider = new Mock<IMailQueueProvider>();
        mockProvider
            .Setup(p => p.Enqueue(It.IsAny<EmailBox>()))
            .Callback<EmailBox>(b => capturedBox = b);
        var service = new MailQueueService(mockProvider.Object);
        var box = new EmailBox { Subject = "Reference Test" };

        // Act
        service.Enqueue(box);

        // Assert
        capturedBox.ShouldBeSameAs(box);
    }
}

/// <summary>
/// <see cref="MailQueueProvider"/> 基本操作测试。
/// 注意：MailQueueProvider 内部使用静态 ConcurrentQueue，测试间可能共享状态，
/// 因此每个测试在操作后应清理自己入队的元素，以减少测试间干扰。
/// </summary>
public class MailQueueProviderTest
{
    /// <summary>
    /// 测试目的：Enqueue 应使 Count 增加，IsEmpty 变为 false。
    /// </summary>
    [Fact]
    public void Enqueue_ShouldIncrementCountAndNotBeEmpty()
    {
        // Arrange
        var provider = new MailQueueProvider();
        var countBefore = provider.Count;
        var box = new EmailBox { Subject = "Queue Count Test" };

        // Act
        provider.Enqueue(box);

        // Assert
        provider.Count.ShouldBe(countBefore + 1);
        provider.IsEmpty.ShouldBeFalse();

        // Cleanup：取出自己入队的项
        provider.TryDequeue(out _);
    }

    /// <summary>
    /// 测试目的：TryDequeue 在队列非空时应返回 true 并输出邮件项。
    /// </summary>
    [Fact]
    public void TryDequeue_WhenNotEmpty_ShouldReturnTrueAndItem()
    {
        // Arrange
        var provider = new MailQueueProvider();
        var box = new EmailBox { Subject = "Dequeue Test" };
        provider.Enqueue(box);

        // Act
        var result = provider.TryDequeue(out var dequeued);

        // Assert
        result.ShouldBeTrue();
        dequeued.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：Count 与实际入队数量匹配（相对计数验证）。
    /// </summary>
    [Fact]
    public void Count_ShouldReflectEnqueuedItems()
    {
        // Arrange
        var provider = new MailQueueProvider();
        var baseline = provider.Count;

        // Act
        provider.Enqueue(new EmailBox { Subject = "A" });
        provider.Enqueue(new EmailBox { Subject = "B" });

        // Assert
        provider.Count.ShouldBe(baseline + 2);

        // Cleanup
        provider.TryDequeue(out _);
        provider.TryDequeue(out _);
    }
}
