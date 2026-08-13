using Bing.Data.Filters;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Sql.Tests.XUnitHelpers;
using Bing.Test.Shared;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - 其它操作
/// </summary>
public partial class SqlBuilderTest
{
    #region Filter

    /// <summary>
    /// 测试 - 根表软删除谓词必须进入 Where，Join 表没有软删除实体时不得额外生成条件。
    /// </summary>
    [Fact]
    public void Test_IsDeletedFilter_1()
    {
        // 目标 SQL：根表谓词位于 Where。
        const string expectedSql = "Select [s].[StringValue] \r\nFrom [Sample5] As [s] \r\nJoin [Sample2] As [s2] On [s].[IntValue]=[s2].[IntValue] \r\nWhere [s].[IsDeleted]=@_p_0";

        // 目标参数：渲染快照中的 @_p_0 = false。
        // 结果语义：没有被逻辑删除的 Sample5 才能作为根结果返回；原 Builder 参数不被渲染污染。

        //执行
        _builder.Select<Sample5>(t => t.StringValue).From<Sample5>("s").Join<Sample2>("s2").On<Sample5, Sample2>((l, r) => l.IntValue == r.IntValue);

        //验证
        _output.WriteLine(_builder.ToSql());
        SqlAssert.Equal(expectedSql, _builder.ToSql(), _builder.Provider.Key);
        Assert.Empty(_builder.GetSqlParams());
    }

    /// <summary>
    /// 测试 - Inner Join 的软删除谓词必须进入 On，根表谓词仍进入 Where。
    /// </summary>
    [Fact]
    public void Test_IsDeletedFilter_2()
    {
        // 目标 SQL：Join 表谓词位于 On，避免与根表过滤混淆。
        const string expectedSql = "Select [s].[StringValue] \r\nFrom [Sample5] As [s] \r\nJoin [Sample6] As [s2] On [s].[IntValue]=[s2].[IntValue] And [s2].[IsDeleted]=@_p_1 \r\nWhere [s].[IsDeleted]=@_p_0";

        // 目标参数：渲染快照中的 @_p_0 = false；@_p_1 = false。
        // 结果语义：仅匹配未删除的根表和关联表记录；原 Builder 参数不被渲染污染。

        //执行
        _builder.Select<Sample5>(t => t.StringValue)
            .From<Sample5>("s")
            .Join<Sample6>("s2").On<Sample5, Sample6>((l, r) => l.IntValue == r.IntValue);

        //验证
        _output.WriteLine(_builder.ToSql());
        SqlAssert.Equal(expectedSql, _builder.ToSql(), _builder.Provider.Key);
        Assert.Empty(_builder.GetSqlParams());
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
        SqlAssert.Equal(expectedSql, first, _builder.Provider.Key);
        SqlAssert.Equal(expectedSql, second, _builder.Provider.Key);
        Assert.Empty(_builder.GetSqlParams());
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
        const string disabledExpectedSql = "Select [s].[StringValue] \r\nFrom [Sample5] As [s]";
        const string restoredExpectedSql = "Select [s].[StringValue] \r\nFrom [Sample5] As [s] \r\nWhere [s].[IsDeleted]=@_p_0";
        SqlAssert.Equal(disabledExpectedSql, disabledSql, builder.Provider.Key);
        SqlAssert.Equal(restoredExpectedSql, restoredSql, builder.Provider.Key);
        Assert.Empty(builder.GetSqlParams());
    }

    /// <summary>
    /// 测试 - 统一 Mutation Builder 冻结写入命令时，软删除边界 SQL 与参数必须来自同一渲染快照。
    /// </summary>
    [Fact]
    public void ToSqlWriteCommand_WhenUnifiedDeleteUsesSoftDeleteBoundary_ShouldFreezeMatchingSqlAndParameters()
    {
        // 目标 SQL：调用方条件和默认 IsDeleted=false 边界均进入 Delete Where。
        const string expectedSql = "Delete From [Sample5] Where [IntValue]=@_p_0 And [IsDeleted]=@_p_1";

        // 目标参数：@_p_0 = 7；@_p_1 = false。
        // 结果语义：动态渲染边界不会造成 SQL token 与冻结参数集合不一致。
        _builder.DeleteClause.From(new SqlTableReference { EntityType = typeof(Sample5), TableName = "Sample5" });
        _builder.Where<Sample5, int>(item => item.IntValue, 7);

        // Act
        var command = _builder.ToSqlWriteCommand();

        // Assert
        SqlAssert.Equal(expectedSql, command.Sql, _builder.Provider.Key, command.Parameters);
        SqlParameterAssert.Equal(command.Parameters, "@_p_0", 7);
        SqlParameterAssert.Equal(command.Parameters, "@_p_1", false);
        Assert.Single(_builder.GetSqlParams());
    }

