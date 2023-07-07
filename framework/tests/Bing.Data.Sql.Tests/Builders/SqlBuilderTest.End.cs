using Bing.Data.Queries;
using Bing.Properties;
using System;
using System.Text;
using Bing.Data.Sql.Tests.XUnitHelpers;
using Xunit;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - 结束 子句
/// </summary>
public partial class SqlBuilderTest
{
    #region Page

    /// <summary>
    /// 验证分页时未设置排序字段，抛出异常
    /// </summary>
    [Fact]
    public void Test_Page_1()
    {
        var pager = new QueryParameter();
        _builder.From("a").Page(pager);
        AssertHelper.Throws<ArgumentException>(() => _builder.ToSql(), LibraryResource.OrderIsEmptyForPage);
    }

    /// <summary>
    /// 分页时设置了排序字段
    /// </summary>
    [Fact]
    public void Test_Page_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Order By [a] ");
        result.Append("Offset @_p_0 Rows Fetch Next @_p_1 Rows Only");

        //执行
        var pager = new QueryParameter { Order = "a" };
        _builder.From("Test").Page(pager);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(0, _builder.GetParamValue("@_p_0"));
        Assert.Equal(20, _builder.GetParamValue("@_p_1"));
    }

    #endregion
}
