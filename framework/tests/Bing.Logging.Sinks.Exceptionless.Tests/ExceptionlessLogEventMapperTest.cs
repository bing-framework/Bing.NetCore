using Exceptionless;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.Exceptionless;
using Shouldly;
using Xunit;

namespace Bing.Logging.Sinks.Exceptionless.Tests;

/// <summary>
/// <see cref="ExceptionlessLogEventMapper"/> 单元测试
/// </summary>
public class ExceptionlessLogEventMapperTest
{
    /// <summary>
    /// 测试目的：密码、Token、API Key等敏感字段应在提交前统一脱敏。
    /// </summary>
    [Theory]
    [InlineData("Password")]
    [InlineData("access_token")]
    [InlineData("X-Api-Key")]
    [InlineData("Authorization")]
    [InlineData("Cookie")]
    public void Map_WhenPropertyNameIsSensitive_ShouldRedactValue(string propertyName)
    {
        // Arrange
        var mapper = new ExceptionlessLogEventMapper();
        var logEvent = CreateLogEvent(
            "message",
            new LogEventProperty(propertyName, new ScalarValue("secret")));
        var client = CreateClient();

        // Act
        var builder = mapper.Map(client, logEvent, true, null);

        // Assert
        builder.Target.Data[propertyName].ShouldBe(SensitiveFieldRedactor.RedactedValue);
    }

    /// <summary>
    /// 测试目的：超长消息和属性值应按配置截断，避免单个事件无限膨胀。
    /// </summary>
    [Fact]
    public void Map_WhenStringsExceedLimit_ShouldTruncateMessageAndProperty()
    {
        // Arrange
        var options = new ExceptionlessLogEventMapperOptions { MaxStringLength = 10 };
        var mapper = new ExceptionlessLogEventMapper(options);
        var logEvent = CreateLogEvent(
            new string('m', 20),
            new LogEventProperty("Value", new ScalarValue(new string('v', 20))));
        var client = CreateClient();

        // Act
        var builder = mapper.Map(client, logEvent, true, null);

        // Assert
        builder.Target.Message.Length.ShouldBe(10);
        builder.Target.Data["Value"].ToString().Length.ShouldBe(10);
    }

    /// <summary>
    /// 测试目的：属性数量超过限制时应优先保留TraceId等关联字段。
    /// </summary>
    [Fact]
    public void Map_WhenPropertyCountExceedsLimit_ShouldKeepPriorityProperties()
    {
        // Arrange
        var options = new ExceptionlessLogEventMapperOptions { MaxPropertyCount = 2 };
        var mapper = new ExceptionlessLogEventMapper(options);
        var logEvent = CreateLogEvent(
            "message",
            new LogEventProperty("Zeta", new ScalarValue("z")),
            new LogEventProperty("TraceId", new ScalarValue("trace")),
            new LogEventProperty("UserId", new ScalarValue("user")));
        var client = CreateClient();

        // Act
        var builder = mapper.Map(client, logEvent, true, null);

        // Assert
        builder.Target.Data.ContainsKey("TraceId").ShouldBeTrue();
        builder.Target.Data.ContainsKey("UserId").ShouldBeTrue();
        builder.Target.Data.ContainsKey("Zeta").ShouldBeFalse();
    }

    private static ExceptionlessClient CreateClient() => new(configuration => configuration.ApiKey = "test-api-key");

    private static LogEvent CreateLogEvent(string message, params LogEventProperty[] properties) =>
        new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse(message),
            properties);
}