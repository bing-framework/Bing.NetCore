using Bing.Logging.Serilog.Internals;
using Serilog.Events;

namespace Bing.Logging.Tests.Internals;

/// <summary>
/// <see cref="LogLevelSwitcher"/> 单元测试
/// </summary>
public class LogLevelSwitcherTest
{
    // ═══════════════════════════════════════════════════════════
    // Switch(LogEventLevel) → string
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Verbose 级别应映射到 MS 日志 "Trace"。
    /// </summary>
    [Fact]
    public void Switch_Verbose_ShouldReturnTrace()
    {
        LogLevelSwitcher.Switch(LogEventLevel.Verbose).ShouldBe("Trace");
    }

    /// <summary>
    /// 测试目的：Debug 级别应映射到 "Debug"。
    /// </summary>
    [Fact]
    public void Switch_Debug_ShouldReturnDebug()
    {
        LogLevelSwitcher.Switch(LogEventLevel.Debug).ShouldBe("Debug");
    }

    /// <summary>
    /// 测试目的：Information 级别应映射到 "Information"。
    /// </summary>
    [Fact]
    public void Switch_Information_ShouldReturnInformation()
    {
        LogLevelSwitcher.Switch(LogEventLevel.Information).ShouldBe("Information");
    }

    /// <summary>
    /// 测试目的：Warning 级别应映射到 "Warning"。
    /// </summary>
    [Fact]
    public void Switch_Warning_ShouldReturnWarning()
    {
        LogLevelSwitcher.Switch(LogEventLevel.Warning).ShouldBe("Warning");
    }

    /// <summary>
    /// 测试目的：Error 级别应映射到 "Error"。
    /// </summary>
    [Fact]
    public void Switch_Error_ShouldReturnError()
    {
        LogLevelSwitcher.Switch(LogEventLevel.Error).ShouldBe("Error");
    }

    /// <summary>
    /// 测试目的：Fatal 级别应映射到 MS 日志 "Critical"。
    /// </summary>
    [Fact]
    public void Switch_Fatal_ShouldReturnCritical()
    {
        LogLevelSwitcher.Switch(LogEventLevel.Fatal).ShouldBe("Critical");
    }

    /// <summary>
    /// 测试目的：未知 Serilog 级别（枚举范围外）应返回 null。
    /// </summary>
    [Fact]
    public void Switch_UnknownLevel_ShouldReturnNull()
    {
        LogLevelSwitcher.Switch((LogEventLevel)999).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：所有标准 Serilog 级别均能正确映射（参数化验证）。
    /// </summary>
    [Theory]
    [InlineData(LogEventLevel.Verbose, "Trace")]
    [InlineData(LogEventLevel.Debug, "Debug")]
    [InlineData(LogEventLevel.Information, "Information")]
    [InlineData(LogEventLevel.Warning, "Warning")]
    [InlineData(LogEventLevel.Error, "Error")]
    [InlineData(LogEventLevel.Fatal, "Critical")]
    public void Switch_AllSerilogLevels_ShouldMapCorrectly(LogEventLevel level, string expected)
    {
        LogLevelSwitcher.Switch(level).ShouldBe(expected);
    }

    // ═══════════════════════════════════════════════════════════
    // Switch(string) → LogEventLevel
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：字符串 "Trace" 应反向映射到 Verbose 级别。
    /// </summary>
    [Fact]
    public void Switch_TraceString_ShouldReturnVerbose()
    {
        LogLevelSwitcher.Switch("Trace").ShouldBe(LogEventLevel.Verbose);
    }

    /// <summary>
    /// 测试目的：字符串 "Debug" 应反向映射到 Debug 级别。
    /// </summary>
    [Fact]
    public void Switch_DebugString_ShouldReturnDebug()
    {
        LogLevelSwitcher.Switch("Debug").ShouldBe(LogEventLevel.Debug);
    }

    /// <summary>
    /// 测试目的：字符串 "Information" 应映射到 Information 级别。
    /// </summary>
    [Fact]
    public void Switch_InformationString_ShouldReturnInformation()
    {
        LogLevelSwitcher.Switch("Information").ShouldBe(LogEventLevel.Information);
    }

    /// <summary>
    /// 测试目的：字符串 "Warning" 应映射到 Warning 级别。
    /// </summary>
    [Fact]
    public void Switch_WarningString_ShouldReturnWarning()
    {
        LogLevelSwitcher.Switch("Warning").ShouldBe(LogEventLevel.Warning);
    }

    /// <summary>
    /// 测试目的：字符串 "Error" 应映射到 Error 级别。
    /// </summary>
    [Fact]
    public void Switch_ErrorString_ShouldReturnError()
    {
        LogLevelSwitcher.Switch("Error").ShouldBe(LogEventLevel.Error);
    }

    /// <summary>
    /// 测试目的：字符串 "Critical" 应映射到 Fatal 级别。
    /// </summary>
    [Fact]
    public void Switch_CriticalString_ShouldReturnFatal()
    {
        LogLevelSwitcher.Switch("Critical").ShouldBe(LogEventLevel.Fatal);
    }

    /// <summary>
    /// 测试目的：字符串 "None" 应映射到 Fatal 级别。
    /// </summary>
    [Fact]
    public void Switch_NoneString_ShouldReturnFatal()
    {
        LogLevelSwitcher.Switch("None").ShouldBe(LogEventLevel.Fatal);
    }

    /// <summary>
    /// 测试目的：Switch 对字符串输入应大小写不敏感（全大写）。
    /// </summary>
    [Theory]
    [InlineData("TRACE", LogEventLevel.Verbose)]
    [InlineData("debug", LogEventLevel.Debug)]
    [InlineData("INFORMATION", LogEventLevel.Information)]
    [InlineData("warning", LogEventLevel.Warning)]
    [InlineData("ERROR", LogEventLevel.Error)]
    [InlineData("critical", LogEventLevel.Fatal)]
    [InlineData("none", LogEventLevel.Fatal)]
    public void Switch_StringInput_ShouldBeCaseInsensitive(string input, LogEventLevel expected)
    {
        LogLevelSwitcher.Switch(input).ShouldBe(expected);
    }

    /// <summary>
    /// 测试目的：未知字符串级别应 fallback 到 Warning（防止未知配置导致日志全量输出）。
    /// </summary>
    [Fact]
    public void Switch_UnknownString_ShouldFallbackToWarning()
    {
        LogLevelSwitcher.Switch("unknown_level").ShouldBe(LogEventLevel.Warning);
    }

    /// <summary>
    /// 测试目的：双向转换应保持幂等——先转 string，再转回 LogEventLevel，结果与原始级别一致。
    /// </summary>
    [Theory]
    [InlineData(LogEventLevel.Verbose)]
    [InlineData(LogEventLevel.Debug)]
    [InlineData(LogEventLevel.Information)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public void Switch_RoundTrip_ShouldRestoreOriginalLevel(LogEventLevel original)
    {
        // Arrange & Act
        var str = LogLevelSwitcher.Switch(original);
        var roundTripped = LogLevelSwitcher.Switch(str);

        // Assert
        roundTripped.ShouldBe(original);
    }
}
