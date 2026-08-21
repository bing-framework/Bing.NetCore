using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Enums;
using System.Linq.Expressions;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// SQL Builder 子查询组合测试。
/// </summary>
public class SqlBuilderSubqueryCompositionTest
{
    /// <summary>
    /// 测试目的：子查询 From 在当前 Mutation 状态不允许查询转换时，应在渲染和合并参数前失败。
    /// </summary>
    [Fact]
    public void From_WhenBuilderIsMutation_ShouldRejectBeforeMergingSubqueryParameters()
    {
        // Arrange
        var outer = new TestSqlBuilder()
            .Update(new SqlTableReference { TableName = "outer" })
            .Set("Name", "existing");
        var subquery = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.From(subquery, "summary"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", exception.Message);
        Assert.Equal(SqlOperationKind.Update, outer.OperationKind);
        Assert.Equal(new object[] { "existing" }, outer.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：子查询 Join 在当前 Mutation 状态不允许查询转换时，应在渲染和合并参数前失败。
    /// </summary>
    [Fact]
    public void Join_WhenBuilderIsMutation_ShouldRejectBeforeMergingSubqueryParameters()
    {
        // Arrange
        var outer = new TestSqlBuilder()
            .Update(new SqlTableReference { TableName = "outer" })
            .Set("Name", "existing");
        var subquery = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.Join(subquery, "summary"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", exception.Message);
        Assert.Equal(SqlOperationKind.Update, outer.OperationKind);
        Assert.Equal(new object[] { "existing" }, outer.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：Mutation 状态下的委托 From 应在执行委托前拒绝，不能由闭包污染父 Builder。
    /// </summary>
    [Fact]
    public void FromAction_WhenBuilderIsMutation_ShouldRejectBeforeExecutingAction()
    {
        // Arrange
        var outer = CreateUpdateBuilder();
        var executed = false;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.From(_ => executed = true, "summary"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", exception.Message);
        Assert.False(executed);
        Assert.Equal(SqlOperationKind.Update, outer.OperationKind);
        Assert.Equal(new object[] { "existing" }, outer.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：Mutation 状态下的各类委托 Join 应在执行委托前拒绝，不能由闭包污染父 Builder。
    /// </summary>
    [Fact]
    public void JoinActions_WhenBuilderIsMutation_ShouldRejectBeforeExecutingAction()
    {
        // Arrange
        var join = CreateUpdateBuilder();
        var leftJoin = CreateUpdateBuilder();
        var rightJoin = CreateUpdateBuilder();
        var joinExecuted = false;
        var leftJoinExecuted = false;
        var rightJoinExecuted = false;

        // Act
        var joinException = Assert.Throws<InvalidOperationException>(() =>
            join.Join(_ => joinExecuted = true, "summary"));
        var leftJoinException = Assert.Throws<InvalidOperationException>(() =>
            leftJoin.LeftJoin(_ => leftJoinExecuted = true, "summary"));
        var rightJoinException = Assert.Throws<InvalidOperationException>(() =>
            rightJoin.RightJoin(_ => rightJoinExecuted = true, "summary"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", joinException.Message);
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", leftJoinException.Message);
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", rightJoinException.Message);
        Assert.False(joinExecuted);
        Assert.False(leftJoinExecuted);
        Assert.False(rightJoinExecuted);
        Assert.Equal(new object[] { "existing" }, join.GetParams().Values.ToArray());
        Assert.Equal(new object[] { "existing" }, leftJoin.GetParams().Values.ToArray());
        Assert.Equal(new object[] { "existing" }, rightJoin.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：Mutation 状态下的委托查询条件应在执行委托前拒绝，不能由闭包污染父 Builder。
    /// </summary>
    [Fact]
    public void WhereAction_WhenBuilderIsMutation_ShouldRejectBeforeExecutingAction()
    {
        // Arrange
        var outer = CreateUpdateBuilder();
        var executed = false;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            outer.Where("Id", _ => executed = true, Operator.In));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", exception.Message);
        Assert.False(executed);
        Assert.Equal(SqlOperationKind.Update, outer.OperationKind);
        Assert.Equal(new object[] { "existing" }, outer.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：Mutation 状态下的委托集合和存在性条件应在执行委托前拒绝，不能由闭包污染父 Builder。
    /// </summary>
    [Fact]
    public void SetSubqueryActions_WhenBuilderIsMutation_ShouldRejectBeforeExecutingAction()
    {
        // Arrange
        var inBuilder = CreateUpdateBuilder();
        var notInBuilder = CreateUpdateBuilder();
        var existsBuilder = CreateUpdateBuilder();
        var notExistsBuilder = CreateUpdateBuilder();
        var inExecuted = false;
        var notInExecuted = false;
        var existsExecuted = false;
        var notExistsExecuted = false;

        // Act
        var inException = Assert.Throws<InvalidOperationException>(() =>
            inBuilder.In("Id", _ => inExecuted = true));
        var notInException = Assert.Throws<InvalidOperationException>(() =>
            notInBuilder.NotIn("Id", _ => notInExecuted = true));
        var existsException = Assert.Throws<InvalidOperationException>(() =>
            existsBuilder.Exists(_ => existsExecuted = true));
        var notExistsException = Assert.Throws<InvalidOperationException>(() =>
            notExistsBuilder.NotExists(_ => notExistsExecuted = true));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", inException.Message);
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", notInException.Message);
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", existsException.Message);
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", notExistsException.Message);
        Assert.False(inExecuted);
        Assert.False(notInExecuted);
        Assert.False(existsExecuted);
        Assert.False(notExistsExecuted);
        Assert.Equal(new object[] { "existing" }, inBuilder.GetParams().Values.ToArray());
        Assert.Equal(new object[] { "existing" }, notInBuilder.GetParams().Values.ToArray());
        Assert.Equal(new object[] { "existing" }, existsBuilder.GetParams().Values.ToArray());
        Assert.Equal(new object[] { "existing" }, notExistsBuilder.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：子查询别名与原始根表冲突时，应在渲染和合并参数前失败。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryAliasDuplicatesRawFromAlias_ShouldRejectBeforeMergingParameters()
    {
        // Arrange
        var outer = new TestSqlBuilder()
            .Select("*")
            .From("outer", "summary")
            .Where("Id", 1);
        var subquery = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 2);
        var expectedSql = outer.ToSql();
        var expectedParameters = outer.GetParams();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.Join(subquery, "summary"));

        // Assert
        Assert.Equal("查询中已存在表别名 \"summary\"。", exception.Message);
        Assert.Equal(expectedSql, outer.ToSql());
        Assert.Equal(expectedParameters, outer.GetParams());
    }

    /// <summary>
    /// 测试目的：子查询参数超过上限或别名重复时，失败不得污染外层参数、SQL 或别名注册状态。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryParameterLimitExceeded_ShouldKeepParametersSqlAndAliasStateUnchanged()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 2, "test");
        var outer = new TestSqlBuilder(parameterManager: parameterManager)
            .Select("*")
            .From("outer")
            .Where("Id", 1);
        var oversized = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 2)
            .Where("Name", "invalid");
        var expectedSql = outer.ToSql();
        var expectedParameters = outer.GetParams();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.Join(oversized, "summary"));

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 2；尝试添加后数量: 3；最大参数数量: 2。", exception.Message);
        Assert.Equal(expectedSql, outer.ToSql());
        Assert.Equal(expectedParameters, outer.GetParams());

        // Act
        var valid = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 2);
        outer.Join(valid, "summary");

        // Assert
        Assert.Equal(new object[] { 1, 2 }, outer.GetParams().Values.ToArray());
        Assert.Equal("Select * \r\nFrom [outer] \r\nJoin (Select * \r\nFrom [source] \r\nWhere [Id]=@_p_1) As [summary] \r\nWhere [Id]=@_p_0", outer.ToSql());
    }

    /// <summary>
    /// 测试目的：子查询 From 的派生表别名格式化失败时，不得合并子查询参数、释放既有别名或替换根来源。
    /// </summary>
    [Fact]
    public void From_WhenSubqueryAliasFormattingFails_ShouldKeepParametersSqlAndAliasStateUnchanged()
    {
        // Arrange
        var dialect = new FailingSubqueryAliasDialect();
        var outer = new TestSqlBuilder(dialect)
            .Select("*")
            .From("outer", "outer_alias")
            .Where("Id", 1);
        var subquery = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 2);
        var expectedSql = outer.ToSql();
        var expectedParameters = outer.GetParams();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.From(subquery, "summary"));

        // Assert
        Assert.Equal("Subquery alias formatting failed.", exception.Message);
        Assert.Equal(expectedSql, outer.ToSql());
        Assert.Equal(expectedParameters, outer.GetParams());
        dialect.ShouldFail = false;
        outer.From(subquery, "summary");
        Assert.Equal("Select * \r\nFrom (Select * \r\nFrom [source] \r\nWhere [Id]=@_p_1) As [summary] \r\nWhere [Id]=@_p_0", outer.ToSql());
    }

    /// <summary>
    /// 测试目的：子查询 Join 的派生表别名格式化失败时，不得合并子查询参数或注册未提交的连接别名。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryAliasFormattingFails_ShouldKeepParametersSqlAndAliasStateUnchanged()
    {
        // Arrange
        var dialect = new FailingSubqueryAliasDialect();
        var outer = new TestSqlBuilder(dialect)
            .Select("*")
            .From("outer")
            .Where("Id", 1);
        var subquery = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 2);
        var expectedSql = outer.ToSql();
        var expectedParameters = outer.GetParams();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.Join(subquery, "summary"));

        // Assert
        Assert.Equal("Subquery alias formatting failed.", exception.Message);
        Assert.Equal(expectedSql, outer.ToSql());
        Assert.Equal(expectedParameters, outer.GetParams());
        dialect.ShouldFail = false;
        outer.Join(subquery, "summary");
        Assert.Equal("Select * \r\nFrom [outer] \r\nJoin (Select * \r\nFrom [source] \r\nWhere [Id]=@_p_1) As [summary] \r\nWhere [Id]=@_p_0", outer.ToSql());
    }

    /// <summary>
    /// 测试目的：子查询参数合并在副本预演成功后，应保留重命名并生成完整 SQL。
    /// </summary>
    [Fact]
    public void RenderSubquery_WhenParameterNamesConflict_ShouldUsePlannedNamesWithoutChangingSource()
    {
        // Arrange
        var outer = new TestSqlBuilder()
            .Select("*")
            .From("outer")
            .Where("Id", 1);
        var source = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 2);
        var sourceSql = source.ToSql();

        // Act
        var sql = outer.RenderSubqueryForTest(source);

        // Assert
        Assert.Equal("Select * \r\nFrom [source] \r\nWhere [Id]=@_p_1", sql);
        Assert.Equal("Select * \r\nFrom [source] \r\nWhere [Id]=@_p_0", sourceSql);
        Assert.Equal(new object[] { 1, 2 }, outer.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：子查询动态软删除过滤的 SQL 和参数必须来自同一快照，避免与外层同名参数错误绑定。
    /// </summary>
    [Fact]
    public void RenderSubquery_WhenSoftDeleteFilterAddsParameter_ShouldMergeFilteredSnapshotParameter()
    {
        // Arrange
        var outer = new TestSqlBuilder()
            .Select("*")
            .From("parents");
        ((ISqlCommonPartAccessor)outer).ParameterManager.Add("@_p_0", 99);
        var child = new TestSqlBuilder()
            .Select("Id")
            .From<Sample5>("child");

        // Act
        var sql = outer.RenderSubqueryForTest(child);

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [Sample5] As [child] \r\nWhere [child].[IsDeleted]=@_p_1", sql);
        Assert.Equal(new object[] { 99, false }, outer.GetParams().Values.ToArray());
        Assert.Empty(child.GetParams());
    }

    /// <summary>
    /// 测试目的：子查询动态租户过滤的 SQL 和参数必须来自同一快照，避免生成未绑定的租户参数。
    /// </summary>
    [Fact]
    public void RenderSubquery_WhenTenantFilterAddsParameter_ShouldMergeFilteredSnapshotParameter()
    {
        // Arrange
        var services = new SqlBuilderServices(filters: new ISqlFilter[]
        {
            new TenantIdFilter(new SubqueryTenantFilterContributor("tenant-a"))
        });
        var outer = new TestSqlBuilder()
            .Select("*")
            .From("parents");
        ((ISqlCommonPartAccessor)outer).ParameterManager.Add("@_p_0", 99);
        var child = new TestSqlBuilder(services, TestDialect.Instance)
            .Select<SubqueryTenantSample>(item => item.Id)
            .From<SubqueryTenantSample>("child");

        // Act
        var sql = outer.RenderSubqueryForTest(child);

        // Assert
        Assert.Equal("Select [child].[Id] \r\nFrom [SubqueryTenantSample] As [child] \r\nWhere [child].[TenantId]=@_p_1", sql);
        Assert.Equal(new object[] { 99, "tenant-a" }, outer.GetParams().Values.ToArray());
        Assert.Empty(child.GetParams());
    }

    /// <summary>
    /// 测试目的：From 子查询入口必须将动态软删除边界的 SQL 和重命名参数一起合并到外层 Builder。
    /// </summary>
    [Fact]
    public void From_WhenSubquerySoftDeleteFilterAddsParameter_ShouldMergeFilteredSnapshotParameter()
    {
        // Arrange
        var outer = new TestSqlBuilder().Select("*");
        ((ISqlCommonPartAccessor)outer).ParameterManager.Add("@_p_0", 99);
        var child = new TestSqlBuilder()
            .Select("Id")
            .From<Sample5>("child");

        // Act
        outer.From(child, "summary");

        // Assert
        Assert.Equal("Select * \r\nFrom (Select [Id] \r\nFrom [Sample5] As [child] \r\nWhere [child].[IsDeleted]=@_p_1) As [summary]",
            outer.ToSql());
        Assert.Equal(new object[] { 99, false }, outer.GetParams().Values.ToArray());
        Assert.Empty(child.GetParams());
    }

    /// <summary>
    /// 测试目的：委托子查询必须继承父 Builder 冻结的租户上下文，不能在构建期间读取后续切换的执行流租户。
    /// </summary>
    [Fact]
    public void Where_WhenDelegatedSubqueryIsBuiltAfterTenantChanges_ShouldKeepParentTenantBoundary()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor
        {
            Current = new DatabaseContext { TenantId = "tenant-a" }
        };
        var services = new SqlBuilderServices(databaseContextAccessor: accessor,
            filters: new ISqlFilter[] { new TenantIdFilter(new ContextTenantFilterContributor()) });
        var builder = new TestSqlBuilder(services, TestDialect.Instance)
            .Select<SubqueryTenantSample>(item => item.Id)
            .From<SubqueryTenantSample>("parent");
        accessor.Current = new DatabaseContext { TenantId = "tenant-b" };

        // Act
        builder.Where("parent.Id", child => child.Select<SubqueryTenantSample>(item => item.Id)
            .From<SubqueryTenantSample>("child"), Operator.In);
        var sql = builder.ToSql();

        // Assert
        Assert.Equal(
            "Select [parent].[Id] \r\nFrom [SubqueryTenantSample] As [parent] \r\nWhere [parent].[Id] In (Select [child].[Id] \r\nFrom [SubqueryTenantSample] As [child] \r\nWhere [child].[TenantId]=@_p_0) And [parent].[TenantId]=@_p_1",
            sql);
        Assert.Equal("tenant-a", builder.GetParams()["@_p_0"]);
    }

    /// <summary>
    /// 测试目的：严格 DTO 派生根来源在 Clone 后应保留投影白名单和 SQL，原 Builder 清除来源后应释放其别名且不影响副本。
    /// </summary>
    [Fact]
    public void From_WhenStrictDtoSubqueryIsClonedAndCleared_ShouldKeepCloneStateAndReleaseSourceAlias()
    {
        // Arrange
        var child = new TestSqlBuilder()
            .Select("Id")
            .From("source");
        var subquery = new SqlSubquery<DerivedSummary>(child, "summary", new[] { nameof(DerivedSummary.Id) },
            "test.sqlserver", null, null, null, null, null);
        var source = new TestSqlBuilder();
        ((FromClause)source.FromClause).From(subquery);
        source.Select("summary.Id");

        // Act
        var clone = (TestSqlBuilder)source.Clone();
        clone.Where("summary.Id", 2);
        source.ClearFrom()
            .ClearSelect()
            .Select("*")
            .From("orders", "summary");

        // Assert
        var cloneFrom = (FromClause)clone.FromClause;
        Assert.Equal(new[] { nameof(DerivedSummary.Id) }, cloneFrom.Sources.Single().ProjectedMembers);
        Assert.Equal("Select [summary].[Id] \r\nFrom (Select [Id] \r\nFrom [source]) As [summary] \r\nWhere [summary].[Id]=@_p_0",
            clone.ToSql());
        Assert.Equal(2, clone.GetParams().Values.Single());
        Assert.Equal("Select * \r\nFrom [orders] As [summary]", source.ToSql());
        Assert.Empty(source.GetParams());
    }

    /// <summary>
    /// 测试目的：严格 DTO 派生 Join 在 Clone 后应保留投影白名单，两个 Builder 清除 Join 后均应独立释放其别名。
    /// </summary>
    [Fact]
    public void Join_WhenStrictDtoSubqueryIsClonedAndCleared_ShouldKeepCloneStateAndReleaseAlias()
    {
        // Arrange
        var child = new TestSqlBuilder()
            .Select("Id")
            .From("source");
        var subquery = new SqlSubquery<DerivedSummary>(child, "summary", new[] { nameof(DerivedSummary.Id) },
            "test.sqlserver", null, null, null, null, null);
        var source = new TestSqlBuilder()
            .Select("order.Id")
            .From("orders", "order");
        ((JoinClause)source.JoinClause).Join(subquery);
        var clone = (TestSqlBuilder)source.Clone();

        // Act
        var cloneSource = ((JoinClause)clone.JoinClause).GetTypedSources().Single();
        var cloneSql = clone.ToSql();
        source.ClearJoin().Join("Invoices", "summary");
        clone.ClearJoin().Join("Payments", "summary");

        // Assert
        Assert.Equal("summary", cloneSource.Alias);
        Assert.Equal(new[] { nameof(DerivedSummary.Id) }, cloneSource.ProjectedMembers);
        Assert.Equal("Select [order].[Id] \r\nFrom [orders] As [order] \r\nJoin (Select [Id] \r\nFrom [source]) As [summary]", cloneSql);
        Assert.Equal("Select [order].[Id] \r\nFrom [orders] As [order] \r\nJoin [Invoices] As [summary]", source.ToSql());
        Assert.Equal("Select [order].[Id] \r\nFrom [orders] As [order] \r\nJoin [Payments] As [summary]", clone.ToSql());
        Assert.Empty(source.GetParams());
        Assert.Empty(clone.GetParams());
    }

    /// <summary>
    /// 测试目的：实体原子 Join 的谓词参数超过上限时，不得提交 Join、候选别名、来源图、Select 或查询操作状态。
    /// </summary>
    [Fact]
    public void TypedEntityJoin_WhenPredicateParameterLimitExceeded_ShouldKeepAllQueryStateUnchanged()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 1, "test");
        var builder = (TestSqlBuilder)new TestSqlBuilder(parameterManager: parameterManager)
            .Select("owner.IntValue")
            .From<Sample>("owner")
            .Where<Sample>(item => item.IntValue, 1);
        var fromClause = (FromClause)builder.FromClause;
        var joinClause = (JoinClause)builder.JoinClause;
        Expression<Func<Sample, Sample2, bool>> predicate = (owner, candidate) => owner.IntValue == 2;
        var expected = CaptureQueryState(builder);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            joinClause.Join<Sample2>(fromClause, predicate, "candidate"));

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 1；尝试添加后数量: 2；最大参数数量: 1。",
            exception.Message);
        AssertQueryState(expected, builder);
    }

    /// <summary>
    /// 测试目的：派生表完成参数渲染后若谓词引用未投影成员，必须回滚渲染参数及全部候选查询状态。
    /// </summary>
    [Fact]
    public void TypedSubqueryJoin_WhenPredicateReferencesUnprojectedMember_ShouldRollbackRenderStateAndAllowRetry()
    {
        // Arrange
        var builder = (TestSqlBuilder)new TestSqlBuilder()
            .Select("owner.IntValue")
            .From<Sample>("owner")
            .Where<Sample>(item => item.IntValue, 1);
        var child = new TestSqlBuilder()
            .Select("IntValue")
            .From("source")
            .Where("Id", 2);
        var subquery = new SqlSubquery<Sample>(child, "summary", new[] { nameof(Sample.IntValue) },
            "test.sqlserver", null, null, null, null, null);
        var fromClause = (FromClause)builder.FromClause;
        var joinClause = (JoinClause)builder.JoinClause;
        Expression<Func<Sample, Sample, bool>> invalidPredicate = (owner, summary) =>
            owner.StringValue == summary.StringValue;
        Expression<Func<Sample, Sample, bool>> validPredicate = (owner, summary) =>
            owner.IntValue == summary.IntValue;
        var expected = CaptureQueryState(builder);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            joinClause.Join(fromClause, subquery, invalidPredicate));

        // Assert
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", exception.Message);
        AssertQueryState(expected, builder);

        joinClause.Join(fromClause, subquery, validPredicate);

        Assert.Equal(new object[] { 1, 2 }, builder.GetParams().Values.ToArray());
        Assert.Equal("Select [owner].[IntValue] \r\nFrom [Sample] As [owner] \r\nJoin (Select [IntValue] \r\nFrom [source] \r\nWhere [Id]=@_p_1) As [summary] On [owner].[IntValue]=[summary].[IntValue] \r\nWhere [owner].[IntValue]=@_p_0",
            builder.ToSql());
        Assert.Equal(new[] { "join_0|summary|Bing.Data.Sql.Tests.Samples.Sample|IntValue" },
            builder.GetTypedJoinSources().Select(DescribeSource));
    }

    /// <summary>
    /// 测试目的：类型化派生表 Join 的重复别名必须在渲染前拒绝，且失败后可使用其他别名完成连接。
    /// </summary>
    [Fact]
    public void TypedSubqueryJoin_WhenAliasDuplicatesRoot_ShouldKeepAllQueryStateAndAllowDifferentAlias()
    {
        // Arrange
        var builder = (TestSqlBuilder)new TestSqlBuilder()
            .Select("owner.IntValue")
            .From<Sample>("owner")
            .Where<Sample>(item => item.IntValue, 1);
        var child = new TestSqlBuilder()
            .Select("IntValue")
            .From("source")
            .Where("Id", 2);
        var duplicate = new SqlSubquery<Sample>(child, "owner", new[] { nameof(Sample.IntValue) },
            "test.sqlserver", null, null, null, null, null);
        var retry = new SqlSubquery<Sample>(child, "summary", new[] { nameof(Sample.IntValue) },
            "test.sqlserver", null, null, null, null, null);
        var fromClause = (FromClause)builder.FromClause;
        var joinClause = (JoinClause)builder.JoinClause;
        Expression<Func<Sample, Sample, bool>> predicate = (owner, derived) =>
            owner.IntValue == derived.IntValue;
        var expected = CaptureQueryState(builder);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            joinClause.Join(fromClause, duplicate, predicate));

        // Assert
        Assert.Equal("查询中已存在表别名 \"owner\"。", exception.Message);
        AssertQueryState(expected, builder);

        joinClause.Join(fromClause, retry, predicate);

        Assert.Equal(new object[] { 1, 2 }, builder.GetParams().Values.ToArray());
        Assert.Equal(new[] { "join_0|summary|Bing.Data.Sql.Tests.Samples.Sample|IntValue" },
            builder.GetTypedJoinSources().Select(DescribeSource));
    }

