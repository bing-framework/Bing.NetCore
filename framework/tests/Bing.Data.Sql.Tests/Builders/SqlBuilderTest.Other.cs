using System;
using System.Text;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Sql.Tests.XUnitHelpers;
using Xunit;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - 其它操作
/// </summary>
public partial class SqlBuilderTest
{
    #region Filter

    /// <summary>
    /// 测试逻辑删除过滤器 - From子句的逻辑删除添加到Where中
    /// </summary>
    [Fact]
    public void Test_IsDeletedFilter_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [s].[StringValue] ");
        result.AppendLine("From [Sample5] As [s] ");
        result.AppendLine("Join [Sample2] As [s2] On [s].[IntValue]=[s2].[IntValue] ");
        result.Append("Where [s].[IsDeleted]=@_p_0");

        //执行
        _builder.Select<Sample5>(t => t.StringValue).From<Sample5>("s").Join<Sample2>("s2").On<Sample5, Sample2>((l, r) => l.IntValue == r.IntValue);

        //验证
        _output.WriteLine(_builder.ToSql());
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试逻辑删除过滤器 - Join子句的逻辑删除添加到Join中
    /// </summary>
    [Fact]
    public void Test_IsDeletedFilter_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [s].[StringValue] ");
        result.AppendLine("From [Sample5] As [s] ");
        result.AppendLine("Join [Sample6] As [s2] On [s].[IntValue]=[s2].[IntValue] And [s2].[IsDeleted]=@_p_1 ");
        result.Append("Where [s].[IsDeleted]=@_p_0");

        //执行
        _builder.Select<Sample5>(t => t.StringValue)
            .From<Sample5>("s")
            .Join<Sample6>("s2").On<Sample5, Sample6>((l, r) => l.IntValue == r.IntValue);

        //验证
        _output.WriteLine(_builder.ToSql());
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试逻辑删除过滤器 - Join子句的逻辑删除添加到Join中 - 多个Join
    /// </summary>
    [Fact]
    public void Test_IsDeletedFilter_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [s5].[StringValue] ");
        result.AppendLine("From [Sample5] As [s5] ");
        result.AppendLine("Join [Sample6] As [s6] On [s5].[IntValue]=[s6].[IntValue] And [s6].[IsDeleted]=@_p_1 ");
        result.AppendLine("Left Join [Sample7] As [s7] On [s6].[IntValue]=[s7].[IntValue] And [s7].[IsDeleted]=@_p_2 ");
        result.AppendLine("Right Join [Sample8] As [s8] On [s7].[IntValue]=[s8].[IntValue] And [s8].[IsDeleted]=@_p_3 ");
        result.Append("Where [s5].[IsDeleted]=@_p_0");

        //执行
        _builder.Select<Sample5>(t => t.StringValue)
            .From<Sample5>("s5")
            .Join<Sample6>("s6").On<Sample5, Sample6>((l, r) => l.IntValue == r.IntValue)
            .LeftJoin<Sample7>("s7").On<Sample6, Sample7>((l, r) => l.IntValue == r.IntValue)
            .RightJoin<Sample8>("s8").On<Sample7, Sample8>((l, r) => l.IntValue == r.IntValue);

        //验证
        _output.WriteLine(_builder.ToSql());
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    #endregion

    #region IgnoreFilter

    /// <summary>
    /// 测试忽略全局过滤器 - From子句的忽略添加过滤器到Where中
    /// </summary>
    [Fact]
    public void Test_IgnoreFilter_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [s].[StringValue] ");
        result.AppendLine("From [Sample5] As [s] ");
        result.Append("Join [Sample2] As [s2] On [s].[IntValue]=[s2].[IntValue]");

        //执行
        _builder.Select<Sample5>(t => t.StringValue)
            .From<Sample5>("s")
            .Join<Sample2>("s2").On<Sample5, Sample2>((l, r) => l.IntValue == r.IntValue)
            .IgnoreFilter<IsDeletedFilter>();

        //验证
        _output.WriteLine(_builder.ToSql());
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试忽略全局过滤器 - Join子句的忽略添加过滤器到Join中
    /// </summary>
    [Fact]
    public void Test_IgnoreFilter_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [s].[StringValue] ");
        result.AppendLine("From [Sample5] As [s] ");
        result.Append("Join [Sample6] As [s2] On [s].[IntValue]=[s2].[IntValue]");

        //执行
        _builder.Select<Sample5>(t => t.StringValue)
            .From<Sample5>("s")
            .Join<Sample6>("s2").On<Sample5, Sample6>((l, r) => l.IntValue == r.IntValue)
            .IgnoreFilter<IsDeletedFilter>();

        //验证
        _output.WriteLine(_builder.ToSql());
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试忽略全局过滤器 - Join子句的忽略添加过滤器到Join中 - 多个Join
    /// </summary>
    [Fact]
    public void Test_IgnoreFilter_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [s5].[StringValue] ");
        result.AppendLine("From [Sample5] As [s5] ");
        result.AppendLine("Join [Sample6] As [s6] On [s5].[IntValue]=[s6].[IntValue] ");
        result.AppendLine("Left Join [Sample7] As [s7] On [s6].[IntValue]=[s7].[IntValue] ");
        result.Append("Right Join [Sample8] As [s8] On [s7].[IntValue]=[s8].[IntValue]");

        //执行
        _builder.Select<Sample5>(t => t.StringValue)
            .From<Sample5>("s5")
            .Join<Sample6>("s6").On<Sample5, Sample6>((l, r) => l.IntValue == r.IntValue)
            .LeftJoin<Sample7>("s7").On<Sample6, Sample7>((l, r) => l.IntValue == r.IntValue)
            .RightJoin<Sample8>("s8").On<Sample7, Sample8>((l, r) => l.IntValue == r.IntValue)
            .IgnoreFilter<IsDeletedFilter>();

        //验证
        _output.WriteLine(_builder.ToSql());
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    #endregion

    #region Validate

    /// <summary>
    /// 验证表名为空
    /// </summary>
    [Fact]
    public void Test_Validate_1()
    {
        _builder.Select("a");
        AssertHelper.Throws<InvalidOperationException>(() => _builder.ToSql());
    }

    /// <summary>
    /// 设置查询条件 - 验证列名为空
    /// </summary>
    [Fact]
    public void Test_Validate_2()
    {
        AssertHelper.Throws<ArgumentNullException>(() => _builder.Where("", "a"));
    }

    #endregion
}
