using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;
using Bing.Extensions;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>
/// Sql项测试
/// </summary>
public class SqlItemTest
{
    /// <summary>解析出的非空前缀优先，空前缀继续使用显式传入值。</summary>
    [Theory]
    [InlineData(".name", "[explicit].[name]")]
    [InlineData("parsed.name", "[parsed].[name]")]
    [InlineData("archive..name", "[archive].[explicit].[name]")]
    public void Parse_UnquotedPrefix_ShouldPreserveExplicitFallback(string name, string expectedSql)
    {
        Assert.Equal(expectedSql, SqlItem.Parse(name, prefix: "explicit").ToSql(TestDialect.Instance));
    }

    /// <summary>常见无别名名称避免正则及临时分词集合，完整 SQL 和显式别名保持一致。</summary>
    [Theory]
    [InlineData("users", "[users] As [u]")]
    [InlineData("public.users", "[public].[users] As [u]")]
    [InlineData("archive.public.users", "[archive].[public].[users] As [u]")]
    public void Parse_CompactName_ShouldAvoidTokenCollections(string name, string expected)
    {
        for (var i = 0; i < 64; i++) SqlItem.Parse(name, alias: "u");
        const int iterations = 100;
        SqlItem last = null;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++) last = SqlItem.Parse(name, alias: "u");
        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(expected, last.ToSql(TestDialect.Instance));
        // 含三段标识符的既有 NameItem 解析仍可分配；这里只约束不再创建别名分词集合。
        Assert.True(bytes / iterations <= 400, $"Allocated {bytes / iterations} bytes per parse.");
    }

    /// <summary>复杂标识符、空白和 AS 继续使用原有解析语义。</summary>
    [Theory]
    [InlineData(" public.users AS u ", "[public].[users] As [u]")]
    [InlineData("public.users\tAS\tu", "[public].[users] As [u]")]
    [InlineData("[public].[users]", "[public].[users]")]
    [InlineData("\"public\".\"users\"", "[public].[users]")]
    [InlineData("public . users u", "[public].[users] As [u]")]
    public void Parse_ComplexOrAliasedName_ShouldPreserveSql(string name, string expected)
    {
        Assert.Equal(expected, SqlItem.Parse(name).ToSql(TestDialect.Instance));
    }

    /// <summary>
    /// 只设置名称
    /// </summary>
    [Fact]
    public void Test_1()
    {
        var item = new SqlItem("a");
        Assert.Equal("a", item.Name);
        Assert.True(item.Prefix.IsEmpty());
        Assert.True(item.Alias.IsEmpty());

        //测试复制副本
        var copy = item.Clone();
        Assert.Equal("a", copy.Name);
        Assert.True(copy.Prefix.IsEmpty());
        Assert.True(copy.Alias.IsEmpty());
    }

    /// <summary>
    /// 设置名称和前缀
    /// </summary>
    [Fact]
    public void Test_2()
    {
        var item = new SqlItem("t.a");
        Assert.Equal("a", item.Name);
        Assert.Equal("t", item.Prefix);
        Assert.True(item.Alias.IsEmpty());
    }

    /// <summary>
    /// 设置名称和前缀 - 增加空格
    /// </summary>
    [Fact]
    public void Test_3()
    {
        var item = new SqlItem("  t  .  a  ");
        Assert.Equal("a", item.Name);
        Assert.Equal("t", item.Prefix);
        Assert.True(item.Alias.IsEmpty());
    }

    /// <summary>
    /// 名称中带别名
    /// </summary>
    [Fact]
    public void Test_4()
    {
        var item = new SqlItem("t . a   aS   b ");
        Assert.Equal("a", item.Name);
        Assert.Equal("t", item.Prefix);
        Assert.Equal("b", item.Alias);
        Assert.Equal("[t].[a] As [b]", item.ToSql(TestDialect.Instance));
    }

    /// <summary>
    /// 设置别名
    /// </summary>
    [Fact]
    public void Test_5()
    {
        var item = new SqlItem("a", alias: "b");
        Assert.Equal("a", item.Name);
        Assert.True(item.Prefix.IsEmpty());
        Assert.Equal("b", item.Alias);
    }

    /// <summary>
    /// 设置别名
    /// </summary>
    [Fact]
    public void Test_6()
    {
        var item = new SqlItem("a as c", alias: "b");
        Assert.Equal("a", item.Name);
        Assert.True(item.Prefix.IsEmpty());
        Assert.Equal("c", item.Alias);
    }

    /// <summary>
    /// 设置前缀
    /// </summary>
    [Fact]
    public void Test_7()
    {
        var item = new SqlItem("a as c", "d", "b");
        Assert.Equal("a", item.Name);
        Assert.Equal("d", item.Prefix);
        Assert.Equal("c", item.Alias);
    }

    /// <summary>
    /// 只设置名称 - 不分割名称
    /// </summary>
    [Fact]
    public void Test_8()
    {
        var item = new SqlItem("a", isSplit: false);
        Assert.Equal("a", item.Name);
        Assert.True(item.Prefix.IsEmpty());
        Assert.True(item.Alias.IsEmpty());
    }

    /// <summary>
    /// 只设置名称 - 不分割名称 - 名称带句点
    /// </summary>
    [Fact]
    public void Test_9()
    {
        var item = new SqlItem("a.b", isSplit: false);
        Assert.Equal("a.b", item.Name);
        Assert.True(item.Prefix.IsEmpty());
        Assert.True(item.Alias.IsEmpty());
    }

    /// <summary>
    /// 带双引号
    /// </summary>
    [Fact]
    public void Test_10()
    {
        var item = new SqlItem("\"a\".\"b\"");
        Assert.Equal("\"b\"", item.Name);
        Assert.Equal("\"a\"", item.Prefix);
        Assert.Equal("[a].[b]", item.ToSql(TestDialect.Instance));
        //Assert.Equal("\"a\".\"b\"", item.ToSql(new PgSqlDialect()));
        //Assert.Equal("`a`.`b`", item.ToSql(new MySqlDialect()));
    }

    /// <summary>
    /// 带`符号
    /// </summary>
    [Fact]
    public void Test_11()
    {
        var item = new SqlItem("`a`.`b`");
        Assert.Equal("`b`", item.Name);
        Assert.Equal("`a`", item.Prefix);
        Assert.Equal("[a].[b]", item.ToSql(TestDialect.Instance));
        //Assert.Equal("\"a\".\"b\"", item.ToSql(new PgSqlDialect()));
        //Assert.Equal("`a`.`b`", item.ToSql(new MySqlDialect()));
    }

    /// <summary>
    /// 带[]符号
    /// </summary>
    [Fact]
    public void Test_12()
    {
        var item = new SqlItem("[a].[b]");
        Assert.Equal("[b]", item.Name);
        Assert.Equal("[a]", item.Prefix);
        Assert.Equal("[a].[b]", item.ToSql(TestDialect.Instance));
        //Assert.Equal("\"a\".\"b\"", item.ToSql(new PgSqlDialect()));
        //Assert.Equal("`a`.`b`", item.ToSql(new MySqlDialect()));
    }

    /// <summary>
    /// 带双引号 - 前缀带句点
    /// </summary>
    [Fact]
    public void Test_13()
    {
        var item = new SqlItem("\"a.b\".\"c\"");
        Assert.Equal("\"c\"", item.Name);
        Assert.Equal("\"a.b\"", item.Prefix);
        Assert.Equal("[a.b].[c]", item.ToSql(TestDialect.Instance));
        //Assert.Equal("\"a.b\".\"c\"", item.ToSql(new PgSqlDialect()));
        //Assert.Equal("`a.b`.`c`", item.ToSql(new MySqlDialect()));
    }

    /// <summary>
    /// 带`符号 - 前缀带句点
    /// </summary>
    [Fact]
    public void Test_14()
    {
        var item = new SqlItem("`a.b`.`c`");
        Assert.Equal("`c`", item.Name);
        Assert.Equal("`a.b`", item.Prefix);
        Assert.Equal("[a.b].[c]", item.ToSql(TestDialect.Instance));
        //Assert.Equal("\"a.b\".\"c\"", item.ToSql(new PgSqlDialect()));
        //Assert.Equal("`a.b`.`c`", item.ToSql(new MySqlDialect()));
    }

    /// <summary>
    /// 带[]符号 - 前缀带句点
    /// </summary>
    [Fact]
    public void Test_15()
    {
        var item = new SqlItem("[a.b].[c]");
        Assert.Equal("[c]", item.Name);
        Assert.Equal("[a.b]", item.Prefix);
        Assert.Equal("[a.b].[c]", item.ToSql(TestDialect.Instance));
        //Assert.Equal("\"a.b\".\"c\"", item.ToSql(new PgSqlDialect()));
        //Assert.Equal("`a.b`.`c`", item.ToSql(new MySqlDialect()));
    }

    /// <summary>
    /// 带[]符号 - 前缀带句点
    /// </summary>
    [Fact]
    public void Test_16()
    {
        var item = new SqlItem("a.b.c");
        Assert.Equal("c", item.Name);
        Assert.Equal("b", item.Prefix);
        Assert.Equal("a", item.DatabaseName);
        Assert.Equal("[a].[b].[c]", item.ToSql(TestDialect.Instance));
        //Assert.Equal("\"a\".\"b\".\"c\"", item.ToSql(new PgSqlDialect()));
        //Assert.Equal("`a`.`b`.`c`", item.ToSql(new MySqlDialect()));
    }

    /// <summary>
    /// 测试 - Clone 应保留三段对象名中的数据库名称。
    /// </summary>
    [Fact]
    public void Clone_WhenItemHasDatabaseName_ShouldPreserveAllIdentifierSegments()
    {
        // Arrange
        var item = new SqlItem("archive.Users.Id");

        // Act
        var clone = item.Clone();

        // Assert
        Assert.Equal("archive", clone.DatabaseName);
        Assert.Equal("Users", clone.Prefix);
        Assert.Equal("Id", clone.Name);
        Assert.Equal("[archive].[Users].[Id]", clone.ToSql(TestDialect.Instance));
    }
}
