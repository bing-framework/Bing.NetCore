using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>
/// 名称项测试
/// </summary>
public class NameItemTest
{
    /// <summary>普通名称只保留最终名称段，不创建拆分数组或集合。</summary>
    [Theory]
    [InlineData("samples", "[samples]")]
    [InlineData("public.samples", "[public].[samples]")]
    [InlineData("archive.public.samples", "[archive].[public].[samples]")]
    public void Construct_CompactName_ShouldAvoidSplitCollections(string name, string expectedSql)
    {
        for (var i = 0; i < 256; i++) GC.KeepAlive(new NameItem(name));
        const int iterations = 1000;
        NameItem last = null;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++) last = new NameItem(name);
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - start) / iterations;
        GC.KeepAlive(last);
        Assert.Equal(expectedSql, last.ToSql(TestDialect.Instance));
        // 短名称仅需 NameItem 和最多三个名称字符串，不能再携带 Split/ToList 容器。
        Assert.True(bytes <= 160, $"Allocated {bytes} bytes per name.");
    }

    /// <summary>未引用名称保留空段及既有空白读取语义，四段及以上仍不解析。</summary>
    [Theory]
    [InlineData(".samples", null, "", "samples")]
    [InlineData("public.", null, "public", "")]
    [InlineData("archive..samples", "archive", "", "samples")]
    [InlineData(".public.samples", "", "public", "samples")]
    [InlineData("..", "", "", "")]
    [InlineData("a.b.c.d", null, "", "")]
    [InlineData("a.b.c.", null, "", "")]
    [InlineData(" public . samples ", null, "public", "samples")]
    public void Construct_UnquotedSegments_ShouldPreserveExistingValues(
        string input, string database, string prefix, string name)
    {
        var item = new NameItem(input);
        Assert.Equal(database, item.DatabaseName);
        Assert.Equal(prefix, item.Prefix);
        Assert.Equal(name, item.Name);
    }

    /// <summary>
    /// 不带前缀
    /// </summary>
    [Fact]
    public void Test_1()
    {
        var item = new NameItem("a");
        Assert.Equal("a", item.Name);
        Assert.Empty(item.Prefix);
        Assert.Equal("[b].[a]", item.ToSql(TestDialect.Instance, "b"));
    }

    /// <summary>
    /// 不带前缀 - 名称带[]符号
    /// </summary>
    [Fact]
    public void Test_2()
    {
        var item = new NameItem("[a]");
        Assert.Equal("[a]", item.Name);
        Assert.Empty(item.Prefix);
    }

    /// <summary>
    /// 不带前缀 - 名称带[]符号
    /// </summary>
    [Fact]
    public void Test_3()
    {
        var item = new NameItem("[a.b]");
        Assert.Equal("[a.b]", item.Name);
        Assert.Empty(item.Prefix);
    }

    /// <summary>
    /// 不带前缀 - 名称带双引号
    /// </summary>
    [Fact]
    public void Test_4()
    {
        var item = new NameItem("\"a\"");
        Assert.Equal("\"a\"", item.Name);
        Assert.Empty(item.Prefix);
    }

    /// <summary>
    /// 不带前缀 - 名称带`符号
    /// </summary>
    [Fact]
    public void Test_5()
    {
        var item = new NameItem("`a`");
        Assert.Equal("`a`", item.Name);
        Assert.Empty(item.Prefix);
    }

    /// <summary>
    /// 带前缀
    /// </summary>
    [Fact]
    public void Test_6()
    {
        var item = new NameItem("a.b");
        Assert.Equal("b", item.Name);
        Assert.Equal("a", item.Prefix);
    }

    /// <summary>
    /// 带前缀 - 名称和前缀带[]符号
    /// </summary>
    [Fact]
    public void Test_7()
    {
        var item = new NameItem("[a].[b]");
        Assert.Equal("[b]", item.Name);
        Assert.Equal("[a]", item.Prefix);
    }

    /// <summary>
    /// 带前缀 - 名称和前缀带双引号
    /// </summary>
    [Fact]
    public void Test_8()
    {
        var item = new NameItem("\"a\".\"b\"");
        Assert.Equal("\"b\"", item.Name);
        Assert.Equal("\"a\"", item.Prefix);
    }

    /// <summary>
    /// 带前缀 - 名称和前缀带`符号
    /// </summary>
    [Fact]
    public void Test_9()
    {
        var item = new NameItem("`a`.`b`");
        Assert.Equal("`b`", item.Name);
        Assert.Equal("`a`", item.Prefix);
    }

    /// <summary>
    /// 带前缀 - 名称和前缀带[]符号 - 前缀包含.
    /// </summary>
    [Fact]
    public void Test_10()
    {
        var item = new NameItem("[a.b].[c]");
        Assert.Equal("[c]", item.Name);
        Assert.Equal("[a.b]", item.Prefix);
    }
    /// <summary>
    /// 带前缀 - 名称和前缀带双引号 - 前缀包含.
    /// </summary>
    [Fact]
    public void Test_11()
    {
        var item = new NameItem("\"a.b\".\"c\"");
        Assert.Equal("\"c\"", item.Name);
        Assert.Equal("\"a.b\"", item.Prefix);
    }

    /// <summary>
    /// 带前缀 - 名称和前缀带`符号 - 前缀包含.
    /// </summary>
    [Fact]
    public void Test_12()
    {
        var item = new NameItem("`a.b`.`c`");
        Assert.Equal("`c`", item.Name);
        Assert.Equal("`a.b`", item.Prefix);
    }

    /// <summary>
    /// 带前缀 - 名称和前缀带[]符号 - 前缀包含. - 名称包含.
    /// </summary>
    [Fact]
    public void Test_13()
    {
        var item = new NameItem("[a.b].[c.d]");
        Assert.Equal("[c.d]", item.Name);
        Assert.Equal("[a.b]", item.Prefix);
    }

    /// <summary>
    /// 带数据库名称
    /// </summary>
    [Fact]
    public void Test_14()
    {
        var item = new NameItem("a.b.c");
        Assert.Equal("c", item.Name);
        Assert.Equal("b", item.Prefix);
        Assert.Equal("a", item.DatabaseName);
        Assert.Equal("[a].[b].[c]", item.ToSql(TestDialect.Instance, "f"));
    }

    /// <summary>
    /// 带数据库名称 - 带[]符号
    /// </summary>
    [Fact]
    public void Test_15()
    {
        var item = new NameItem("[a].[b].[c]");
        Assert.Equal("[c]", item.Name);
        Assert.Equal("[b]", item.Prefix);
        Assert.Equal("[a]", item.DatabaseName);
    }

    /// <summary>
    /// 带数据库名称 - 带[]符号 - 带.
    /// </summary>
    [Fact]
    public void Test_16()
    {
        var item = new NameItem("[a.b].[c.d].[e.f]");
        Assert.Equal("[e.f]", item.Name);
        Assert.Equal("[c.d]", item.Prefix);
        Assert.Equal("[a.b]", item.DatabaseName);
    }

    /// <summary>
    /// 带数据库名称 - 带`符号 - 带.
    /// </summary>
    [Fact]
    public void Test_17()
    {
        var item = new NameItem("`a.b`.`c.d`.`e.f`");
        Assert.Equal("`e.f`", item.Name);
        Assert.Equal("`c.d`", item.Prefix);
        Assert.Equal("`a.b`", item.DatabaseName);
    }

    /// <summary>
    /// 带数据库名称 - 带"符号 - 带.
    /// </summary>
    [Fact]
    public void Test_18()
    {
        var item = new NameItem("\"a.b\".\"c.d\".\"e.f\"");
        Assert.Equal("\"e.f\"", item.Name);
        Assert.Equal("\"c.d\"", item.Prefix);
        Assert.Equal("\"a.b\"", item.DatabaseName);
    }
}
