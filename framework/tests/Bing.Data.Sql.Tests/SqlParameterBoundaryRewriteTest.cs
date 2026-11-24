using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Tests;

/// <summary>参数标记替换应同步维护冻结位置，且不解析字符串和注释中的伪标记。</summary>
public sealed class SqlParameterBoundaryRewriteTest
{
    /// <summary>多次增减参数长度时，已调整和未经过的边界都保持正确。</summary>
    [Fact]
    public void ReplaceParameterTokens_ShouldAdjustBoundariesAndSkipQuotedContent()
    {
        const string sql = "Select @a, '@a', \"@a\", [@a], `@a` /* @a */\r\nFrom [t] Where [Id]=@a Or [X]=@abc";
        const string expected = "Select @consumer_a, '@a', \"@a\", [@a], `@a` /* @a */\r\nFrom [t] Where [Id]=@consumer_a Or [X]=@x";
        var boundaries = new[] { 0, sql.IndexOf("From", StringComparison.Ordinal), sql.Length, 0 };
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["@a"] = "@consumer_a", ["@abc"] = "@x"
        };
        Assert.Equal(expected, SqlBuilderBase.ReplaceParameterTokens(sql, names, true, boundaries));
        Assert.Equal(new[] { 0, expected.IndexOf("From", StringComparison.Ordinal), expected.Length, 0 }, boundaries);
    }

    /// <summary>注释、美元引号和大小写变化不应使后续边界偏移。</summary>
    [Fact]
    public void ReplaceParameterTokens_ShouldKeepCommentAndDollarQuotedBoundaries()
    {
        const string sql = "With x As (Select @ID, $$@id$$ /* @id /* @id */ */)\r\nSelect @id -- @id\r\nFrom x";
        const string expected = "With x As (Select @r_id, $$@id$$ /* @id /* @id */ */)\r\nSelect @r_id -- @id\r\nFrom x";
        var boundaries = new[] { sql.IndexOf("\r\nSelect", StringComparison.Ordinal), sql.IndexOf("From x", StringComparison.Ordinal), sql.Length };
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["@id"] = "@r_id" };
        Assert.Equal(expected, SqlBuilderBase.ReplaceParameterTokens(sql, names, true, boundaries));
        Assert.Equal(new[] { expected.IndexOf("\r\nSelect", StringComparison.Ordinal), expected.IndexOf("From x", StringComparison.Ordinal), expected.Length }, boundaries);
    }
}
