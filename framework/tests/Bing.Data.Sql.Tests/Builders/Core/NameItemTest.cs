namespace Bing.Data.Sql.Tests.Builders.Core;

public class NameItemTest
{
    /// <summary>
    /// 测试 - 不带前缀
    /// </summary>
    [Fact]
    public void Test_1()
    {
        var item = new NameItemV1("a");
        Assert.Equal("a", item.Name);
        Assert.Null(item.Prefix);
        Assert.Null(item.Alias);
    }

    /// <summary>
    /// 测试 - 不带前缀 - 名称带[]符号
    /// </summary>
    [Fact]
    public void Test_2()
    {
        var item = new NameItemV1("[a]");
        Assert.Equal("[a]", item.Name);
        Assert.Null(item.Prefix);
        Assert.Null(item.Alias);
    }

    /// <summary>
    /// 测试 - 不带前缀 - 名称带[]符号
    /// </summary>
    [Fact]
    public void Test_3()
    {
        var item = new NameItemV1("[a.b]");
        //Assert.Equal("[a.b]", item.Name);
        Assert.Null(item.Prefix);
        Assert.Null(item.Alias);
    }

    /// <summary>
    /// 测试 - 不带前缀 - 名称带双引号
    /// </summary>
    [Fact]
    public void Test_4()
    {
        var item = new NameItemV1("\"a\"");
        Assert.Equal("\"a\"", item.Name);
        Assert.Null(item.Prefix);
        Assert.Null(item.Alias);
    }

    /// <summary>
    /// 测试 - 不带前缀 - 名称带`符号
    /// </summary>
    [Fact]
    public void Test_5()
    {
        var item = new NameItemV1("`a`");
        Assert.Equal("`a`", item.Name);
        Assert.Null(item.Prefix);
        Assert.Null(item.Alias);
    }

    /// <summary>
    /// 测试 - 带前缀
    /// </summary>
    [Fact]
    public void Test_6()
    {
        var item = new NameItemV1("a.b");
        Assert.Equal("b", item.Name);
        Assert.Equal("a", item.Prefix);
    }

    /// <summary>
    /// 测试 - 带前缀 - 名称和前缀带[]符号
    /// </summary>
    [Fact]
    public void Test_7()
    {
        var item = new NameItemV1("[a].[b]");
        Assert.Equal("[b]", item.Name);
        Assert.Equal("[a]", item.Prefix);
    }

    /// <summary>
    /// 测试 - 带前缀 - 名称和前缀带双引号
    /// </summary>
    [Fact]
    public void Test_8()
    {
        var item = new NameItemV1("\"a\".\"b\"");
        Assert.Equal("\"b\"", item.Name);
        Assert.Equal("\"a\"", item.Prefix);
    }

    /// <summary>
    /// 测试 - 带前缀 - 名称和前缀带`符号
    /// </summary>
    [Fact]
    public void Test_9()
    {
        var item = new NameItemV1("`a`.`b`");
        Assert.Equal("`b`", item.Name);
        Assert.Equal("`a`", item.Prefix);
    }

    /// <summary>
    /// 测试 - 带前缀 - 名称和前缀带[]符号 - 前缀包含.
    /// </summary>
    [Fact]
    public void Test_10()
    {
        var item = new NameItemV1("[a.b].[c]");
        Assert.Equal("[c]", item.Name);
        Assert.Equal("[a.b]", item.Prefix);
    }

    /// <summary>
    /// 测试 - 带前缀 - 名称和前缀带双引号 - 前缀包含.
    /// </summary>
    [Fact]
    public void Test_11()
    {
        var item = new NameItemV1("\"a.b\".\"c\"");
        Assert.Equal("\"c\"", item.Name);
        Assert.Equal("\"a.b\"", item.Prefix);
    }

    /// <summary>
    /// 测试 - 带前缀 - 名称和前缀带`符号 - 前缀包含.
    /// </summary>
    [Fact]
    public void Test_12()
    {
        var item = new NameItemV1("`a.b`.`c`");
        Assert.Equal("`c`", item.Name);
        Assert.Equal("`a.b`", item.Prefix);
    }

    /// <summary>
    /// 测试 - 带前缀 - 名称和前缀带[]符号 - 前缀包含. - 名称包含.
    /// </summary>
    [Fact]
    public void Test_13()
    {
        var item = new NameItemV1("[a.b].[c.d]");
        Assert.Equal("[c.d]", item.Name);
        Assert.Equal("[a.b]", item.Prefix);
    }

    /// <summary>
    /// 测试 - 带数据库名称
    /// </summary>
    [Fact]
    public void Test_14()
    {
        var item = new NameItemV1("a.b.c");
        Assert.Equal("c", item.Name);
        Assert.Equal("b", item.Prefix);
        //Assert.Equal("a", item.DatabaseName);
    }

    /// <summary>
    /// 测试 - 带数据库名称 - 带[]符号
    /// </summary>
    [Fact]
    public void Test_15()
    {
        var item = new NameItemV1("[a].[b].[c]");
        Assert.Equal("[c]", item.Name);
        Assert.Equal("[b]", item.Prefix);
        //Assert.Equal("[a]", item.DatabaseName);
    }

    /// <summary>
    /// 测试 - 带数据库名称 - 带[]符号 - 带.
    /// </summary>
    [Fact]
    public void Test_16()
    {
        var item = new NameItemV1("[a.b].[c.d].[e.f]");
        Assert.Equal("[e.f]", item.Name);
        Assert.Equal("[c.d]", item.Prefix);
        //Assert.Equal("[a.b]", item.DatabaseName);
    }

    /// <summary>
    /// 测试 - 带数据库名称 - 带`符号 - 带.
    /// </summary>
    [Fact]
    public void Test_17()
    {
        var item = new NameItemV1("`a.b`.`c.d`.`e.f`");
        Assert.Equal("`e.f`", item.Name);
        Assert.Equal("`c.d`", item.Prefix);
        //Assert.Equal("`a.b`", item.DatabaseName);
    }

    /// <summary>
    /// 测试 - 带数据库名称 - 带"符号 - 带.
    /// </summary>
    [Fact]
    public void Test_18()
    {
        var item = new NameItemV1("\"a.b\".\"c.d\".\"e.f\"");
        Assert.Equal("\"e.f\"", item.Name);
        Assert.Equal("\"c.d\"", item.Prefix);
        //Assert.Equal("\"a.b\"", item.DatabaseName);
    }
}
