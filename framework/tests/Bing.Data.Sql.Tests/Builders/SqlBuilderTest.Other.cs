using Bing.Data.Filters;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Sql.Tests.XUnitHelpers;

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
    /// 测试 - 每个结构化 Join 必须生成独立表源身份，供全局过滤器按别名绑定条件。
    /// </summary>
    [Fact]
    public void JoinSources_WhenSameEntityIsJoinedWithDifferentAliases_ShouldKeepIndependentSourceIds()
    {
        // Arrange
        _builder.From<Sample5>("root").Join<Sample6>("first").LeftJoin<Sample6>("second");

        // Act
        var sources = _builder.GetTypedJoinSources();

        // Assert
        Assert.Equal(new[] { "join_0", "join_1" }, sources.Select(source => source.SourceId));
        Assert.Equal(new[] { "first", "second" }, sources.Select(source => source.Alias));
        Assert.All(sources, source => Assert.Equal(typeof(Sample6), source.EntityType));
    }

    /// <summary>
    /// 测试 - 多次渲染全局过滤查询时，原 Builder 不应累积过滤条件或参数。
    /// </summary>
    [Fact]
    public void ToSql_WhenGlobalFilterIsEnabled_ShouldRenderFromIndependentSnapshot()
    {
        // Arrange
        const string expectedSql = "Select [s].[StringValue] \r\nFrom [Sample5] As [s] \r\nWhere [s].[IsDeleted]=@_p_0";
        _builder.Select<Sample5>(item => item.StringValue).From<Sample5>("s");

        // Act
        var first = _builder.ToSql();
        var second = _builder.ToSql();

        // Assert
        Assert.Equal(expectedSql, first);
        Assert.Equal(expectedSql, second);
        Assert.DoesNotContain("IsDeleted", _builder.GetCondition());
    }

    /// <summary>
    /// 测试 - 结构化 SQL 的逻辑删除谓词应遵循当前异步执行流共享的 IDataFilter 状态，并在作用域释放后恢复。
    /// </summary>
    [Fact]
    public void IsDeletedFilter_WhenSharedDataFilterIsDisabled_ShouldOmitPredicateAndRestoreAfterDispose()
    {
        // Arrange
        var dataFilter = new DataFilter();
        var services = new SqlBuilderServices(dataFilter: dataFilter);
        var builder = new TestSqlBuilder(services, TestDialect.Instance);
        builder.Select<Sample5>(item => item.StringValue).From<Sample5>("s");

        // Act
        string disabledSql;
        using (dataFilter.Disable<ISoftDelete>())
            disabledSql = builder.ToSql();
        var restoredSql = builder.ToSql();

        // Assert
        Assert.Equal("Select [s].[StringValue] \r\nFrom [Sample5] As [s]", disabledSql);
        Assert.Equal("Select [s].[StringValue] \r\nFrom [Sample5] As [s] \r\nWhere [s].[IsDeleted]=@_p_0",
            restoredSql);
    }

    /// <summary>
    /// 测试 - 全局过滤器遇到 Right Join 的保留侧语义不明确时应拒绝渲染，避免生成错误 SQL。
    /// </summary>
    [Fact]
    public void IsDeletedFilter_WhenRightJoinContainsSoftDeleteEntity_ShouldThrowNotSupportedException()
    {
        // Arrange
        _builder.Select<Sample5>(t => t.StringValue)
            .From<Sample5>("s5")
            .Join<Sample6>("s6").On<Sample5, Sample6>((l, r) => l.IntValue == r.IntValue)
            .LeftJoin<Sample7>("s7").On<Sample6, Sample7>((l, r) => l.IntValue == r.IntValue)
            .RightJoin<Sample8>("s8").On<Sample7, Sample8>((l, r) => l.IntValue == r.IntValue);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => _builder.ToSql());

        // Assert
        Assert.Contains("Right Join", exception.Message);
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
