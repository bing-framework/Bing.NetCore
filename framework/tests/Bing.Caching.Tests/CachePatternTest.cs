using Bing.Caching;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// 验证缓存逻辑键模式的通配和反斜线转义语义。
/// </summary>
public sealed class CachePatternTest
{
    /// <summary>
    /// 验证 glob 通配符、转义和字面量匹配语义。
    /// </summary>
    /// <param name="pattern">待解析的 glob 键匹配模式。</param>
    /// <param name="input">待匹配的逻辑键文本。</param>
    /// <param name="expected">预期的匹配结果。</param>
    [Theory]
    [InlineData("item-*", "item-", true)]
    [InlineData("item-*", "item-42", true)]
    [InlineData("item-?", "item-a", true)]
    [InlineData("item-?", "item-ab", false)]
    [InlineData(@"report\*", "report*", true)]
    [InlineData(@"report\*", "report-1", false)]
    [InlineData(@"report\?", "report?", true)]
    [InlineData(@"report\?", "report1", false)]
    [InlineData(@"path\\item", @"path\item", true)]
    [InlineData(@"path\\item", "path-item", false)]
    [InlineData("literal.+", "literal.+", true)]
    public void IsMatch_UsesGlobAndEscapeSemantics(string pattern, string input, bool expected)
    {
        Assert.Equal(expected, CachePattern.IsMatch(input, pattern));
    }

    /// <summary>
    /// 验证模式末尾反斜线匹配字面量反斜线。
    /// </summary>
    [Fact]
    public void TrailingBackslashMatchesLiteralBackslash()
    {
        Assert.True(CachePattern.IsMatch(@"path\", @"path\"));
        Assert.False(CachePattern.IsMatch(@"path\", "path"));
    }

    /// <summary>
    /// 验证 Unicode 和转义模式使用同一解析器匹配。
    /// </summary>
    /// <param name="pattern">待解析的 glob 键匹配模式。</param>
    /// <param name="input">待匹配的逻辑键文本。</param>
    /// <param name="expected">预期的匹配结果。</param>
    [Theory]
    [InlineData("订单*?", "订单缓存1", true)]
    [InlineData("订单*?", "订单", false)]
    [InlineData(@"path\\*", @"path\nested", true)]
    [InlineData(@"path\\*", "path/nested", false)]
    public void IsMatch_UsesSingleParsedGlobMatcherForUnicodeAndEscapes(string pattern, string input,
        bool expected)
    {
        Assert.Equal(expected, CachePattern.IsMatch(input, pattern));
    }

    /// <summary>
    /// 验证 IsMatch 与公开正则表示保持兼容。
    /// </summary>
    [Fact]
    public void IsMatch_RemainsCompatibleWithPublicRegexRepresentation()
    {
        var patterns = new[] { "item-*", "*item", "item-?", @"report\*", @"path\\item", "订单*?", "订单?", "a*b*c" };
        var inputs = new[] { "item-1", "item", "prefix-item", "report*", @"path\item", "订单缓存1", "订单😀", "abc" };

        foreach (var pattern in patterns)
        {
            var regex = CachePattern.CreateRegex(pattern);
            foreach (var input in inputs)
                Assert.Equal(regex.IsMatch(input), CachePattern.IsMatch(input, pattern));
        }
    }

    /// <summary>
    /// 验证匹配器只提取安全的字面量前缀。
    /// </summary>
    /// <param name="pattern">待解析的 glob 键匹配模式。</param>
    /// <param name="expectedPrefix">预期提取出的字面量前缀。</param>
    [Theory]
    [InlineData("item-*", "item-")]
    [InlineData("订单*?", "订单")]
    [InlineData(@"report\*", "report*")]
    [InlineData("*item", "")]
    public void PatternMatcher_ExtractsOnlySafeLiteralPrefix(string pattern, string expectedPrefix)
    {
        var configuration = new CacheConfiguration("pattern-scan", "redis");
        var matcher = CachePattern.CreateMatcher(pattern);

        Assert.Equal(expectedPrefix, matcher.LiteralPrefix);
        var expectedPattern = string.IsNullOrEmpty(expectedPrefix)
            ? CacheKeyFormat.GetNamespacePattern(configuration)
            : CacheKeyFormat.GetPrefixPattern(configuration, expectedPrefix);
        Assert.Equal(expectedPattern, CacheKeyFormat.GetPatternScanPattern(configuration, matcher));
    }

    /// <summary>
    /// 验证匹配器识别纯字面量模式。
    /// </summary>
    /// <param name="pattern">待解析的 glob 键匹配模式。</param>
    /// <param name="expected">预期是否为纯字面量模式。</param>
    [Theory]
    [InlineData("item", true)]
    [InlineData(@"report\*", true)]
    [InlineData(@"path\\", true)]
    [InlineData("item-*", false)]
    [InlineData("*item", false)]
    [InlineData("item-?", false)]
    public void PatternMatcher_IdentifiesLiteralPatterns(string pattern, bool expected)
    {
        Assert.Equal(expected, CachePattern.CreateMatcher(pattern).IsLiteral);
    }

    /// <summary>
    /// 验证全命名空间通配模式被拒绝，同时保留转义字面量匹配。
    /// </summary>
    [Fact]
    public void NamespaceWideWildcardPatternsAreRejected()
    {
        foreach (var pattern in new[] { "*", "**", "?", "*?" })
            Assert.Throws<ArgumentException>(() => CachePattern.CreateRegex(pattern));

        Assert.True(CachePattern.IsMatch("*", @"\*"));
        Assert.True(CachePattern.IsMatch("?", @"\?"));
    }
}
