using Bing.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Bing.AspNetCore.Mvc.Tests.Bing.AspNetCore.Hosting;

/// <summary>
/// 验证宿主角色配置解析。
/// </summary>
public sealed class BingHostRoleTest
{
    /// <summary>
    /// 未配置角色时保持 Web 兼容行为。
    /// </summary>
    [Fact]
    public void GetBingHostRole_WhenMissing_ShouldReturnWeb()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Equal(BingHostRole.Web, configuration.GetBingHostRole());
        Assert.False(configuration.IsBingConsumerRole());
        Assert.False(configuration.IsBingBackgroundRole());
    }

    /// <summary>
    /// 角色配置应忽略大小写。
    /// </summary>
    [Theory]
    [InlineData("web", BingHostRole.Web)]
    [InlineData("CONSUMER", BingHostRole.Consumer)]
    [InlineData("Task", BingHostRole.Task)]
    public void GetBingHostRole_ShouldParseSupportedValues(string value, BingHostRole expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                [BingHostRoleExtensions.ConfigurationKey] = value
            })
            .Build();

        Assert.Equal(expected, configuration.GetBingHostRole());
        Assert.Equal(expected != BingHostRole.Web, configuration.IsBingBackgroundRole());
    }

    /// <summary>
    /// 不支持的角色应在启动配置阶段失败。
    /// </summary>
    [Theory]
    [InlineData("publisher")]
    [InlineData("1")]
    public void GetBingHostRole_WhenInvalid_ShouldFailWithConfigurationKey(string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                [BingHostRoleExtensions.ConfigurationKey] = value
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.GetBingHostRole());

        Assert.Contains(BingHostRoleExtensions.ConfigurationKey, exception.Message, StringComparison.Ordinal);
    }
}
