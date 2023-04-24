using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>
/// 表测试
/// </summary>
public class TableItemTest
{
    /// <summary>
    /// Sql方言
    /// </summary>
    private readonly IDialect _dialect;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public TableItemTest()
    {
        _dialect = TestDialect.Instance;
    }

    /// <summary>
    /// 测试 - 解析简单表名
    /// </summary>
    [Fact]
    public void Test_1()
    {
        var item = new TableItem(_dialect, "a");
        Assert.Null(item.Schema);
        Assert.Equal("a", item.Name);
        Assert.Null(item.TableAlias);
    }

    /// <summary>
    /// 测试 - 解析带架构的表
    /// </summary>
    [Fact]
    public void Test_2()
    {
        var item = new TableItem(_dialect, "a.b");
        Assert.Equal("a", item.Schema);
        Assert.Equal("b", item.Name);
        Assert.Null(item.TableAlias);
    }

    /// <summary>
    /// 测试 - 解析带架构的表
    /// </summary>
    [Fact]
    public void Test_2_1()
    {
        var item = new TableItem(_dialect, "`a.b`.`c`");
        Assert.Equal("`a.b`", item.Schema);
        Assert.Equal("`c`", item.Name);
        Assert.Null(item.TableAlias);
    }

    /// <summary>
    /// 测试 - 解析带架构和表别名的表 - 使用as关键字
    /// </summary>
    [Fact]
    public void Test_3()
    {
        var item = new TableItem(_dialect, "a.b as c");
        Assert.Equal("a", item.Schema);
        Assert.Equal("b", item.Name);
        Assert.Equal("c", item.TableAlias);
    }

    /// <summary>
    /// 测试 - 解析带架构和表别名的表 - 使用空格
    /// </summary>
    [Fact]
    public void Test_4()
    {
        var item = new TableItem(_dialect, "a.b c");
        Assert.Equal("a", item.Schema);
        Assert.Equal("b", item.Name);
        Assert.Equal("c", item.TableAlias);
    }

    /// <summary>
    /// 测试 - .前后有空格
    /// </summary>
    [Fact]
    public void Test_5()
    {
        var item = new TableItem(_dialect, "a . b");
        Assert.Equal("a", item.Schema);
        Assert.Equal("b", item.Name);
    }

    /// <summary>
    /// 测试 - 多个空格
    /// </summary>
    [Fact]
    public void Test_6()
    {
        var item = new TableItem(_dialect, " a . b  As   c   ");
        Assert.Equal("a", item.Schema);
        Assert.Equal("b", item.Name);
        Assert.Equal("c", item.TableAlias);
    }

    /// <summary>
    /// 测试 - 添加了转义符
    /// </summary>
    [Fact]
    public void Test_7()
    {
        var item = new TableItem(_dialect, "[a].[b] as [c]");
        Assert.Equal("[a]", item.Schema);
        Assert.Equal("[b]", item.Name);
        Assert.Equal("[c]", item.TableAlias);
    }

    /// <summary>
    /// 测试 - 获取Sql结果 - 简单表名
    /// </summary>
    [Fact]
    public void Test_ToResult_1()
    {
        var item = new TableItem(_dialect, "a");
        Assert.Equal("[a]", item.ToResult());
    }

    /// <summary>
    /// 测试 - 获取Sql结果 - 带架构的表
    /// </summary>
    [Fact]
    public void Test_ToResult_2()
    {
        var item = new TableItem(_dialect, "a.b");
        Assert.Equal("[a].[b]", item.ToResult());
    }

    /// <summary>
    /// 测试 - 获取Sql结果 - 带架构和表别名的表
    /// </summary>
    [Fact]
    public void Test_ToResult_3()
    {
        var item = new TableItem(_dialect, "a.b as c");
        Assert.Equal("[a].[b] As [c]", item.ToResult());
    }

    /// <summary>
    /// 测试 - 获取Sql结果 - 添加了转义符和多余的空格
    /// </summary>
    [Fact]
    public void Test_ToResult_4()
    {
        var item = new TableItem(_dialect, "  [a] . [b]  as  [c] ");
        Assert.Equal("[a].[b] As [c]", item.ToResult());
    }
}
