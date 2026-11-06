using System.Globalization;
using Bing.Caching;
using Shouldly;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// <see cref="CacheKey.CreateInvariant(string, object[])"/> 单元测试。
/// </summary>
public class CacheKeyInvariantTest
{
    /// <summary>
    /// 固定区域性工厂在当前区域性变化时仍应生成相同键文本。
    /// </summary>
    [Fact]
    public void CreateInvariant_WithNumberAndDate_ShouldIgnoreCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var frenchCultureKey = CacheKey.CreateInvariant("price:{0:N2}|date:{1:d}", 1234.5m,
                new DateTime(2026, 1, 2));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var englishCultureKey = CacheKey.CreateInvariant("price:{0:N2}|date:{1:d}", 1234.5m,
                new DateTime(2026, 1, 2));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
            var chineseCultureKey = CacheKey.CreateInvariant("price:{0:N2}|date:{1:d}", 1234.5m,
                new DateTime(2026, 1, 2));

            frenchCultureKey.ToString().ShouldBe("price:1,234.50|date:01/02/2026");
            englishCultureKey.ToString().ShouldBe(frenchCultureKey.ToString());
            chineseCultureKey.ToString().ShouldBe(frenchCultureKey.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>
    /// 格式化后的转义大括号应作为普通键文本保留，且前缀仍应正常拼接。
    /// </summary>
    [Fact]
    public void CreateInvariant_WithEscapedBraces_ShouldPreserveBracesAndPrefix()
    {
        var key = CacheKey.CreateInvariant("cache:{{{0}}}", 42);
        key.Prefix = "tenant:";

        key.ToString().ShouldBe("tenant:cache:{42}");
    }

    /// <summary>
    /// 新工厂不应改变旧构造函数的格式化及前缀行为。
    /// </summary>
    [Fact]
    public void Constructor_WithFormatParameters_ShouldKeepExistingBehavior()
    {
        var key = new CacheKey("user:{0}", 42)
        {
            Prefix = "tenant:"
        };

        key.ToString().ShouldBe("tenant:user:42");
    }
}