    /// <summary>
    /// 测试目的：实体 Join 的跨库校验失败必须发生在来源、别名、Select 和查询操作状态提交之前。
    /// </summary>
    [Fact]
    public void TypedEntityJoin_WhenCrossDatabaseValidationFails_ShouldKeepAllQueryStateUnchanged()
    {
        // Arrange
        var builder = (TestSqlBuilder)new TestSqlBuilder(
                crossDatabaseQueryValidator: new RejectingCrossDatabaseQueryValidator())
            .Select("owner.IntValue")
            .From<Sample>("owner")
            .Where<Sample>(item => item.IntValue, 1);
        var fromClause = (FromClause)builder.FromClause;
        var joinClause = (JoinClause)builder.JoinClause;
        Expression<Func<Sample, Sample2, bool>> predicate = (owner, candidate) =>
            owner.IntValue == candidate.IntValue;
        var expected = CaptureQueryState(builder);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            joinClause.Join<Sample2>(fromClause, predicate, "candidate"));

        // Assert
        Assert.Equal("测试跨库校验拒绝连接。", exception.Message);
        AssertQueryState(expected, builder);
    }

    /// <summary>
    /// 测试目的：实体映射解析失败时，类型化 Join 不得提交连接、别名、来源图或查询状态。
    /// </summary>
    [Fact]
    public void TypedEntityJoin_WhenMappingResolutionFails_ShouldKeepAllQueryStateUnchanged()
    {
        // Arrange
        var builder = new TestSqlBuilder(entityMappingResolver: new RejectingMappingResolver())
            .Select("owner.Id")
            .From("owner", "owner");
        var fromClause = (FromClause)builder.FromClause;
        var joinClause = (JoinClause)builder.JoinClause;
        Expression<Func<Sample, Sample2, bool>> predicate = (owner, candidate) => owner.IntValue == candidate.IntValue;
        var expected = CaptureQueryState(builder);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            joinClause.Join<Sample2>(fromClause, predicate, "candidate"));

        // Assert
        Assert.Equal("测试实体映射拒绝连接。", exception.Message);
        AssertQueryState(expected, builder);
        Assert.Empty(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 测试目的：结构化表引用校验失败时，类型化 Join 不得写入任何候选查询状态。
    /// </summary>
    [Fact]
    public void TypedEntityJoin_WhenTableReferenceValidationFails_ShouldKeepAllQueryStateUnchanged()
    {
        // Arrange
        var builder = new TestSqlBuilder(tableReferenceValidator: new RejectingTableReferenceValidator())
            .Select("owner.Id")
            .From("owner", "owner");
        var fromClause = (FromClause)builder.FromClause;
        var joinClause = (JoinClause)builder.JoinClause;
        Expression<Func<Sample, Sample2, bool>> predicate = (owner, candidate) => owner.IntValue == candidate.IntValue;
        var expected = CaptureQueryState(builder);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            joinClause.Join<Sample2>(fromClause, predicate, "candidate"));

        // Assert
        Assert.Equal("测试表引用校验拒绝连接。", exception.Message);
        AssertQueryState(expected, builder);
        Assert.Empty(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 测试目的：对象名格式化失败时，类型化 Join 不得留下别名、参数或部分来源图，并应允许后续重试。
    /// </summary>
    [Fact]
    public void TypedEntityJoin_WhenObjectNameFormattingFails_ShouldKeepAllQueryStateAndAllowRetry()
    {
        // Arrange
        var builder = new TestSqlBuilder(objectNameFormatter: new RejectingObjectNameFormatter())
            .Select("owner.Id")
            .From("owner", "owner");
        var fromClause = (FromClause)builder.FromClause;
        var joinClause = (JoinClause)builder.JoinClause;
        Expression<Func<Sample, Sample2, bool>> predicate = (owner, candidate) => owner.IntValue == candidate.IntValue;
        var expected = CaptureQueryState(builder);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            joinClause.Join<Sample2>(fromClause, predicate, "candidate"));

        // Assert
        Assert.Equal("测试对象名格式化拒绝连接。", exception.Message);
        AssertQueryState(expected, builder);
        Assert.Empty(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 创建已写入一个 Set 参数的 Update Builder。
    /// </summary>
    private static TestSqlBuilder CreateUpdateBuilder() => new TestSqlBuilder()
        .Update(new SqlTableReference { TableName = "outer" })
        .Set("Name", "existing");

    /// <summary>
    /// 捕获原子操作失败后必须保持不变的查询状态。
    /// </summary>
    private static QueryState CaptureQueryState(TestSqlBuilder builder)
    {
        var fromClause = (FromClause)builder.FromClause;
        return new QueryState(
            builder.ToSql(),
            builder.GetParams().OrderBy(item => item.Key).ToArray(),
            fromClause.Sources.Select(DescribeSource).ToArray(),
            builder.GetTypedJoinSources().Select(DescribeSource).ToArray(),
            builder.SelectClause.ToSql(),
            builder.OperationKind);
    }

    /// <summary>
    /// 断言失败后的查询状态与调用前快照完全一致。
    /// </summary>
    private static void AssertQueryState(QueryState expected, TestSqlBuilder builder)
    {
        var actual = CaptureQueryState(builder);
        Assert.Equal(expected.Sql, actual.Sql);
        Assert.Equal(expected.Parameters, actual.Parameters);
        Assert.Equal(expected.RootSources, actual.RootSources);
        Assert.Equal(expected.JoinSources, actual.JoinSources);
        Assert.Equal(expected.Select, actual.Select);
        Assert.Equal(expected.Operation, actual.Operation);
    }

    /// <summary>
    /// 描述表源的稳定身份、别名、类型和派生表投影白名单。
    /// </summary>
    private static string DescribeSource(TableSource source) =>
        $"{source.SourceId}|{source.Alias}|{source.EntityType?.FullName}|{string.Join(",", source.ProjectedMembers ?? Array.Empty<string>())}";

    /// <summary>
    /// 原子 Join 失败测试使用的跨库校验器。
    /// </summary>
    private sealed class RejectingCrossDatabaseQueryValidator : ISqlCrossDatabaseQueryValidator
    {
        /// <inheritdoc />
        public void Validate(DatabaseContext executionContext, SqlTableReference source, SqlTableReference target) =>
            throw new NotSupportedException("测试跨库校验拒绝连接。");

        /// <inheritdoc />
        public void ValidateTarget(DatabaseContext executionContext, SqlTableReference target) =>
            throw new NotSupportedException("测试跨库校验拒绝连接。");
    }

    /// <summary>
    /// 原子 Join 测试使用的实体映射解析器。
    /// </summary>
    private sealed class RejectingMappingResolver : IEntityMappingResolver
    {
        /// <inheritdoc />
        public EntityDescriptor GetDescriptor(Type entityType) =>
            throw new InvalidOperationException("测试实体映射拒绝连接。");

        /// <inheritdoc />
        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext) =>
            throw new InvalidOperationException("测试实体映射拒绝连接。");
    }

    /// <summary>
    /// 原子 Join 测试使用的表引用校验器。
    /// </summary>
    private sealed class RejectingTableReferenceValidator : ISqlTableReferenceValidator
    {
        /// <inheritdoc />
        public void Validate(SqlTableReference table, DatabaseType databaseType) =>
            throw new InvalidOperationException("测试表引用校验拒绝连接。");
    }

    /// <summary>
    /// 原子 Join 测试使用的对象名格式化器。
    /// </summary>
    private sealed class RejectingObjectNameFormatter : ISqlObjectNameFormatter
    {
        /// <inheritdoc />
        public string Format(SqlTableReference reference, IDialect dialect, DatabaseType? databaseType) =>
            throw new InvalidOperationException("测试对象名格式化拒绝连接。");
    }

    /// <summary>
    /// 查询状态快照。
    /// </summary>
    private sealed record QueryState(
        string Sql,
        IReadOnlyList<KeyValuePair<string, object>> Parameters,
        IReadOnlyList<string> RootSources,
        IReadOnlyList<string> JoinSources,
        string Select,
        SqlOperationKind Operation);

    /// <summary>
    /// 严格派生表的最小投影模型。
    /// </summary>
    private sealed class DerivedSummary
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 子查询租户过滤测试实体。
    /// </summary>
    private sealed class SubqueryTenantSample
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 租户标识。
        /// </summary>
        public string TenantId { get; set; }
    }

    /// <summary>
    /// 子查询租户过滤测试贡献者。
    /// </summary>
    private sealed class SubqueryTenantFilterContributor : ISqlTenantFilterContributor
    {
        /// <summary>
        /// 初始化一个 <see cref="SubqueryTenantFilterContributor"/> 类型的实例。
        /// </summary>
        /// <param name="tenantId">当前租户标识。</param>
        public SubqueryTenantFilterContributor(string tenantId) => TenantId = tenantId;

        /// <summary>
        /// 当前租户标识。
        /// </summary>
        private string TenantId { get; }

        /// <inheritdoc />
        public bool IsTenantEntity(Type entityType) => entityType == typeof(SubqueryTenantSample);

        /// <inheritdoc />
        public object GetTenantId(SqlTenantFilterContext context) => TenantId;
    }

    /// <summary>
    /// 从 Builder 冻结数据库上下文读取租户标识的测试贡献者。
    /// </summary>
    private sealed class ContextTenantFilterContributor : ISqlTenantFilterContributor
    {
        /// <inheritdoc />
        public bool IsTenantEntity(Type entityType) => entityType == typeof(SubqueryTenantSample);

        /// <inheritdoc />
        public object GetTenantId(SqlTenantFilterContext context) => context.DatabaseContext?.TenantId;
    }

    /// <summary>
    /// 仅在格式化指定派生表别名时失败的测试方言。
    /// </summary>
    private sealed class FailingSubqueryAliasDialect : DialectBase
    {
        /// <summary>
        /// 是否抛出别名格式化异常。
        /// </summary>
        public bool ShouldFail { get; set; } = true;

        /// <inheritdoc />
        public override string SafeName(string name)
        {
            if (ShouldFail && string.Equals(name, "summary", StringComparison.Ordinal))
                throw new InvalidOperationException("Subquery alias formatting failed.");
            return base.SafeName(name);
        }
    }
}