    /// <summary>
    /// 测试 - 启用租户过滤时，结构化根表查询必须追加参数化 TenantId 谓词。
    /// </summary>
    [Fact]
    public void TenantIdFilter_WhenTenantEntityIsQueried_ShouldAppendParameterizedBoundary()
    {
        // Arrange
        const string expectedSql = "Select [t].[Name] \r\nFrom [TenantFilterSample] As [t] \r\nWhere [t].[TenantId]=@_p_0";
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { new TenantIdFilter(new TestTenantFilterContributor("tenant-a")) });
        var builder = new TestSqlBuilder(services, TestDialect.Instance);

        // Act
        builder.Select<TenantFilterSample>(item => item.Name).From<TenantFilterSample>("t");
        var sql = builder.ToSql();

        // Assert
        SqlAssert.Equal(expectedSql, sql, builder.Provider.Key);
        Assert.Empty(builder.GetSqlParams());
    }

    /// <summary>
    /// 测试 - 启用租户过滤但未解析当前租户值时，结构化查询必须拒绝渲染。
    /// </summary>
    [Fact]
    public void TenantIdFilter_WhenCurrentTenantIsMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { new TenantIdFilter(new TestTenantFilterContributor(null)) });
        var builder = new TestSqlBuilder(services, TestDialect.Instance);
        builder.Select<TenantFilterSample>(item => item.Name).From<TenantFilterSample>("t");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("租户过滤已启用，但实体 TenantFilterSample 未解析到当前租户值。", exception.Message);
    }

    /// <summary>
    /// 测试 - 租户过滤禁用作用域释放后，结构化查询必须恢复租户数据边界。
    /// </summary>
    [Fact]
    public void TenantIdFilter_WhenDisabledInScope_ShouldOmitBoundaryAndRestoreAfterDispose()
    {
        // Arrange
        const string disabledExpectedSql = "Select [t].[Name] \r\nFrom [TenantFilterSample] As [t]";
        const string restoredExpectedSql = "Select [t].[Name] \r\nFrom [TenantFilterSample] As [t] \r\nWhere [t].[TenantId]=@_p_0";
        var dataFilter = new DataFilter();
        var services = new SqlBuilderServices(dataFilter: dataFilter,
            filters: new ISqlFilter[] { new TenantIdFilter(new TestTenantFilterContributor("tenant-a")) });
        var builder = new TestSqlBuilder(services, TestDialect.Instance);
        builder.Select<TenantFilterSample>(item => item.Name).From<TenantFilterSample>("t");

        // Act
        string disabledSql;
        using (dataFilter.Disable<TenantIdFilter>())
            disabledSql = builder.ToSql();
        var restoredSql = builder.ToSql();

        // Assert
        SqlAssert.Equal(disabledExpectedSql, disabledSql, builder.Provider.Key);
        SqlAssert.Equal(restoredExpectedSql, restoredSql, builder.Provider.Key);
        Assert.Empty(builder.GetSqlParams());
    }

    /// <summary>
    /// 测试 - 原始 From SQL 缺少实体映射信息时，租户过滤不得猜测或改写调用方 SQL。
    /// </summary>
    [Fact]
    public void TenantIdFilter_WhenRawFromIsConfigured_ShouldNotRewriteCallerOwnedSql()
    {
        // Arrange
        const string expectedSql = "Select [r].[Name] \r\nFrom TenantFilterSample r";
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { new TenantIdFilter(new TestTenantFilterContributor("tenant-a")) });
        var builder = new TestSqlBuilder(services, TestDialect.Instance);

        // Act
        var sql = builder.Select("r.Name").AppendFrom("TenantFilterSample r").ToSql();

        // Assert
        SqlAssert.Equal(expectedSql, sql, builder.Provider.Key);
        Assert.Empty(builder.GetSqlParams());
    }

    private sealed class TenantFilterSample
    {
        public int Id { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
    }

    private sealed class TestTenantFilterContributor : ISqlTenantFilterContributor
    {
        private readonly string _tenantId;

        public TestTenantFilterContributor(string tenantId) => _tenantId = tenantId;

        public bool IsTenantEntity(Type entityType) => entityType == typeof(TenantFilterSample);

        public object GetTenantId(SqlTenantFilterContext context) => _tenantId;
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
        XUnitHelpers.AssertHelper.Throws<InvalidOperationException>(() => _builder.ToSql());
    }

    /// <summary>
    /// 设置查询条件 - 验证列名为空
    /// </summary>
    [Fact]
    public void Test_Validate_2()
    {
        XUnitHelpers.AssertHelper.Throws<ArgumentNullException>(() => _builder.Where("", "a"));
    }

    #endregion
}
