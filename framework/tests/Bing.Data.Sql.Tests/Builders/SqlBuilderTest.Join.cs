using System.Linq.Expressions;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Test.Shared;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - Join 子句
/// </summary>
public partial class SqlBuilderTest
{
    /// <summary>
    /// 内连接
    /// </summary>
    [Fact]
    public void Test_Join_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join [c] As [d]");

        //执行
        _builder.Select("a")
            .From("b")
            .Join("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 内连接 - 泛型
    /// </summary>
    [Fact]
    public void Test_Join_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join [d].[Sample] As [c]");

        //执行
        _builder.Select("a")
            .From("b")
            .Join<Sample>("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 添加Join子查询
    /// </summary>
    [Fact]
    public void Test_Join_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        var builder2 = _builder.New().From("Test2").Where("Name", "a");
        _builder.From("Test").Join(builder2, "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：独立子查询与外层查询生成相同参数名时，应重命名子查询参数并保留两侧参数值。
    /// </summary>
    [Fact]
    public void Join_WhenExternalSubqueryParameterConflicts_ShouldRenameSubqueryParameter()
    {
        // Arrange
        var subquery = new TestSqlBuilder().From("Test2").Where("Name", "child");
        var expected = new StringBuilder();
        expected.AppendLine("Select * ");
        expected.AppendLine("From [Test] ");
        expected.AppendLine("Join (Select * ");
        expected.AppendLine("From [Test2] ");
        expected.AppendLine("Where [Name]=@_p_1) As [t] ");
        expected.Append("Where [Age]=@_p_0");

        // Act
        _builder.From("Test").Where("Age", 1).Join(subquery, "t");

        // Assert
        Assert.Equal(expected.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal("child", _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：作为 On 条件的独立 Builder 必须合并参数，并在冲突时重命名避免错误绑定到外层值。
    /// </summary>
    [Fact]
    public void Join_WhenOnUsesExternalBuilderCondition_ShouldMergeParameters()
    {
        // Arrange
        const string expected = "Select * \r\nFrom [Parent] \r\nJoin [Child] On [TenantId]=@_p_1 \r\nWhere [TenantId]=@_p_0";
        var condition = new TestSqlBuilder().Where("TenantId", "child");

        // Act
        _builder.From("Parent").Where("TenantId", "parent").Join("Child").On(condition);

        // Assert
        Assert.Equal(expected, _builder.ToSql());
        Assert.Equal("parent", _builder.GetParam("@_p_0"));
        Assert.Equal("child", _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：子查询连续参数均与外层冲突时，应基于原始 SQL 一次性重命名，避免先替换的参数被后续替换再次改写。
    /// </summary>
    [Fact]
    public void Join_WhenChildHasSequentialConflictingParameters_ShouldRenameEachTokenOnce()
    {
        // Arrange
        var subquery = new TestSqlBuilder().From("Test2").Where("Name", "child-name").Where("Age", 18);
        const string expected = "Select * \r\nFrom [Test] \r\nJoin (Select * \r\nFrom [Test2] \r\nWhere [Name]=@_p_1 And [Age]=@_p_2) As [t] \r\nWhere [Id]=@_p_0";

        // Act
        _builder.From("Test").Where("Id", 1).Join(subquery, "t");

        // Assert
        Assert.Equal(expected, _builder.ToSql());
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal("child-name", _builder.GetParam("@_p_1"));
        Assert.Equal(18, _builder.GetParam("@_p_2"));
    }

    /// <summary>
    /// 测试目的：子查询参数冲突改名时，只应替换 SQL 代码中的参数标记，不得修改字符串、注释或方括号标识符。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryContainsQuotedOrCommentedParameterText_ShouldRenameOnlyParameterToken()
    {
        // Arrange
        var subquery = new TestSqlBuilder().From("Child")
            .AppendWhere("[Text]='@_p_0' And [@_p_0]=1 /* @_p_0 */ -- @_p_0\r\n And [Value]=@_p_0")
            .AddParam("_p_0", "child");
        var expected = new StringBuilder();
        expected.AppendLine("Select * ");
        expected.AppendLine("From [Parent] ");
        expected.AppendLine("Join (Select * ");
        expected.AppendLine("From [Child] ");
        expected.AppendLine("Where [Text]='@_p_0' And [@_p_0]=1 /* @_p_0 */ -- @_p_0");
        expected.AppendLine(" And [Value]=@_p_1) As [c] ");
        expected.Append("Where [Id]=@_p_0");

        // Act
        _builder.From("Parent").Where("Id", 1).Join(subquery, "c");

        // Assert
        Assert.Equal(expected.ToString(), _builder.ToSql());
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal("child", _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：结构化 From 和 Join 在同一查询范围内使用重复 alias 时应立即失败。
    /// </summary>
    [Fact]
    public void Join_WhenAliasDuplicatesFromAlias_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _builder.From("Orders", "o");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _builder.Join("OrderItems", "o"));

        // Assert
        Assert.Equal("查询中已存在表别名 \"o\"。", exception.Message);
    }

    /// <summary>
    /// 测试目的：类型化 Join 的重复 alias 失败时应保持 SQL、参数和类型化来源图不变。
    /// </summary>
    [Fact]
    public void TypedJoin_WhenAliasDuplicatesFromAlias_ShouldKeepStateUnchanged()
    {
        // Arrange
        _builder.From<Sample>("s");
        var fromClause = Assert.IsType<FromClause>(_builder.FromClause);
        var joinClause = Assert.IsType<JoinClause>(_builder.JoinClause);
        var sqlBefore = _builder.ToSql();
        var parametersBefore = _builder.GetParams().ToDictionary(item => item.Key, item => item.Value);
        var sourceCountBefore = joinClause.GetTypedSources().Count;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => joinClause.Join<Sample2>(fromClause,
            (Expression<Func<Sample, Sample2, bool>>)((left, right) => left.IntValue == right.IntValue), "s"));

        // Assert
        Assert.Equal("查询中已存在表别名 \"s\"。", exception.Message);
        Assert.Equal(sqlBefore, _builder.ToSql());
        Assert.Equal(parametersBefore, _builder.GetParams());
        Assert.Equal(sourceCountBefore, joinClause.GetTypedSources().Count);
    }

    /// <summary>
    /// 测试目的：类型化 Join 的空谓词失败时应保持 SQL、参数和类型化来源图不变。
    /// </summary>
    [Fact]
    public void TypedJoin_WhenPredicateIsNull_ShouldKeepStateUnchanged()
    {
        // Arrange
        _builder.From<Sample>("s");
        var fromClause = Assert.IsType<FromClause>(_builder.FromClause);
        var joinClause = Assert.IsType<JoinClause>(_builder.JoinClause);
        var sqlBefore = _builder.ToSql();
        var parametersBefore = _builder.GetParams().ToDictionary(item => item.Key, item => item.Value);
        var sourceCountBefore = joinClause.GetTypedSources().Count;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => joinClause.Join<Sample2>(fromClause, null, "j"));

        // Assert
        Assert.Equal("predicate", exception.ParamName);
        Assert.Equal(sqlBefore, _builder.ToSql());
        Assert.Equal(parametersBefore, _builder.GetParams());
        Assert.Equal(sourceCountBefore, joinClause.GetTypedSources().Count);
    }

    /// <summary>
    /// 测试目的：替换根 From 后，已移除根表的 alias 应可由新的 Join 合法复用。
    /// </summary>
    [Fact]
    public void Join_WhenRootFromWasReplaced_ShouldAllowReusingReleasedRootAlias()
    {
        // Arrange
        _builder.From("Orders", "o");

        // Act
        _builder.From("Customers", "c").Join("OrderItems", "o");

        // Assert
        Assert.Equal("Select * \r\nFrom [Customers] As [c] \r\nJoin [OrderItems] As [o]", _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：原始 From 替换结构化根来源后，被移除根表的 alias 应可由新的 Join 合法复用。
    /// </summary>
    [Fact]
    public void Join_WhenRawFromReplacesRoot_ShouldAllowReusingReleasedRootAlias()
    {
        // Arrange
        _builder.From("Orders", "o");

        // Act
        _builder.AppendFrom("ArchivedOrders archive").Join("OrderItems", "o");

        // Assert
        Assert.Equal("Select * \r\nFrom ArchivedOrders archive \r\nJoin [OrderItems] As [o]", _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：普通子查询替换结构化根来源后，被移除根表的 alias 应可由新的 Join 合法复用。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryFromReplacesRoot_ShouldAllowReusingReleasedRootAlias()
    {
        // Arrange
        var subquery = new TestSqlBuilder().Select("*").From("ArchivedOrders");
        _builder.From("Orders", "o");

        // Act
        _builder.From(subquery, "archive").Join("OrderItems", "o");

        // Assert
        Assert.Equal("Select * \r\nFrom (Select * \r\nFrom [ArchivedOrders]) As [archive] \r\nJoin [OrderItems] As [o]",
            _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：清除根 From 后，已移除根表的 alias 应可由新的 Join 合法复用。
    /// </summary>
    [Fact]
    public void Join_WhenRootFromWasCleared_ShouldAllowReusingReleasedRootAlias()
    {
        // Arrange
        _builder.From("Orders", "o");

        // Act
        _builder.ClearFrom().From("Customers", "c").Join("OrderItems", "o");

        // Assert
        Assert.Equal("Select * \r\nFrom [Customers] As [c] \r\nJoin [OrderItems] As [o]", _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：清除结构化 Join 后，已移除连接的 alias 应可由新 Join 合法复用。
    /// </summary>
    [Fact]
    public void Join_WhenPreviousJoinWasCleared_ShouldAllowReusingReleasedJoinAlias()
    {
        // Arrange
        _builder.From("Orders", "o").Join("OrderItems", "i");

        // Act
        _builder.ClearJoin().Join("Invoices", "i");

        // Assert
        Assert.Equal("Select * \r\nFrom [Orders] As [o] \r\nJoin [Invoices] As [i]", _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：清除普通子查询 Join 后，已移除连接的 alias 应可由新 Join 合法复用。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryJoinWasCleared_ShouldAllowReusingReleasedJoinAlias()
    {
        // Arrange
        var subquery = new TestSqlBuilder().Select("*").From("OrderItems");
        _builder.From("Orders", "o").Join(subquery, "items");

        // Act
        _builder.ClearJoin().Join("Invoices", "items");

        // Assert
        Assert.Equal("Select * \r\nFrom [Orders] As [o] \r\nJoin [Invoices] As [items]", _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：同一实体类型自连接时，类型化 On 表达式应分别使用来源表和最新 Join 表的别名。
    /// </summary>
    [Fact]
    public void Join_WhenSelfJoinTypedOnConfigured_ShouldRenderDistinctAliases()
    {
        // Arrange
        _builder.Select("s.Email")
            .From<Sample>("s")
            .Join<Sample>("p");

        // Act
        _builder.On<Sample, Sample>((left, right) => left.IntValue == right.IntValue);

        // Assert
        Assert.Equal("Select [s].[Email] \r\nFrom [Sample] As [s] \r\nJoin [Sample] As [p] On [s].[IntValue]=[p].[IntValue]", _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：重复实体类型化 Join 提交后，既有实体投影必须冻结为根来源别名，不能被新 Join 改写。
    /// </summary>
    [Fact]
    public void Join_WhenSelfJoinTypedProjectionAlreadyExists_ShouldFreezeRootProjectionAlias()
    {
        // Arrange
        _builder.Select<Sample>(sample => new object[] { sample.Email })
            .From<Sample>("s");
        var fromClause = Assert.IsType<FromClause>(_builder.FromClause);
        var joinClause = Assert.IsType<JoinClause>(_builder.JoinClause);

        // Act
        joinClause.Join<Sample>(fromClause,
            (Expression<Func<Sample, Sample, bool>>)((left, right) => left.IntValue == right.IntValue), "p");

        // Assert
        Assert.Equal(
            "Select [s].[Email] \r\nFrom [Sample] As [s] \r\nJoin [Sample] As [p] On [s].[IntValue]=[p].[IntValue]",
            _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：投影别名冻结失败时，类型化 Join 不得提交参数、Operation、Join 来源或别名，并且失败后可重试。
    /// </summary>
    [Fact]
    public void TypedJoin_WhenProjectionAliasFreezeFails_ShouldKeepAllStateUnchangedAndAllowRetry()
    {
        // Arrange
        var builder = new FreezeFailingSqlBuilder();
        builder.Select<Sample>(sample => new object[] { sample.Email }).From<Sample>("s");
        var fromClause = Assert.IsType<FromClause>(builder.FromClause);
        var joinClause = Assert.IsType<FreezeFailingJoinClause>(builder.JoinClause);
        var sqlBefore = builder.ToSql();
        var parametersBefore = builder.GetParams().ToDictionary(item => item.Key, item => item.Value);
        var operationBefore = builder.OperationKind;
        var sourceCountBefore = joinClause.GetTypedSources().Count;
        joinClause.FailFreeze = true;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => joinClause.Join<Sample>(fromClause,
            (Expression<Func<Sample, Sample, bool>>)((left, right) => left.IntValue == right.IntValue), "p"));

        // Assert
        Assert.Equal("测试投影别名冻结失败。", exception.Message);
        Assert.Equal(sqlBefore, builder.ToSql());
        Assert.Equal(parametersBefore, builder.GetParams());
        Assert.Equal(operationBefore, builder.OperationKind);
        Assert.Equal(sourceCountBefore, joinClause.GetTypedSources().Count);

        // Retry
        joinClause.FailFreeze = false;
        joinClause.Join<Sample>(fromClause,
            (Expression<Func<Sample, Sample, bool>>)((left, right) => left.IntValue == right.IntValue), "p");
        Assert.Contains("Join [Sample] As [p]", builder.ToSql(), StringComparison.Ordinal);
        Assert.Single(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 测试目的：别名注册提交完成后抛异常时，必须恢复真实投影、别名、参数和连接图，并允许重试。
    /// </summary>
    [Fact]
    public void TypedJoin_WhenAliasRegisterCommitFails_ShouldKeepAllStateUnchangedAndAllowRetry()
    {
        // Arrange
        var builder = new FreezeFailingSqlBuilder();
        builder.Select<Sample>(sample => new object[] { sample.Email }).From<Sample>("s").Where<Sample>(
            sample => sample.IntValue, 1);
        var fromClause = Assert.IsType<FromClause>(builder.FromClause);
        var joinClause = Assert.IsType<FreezeFailingJoinClause>(builder.JoinClause);
        var sqlBefore = builder.ToSql();
        var parametersBefore = builder.GetParams().OrderBy(item => item.Key).ToArray();
        var metadataBefore = CaptureParameterMetadata(builder);
        var aliasesBefore = builder.AliasData.OrderBy(item => item.Key.FullName).ToArray();
        var operationBefore = builder.OperationKind;
        var sourceCountBefore = joinClause.GetTypedSources().Count;
        joinClause.FailAliasCommit = true;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => joinClause.Join<Sample>(fromClause,
            (Expression<Func<Sample, Sample, bool>>)((left, right) => left.IntValue == 2), "p"));

        // Assert
        Assert.Equal("测试别名提交失败。", exception.Message);
        Assert.Equal(sqlBefore, builder.ToSql());
        Assert.Equal(parametersBefore, builder.GetParams().OrderBy(item => item.Key));
        Assert.Equal(metadataBefore, CaptureParameterMetadata(builder));
        Assert.Equal(aliasesBefore, builder.AliasData.OrderBy(item => item.Key.FullName));
        Assert.Equal(operationBefore, builder.OperationKind);
        Assert.Equal(sourceCountBefore, joinClause.GetTypedSources().Count);

        // Retry
        joinClause.FailAliasCommit = false;
        joinClause.Join<Sample>(fromClause,
            (Expression<Func<Sample, Sample, bool>>)((left, right) => left.IntValue == 2), "p");
        Assert.Equal("Select [s].[Email] \r\nFrom [Sample] As [s] \r\nJoin [Sample] As [p] On [s].[IntValue]=@_p_1 \r\nWhere [s].[IntValue]=@_p_0",
            builder.ToSql());
        Assert.Equal(2, builder.GetParams().Count);
        Assert.Single(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 测试目的：最终连接项提交完成后抛异常时，必须移除已追加项并恢复全部可变状态，随后可重试。
    /// </summary>
    [Fact]
    public void TypedJoin_WhenFinalJoinCommitFails_ShouldKeepAllStateUnchangedAndAllowRetry()
    {
        // Arrange
        var builder = new FreezeFailingSqlBuilder();
        builder.Select<Sample>(sample => new object[] { sample.Email }).From<Sample>("s").Where<Sample>(
            sample => sample.IntValue, 1);
        var fromClause = Assert.IsType<FromClause>(builder.FromClause);
        var joinClause = Assert.IsType<FreezeFailingJoinClause>(builder.JoinClause);
        var sqlBefore = builder.ToSql();
        var parametersBefore = builder.GetParams().OrderBy(item => item.Key).ToArray();
        var metadataBefore = CaptureParameterMetadata(builder);
        var aliasesBefore = builder.AliasData.OrderBy(item => item.Key.FullName).ToArray();
        var operationBefore = builder.OperationKind;
        var sourceCountBefore = joinClause.GetTypedSources().Count;
        joinClause.FailJoinCommit = true;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => joinClause.Join<Sample>(fromClause,
            (Expression<Func<Sample, Sample, bool>>)((left, right) => left.IntValue == 2), "p"));

        // Assert
        Assert.Equal("测试连接项提交失败。", exception.Message);
        Assert.Equal(sqlBefore, builder.ToSql());
        Assert.Equal(parametersBefore, builder.GetParams().OrderBy(item => item.Key));
        Assert.Equal(metadataBefore, CaptureParameterMetadata(builder));
        Assert.Equal(aliasesBefore, builder.AliasData.OrderBy(item => item.Key.FullName));
        Assert.Equal(operationBefore, builder.OperationKind);
        Assert.Equal(sourceCountBefore, joinClause.GetTypedSources().Count);

        // Retry
        joinClause.FailJoinCommit = false;
        joinClause.Join<Sample>(fromClause,
            (Expression<Func<Sample, Sample, bool>>)((left, right) => left.IntValue == 2), "p");
        Assert.Equal("Select [s].[Email] \r\nFrom [Sample] As [s] \r\nJoin [Sample] As [p] On [s].[IntValue]=@_p_1 \r\nWhere [s].[IntValue]=@_p_0",
            builder.ToSql());
        Assert.Equal(2, builder.GetParams().Count);
        Assert.Single(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 测试目的：自定义参数管理器的候选 Add 在第二个参数失败时，不得污染真实 Builder，且关闭故障后可重试。
    /// </summary>
    [Fact]
    public void TypedJoin_WhenCustomParameterProbeFails_ShouldKeepStateUnchangedAndAllowRetry()
    {
        // Arrange
        var parameterManager = new ThrowingParameterManager(TestDialect.Instance, 1);
        var builder = new InspectableParameterSqlBuilder(parameterManager);
        builder.Select<Sample>(sample => new object[] { sample.Email }).From<Sample>("s").Where<Sample>(
            sample => sample.IntValue, 7);
        var fromClause = Assert.IsType<FromClause>(builder.FromClause);
        var joinClause = Assert.IsType<JoinClause>(builder.JoinClause);
        var sqlBefore = builder.ToSql();
        var selectBefore = builder.SelectClause.ToSql();
        var parametersBefore = builder.GetParams().OrderBy(item => item.Key).ToArray();
        var aliasesBefore = builder.AliasData.OrderBy(item => item.Key.FullName).ToArray();
        var operationBefore = builder.OperationKind;
        var sourceGraphBefore = CaptureSourceGraph(builder);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => joinClause.Join<Sample2>(fromClause,
            (Expression<Func<Sample, Sample2, bool>>)((left, right) => left.IntValue == 1 &&
                right.IntValue == 2), "p"));

        // Assert
        Assert.Equal("测试参数管理器在第二个参数写入时失败。", exception.Message);
        Assert.Equal(sqlBefore, builder.ToSql());
        Assert.Equal(selectBefore, builder.SelectClause.ToSql());
        Assert.Equal(parametersBefore, builder.GetParams().OrderBy(item => item.Key));
        Assert.Equal(aliasesBefore, builder.AliasData.OrderBy(item => item.Key.FullName));
        Assert.Equal(operationBefore, builder.OperationKind);
        Assert.Equal(sourceGraphBefore, CaptureSourceGraph(builder));
        Assert.Single(parameterManager.GetParams());
        Assert.Equal(7, parameterManager.GetValue("@_p_0"));

        // Retry
        parameterManager.FailAfterAdds = null;
        joinClause.Join<Sample2>(fromClause,
            (Expression<Func<Sample, Sample2, bool>>)((left, right) => left.IntValue == 1 &&
                right.IntValue == 2), "p");
        Assert.Equal("Select [s].[Email] \r\nFrom [Sample] As [s] \r\nJoin [Sample2] As [p] On [s].[IntValue]=@_p_1 And [p].[IntValue]=@_p_2 \r\nWhere [s].[IntValue]=@_p_0",
            builder.ToSql());
        Assert.Equal(new object[] { 7, 1, 2 }, builder.GetParams().Values.ToArray());
        Assert.Single(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 测试目的：增强参数管理器的候选 Add 失败时，元数据状态也不得污染真实 Builder，且可按原序号重试。
    /// </summary>
    [Fact]
    public void TypedJoin_WhenCustomAdvancedParameterProbeFails_ShouldKeepMetadataStateUnchangedAndAllowRetry()
    {
        // Arrange
        var parameterManager = new ThrowingAdvancedParameterManager(TestDialect.Instance, 1);
        var builder = new InspectableParameterSqlBuilder(parameterManager);
        builder.Select<Sample>(sample => new object[] { sample.Email }).From<Sample>("s").Where<Sample>(
            sample => sample.IntValue, 7);
        var fromClause = Assert.IsType<FromClause>(builder.FromClause);
        var joinClause = Assert.IsType<JoinClause>(builder.JoinClause);
        var sqlBefore = builder.ToSql();
        var selectBefore = builder.SelectClause.ToSql();
        var parametersBefore = builder.GetParams().OrderBy(item => item.Key).ToArray();
        var aliasesBefore = builder.AliasData.OrderBy(item => item.Key.FullName).ToArray();
        var metadataBefore = CaptureParameterMetadata(parameterManager);
        var sourceGraphBefore = CaptureSourceGraph(builder);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => joinClause.Join<Sample2>(fromClause,
            (Expression<Func<Sample, Sample2, bool>>)((left, right) => left.IntValue == 1 &&
                right.IntValue == 2), "p"));

        // Assert
        Assert.Equal("测试增强参数管理器在第二个参数写入时失败。", exception.Message);
        Assert.Equal(sqlBefore, builder.ToSql());
        Assert.Equal(selectBefore, builder.SelectClause.ToSql());
        Assert.Equal(parametersBefore, builder.GetParams().OrderBy(item => item.Key));
        Assert.Equal(aliasesBefore, builder.AliasData.OrderBy(item => item.Key.FullName));
        Assert.Equal(metadataBefore, CaptureParameterMetadata(parameterManager));
        Assert.Equal(sourceGraphBefore, CaptureSourceGraph(builder));
        Assert.Single(parameterManager.GetParams());
        Assert.Equal(7, parameterManager.GetValue("@_p_0"));

        // Retry
        parameterManager.FailAfterAdds = null;
        joinClause.Join<Sample2>(fromClause,
            (Expression<Func<Sample, Sample2, bool>>)((left, right) => left.IntValue == 1 &&
                right.IntValue == 2), "p");
        Assert.Equal("Select [s].[Email] \r\nFrom [Sample] As [s] \r\nJoin [Sample2] As [p] On [s].[IntValue]=@_p_1 And [p].[IntValue]=@_p_2 \r\nWhere [s].[IntValue]=@_p_0",
            builder.ToSql());
        Assert.Equal(new object[] { 7, 1, 2 }, builder.GetParams().Values.ToArray());
        Assert.Equal(3, CaptureParameterMetadata(builder).Length);
        Assert.Single(parameterManager.GetSqlParams());
        Assert.Single(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 测试目的：类型化派生表 Join 的子查询参数渲染失败时，不得写入普通第三方参数管理器，且关闭故障后可按原编号重试。
    /// </summary>
    [Fact]
    public void TypedSubqueryJoin_WhenCustomParameterRenderFails_ShouldKeepStateUnchangedAndAllowRetry()
    {
        // Arrange
        var parameterManager = new ThrowingParameterManager(TestDialect.Instance, 1);
        var builder = new InspectableParameterSqlBuilder(parameterManager);
        builder.Select("owner.IntValue").From<Sample>("owner").Where<Sample>(sample => sample.IntValue, 7);
        var child = new TestSqlBuilder()
            .Select("IntValue")
            .From("source")
            .Where("Id", 2)
            .Where("Code", "child");
        var subquery = new SqlSubquery<Sample>(child, "summary", new[] { nameof(Sample.IntValue) },
            "test.sqlserver", null, null, null, null, null);
        var fromClause = Assert.IsType<FromClause>(builder.FromClause);
        var joinClause = Assert.IsType<JoinClause>(builder.JoinClause);
        Expression<Func<Sample, Sample, bool>> predicate = (owner, summary) =>
            owner.IntValue == 3 && owner.IntValue == summary.IntValue;
        var sqlBefore = builder.ToSql();
        var parametersBefore = builder.GetParams().OrderBy(item => item.Key).ToArray();
        var aliasesBefore = builder.AliasData.OrderBy(item => item.Key.FullName).ToArray();
        var operationBefore = builder.OperationKind;
        var sourceGraphBefore = CaptureSourceGraph(builder);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            joinClause.Join(fromClause, subquery, predicate));

        // Assert
        Assert.Equal("测试参数管理器在第二个参数写入时失败。", exception.Message);
        Assert.Equal(sqlBefore, builder.ToSql());
        Assert.Equal(parametersBefore, builder.GetParams().OrderBy(item => item.Key));
        Assert.Equal(aliasesBefore, builder.AliasData.OrderBy(item => item.Key.FullName));
        Assert.Equal(operationBefore, builder.OperationKind);
        Assert.Equal(sourceGraphBefore, CaptureSourceGraph(builder));
        Assert.Empty(joinClause.GetTypedSources());
        Assert.Single(parameterManager.GetParams());
        Assert.Equal(7, parameterManager.GetValue("@_p_0"));

        // Retry
        parameterManager.FailAfterAdds = null;
        joinClause.Join(fromClause, subquery, predicate);

        // Assert
        Assert.Equal(new object[] { 7, 2, "child", 3 }, builder.GetParams().Values.ToArray());
        Assert.Equal(
            "Select [owner].[IntValue] \r\nFrom [Sample] As [owner] \r\nJoin (Select [IntValue] \r\nFrom [source] \r\nWhere [Id]=@_p_1 And [Code]=@_p_2) As [summary] On [owner].[IntValue]=@_p_3 And [owner].[IntValue]=[summary].[IntValue] \r\nWhere [owner].[IntValue]=@_p_0",
            builder.ToSql());
        Assert.Single(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 测试目的：类型化派生表 Join 的子查询参数渲染失败时，增强第三方参数的元数据不得污染，且可按原编号重试。
    /// </summary>
    [Fact]
    public void TypedSubqueryJoin_WhenCustomAdvancedParameterRenderFails_ShouldKeepMetadataStateUnchangedAndAllowRetry()
    {
        // Arrange
        var parameterManager = new ThrowingAdvancedParameterManager(TestDialect.Instance, 1);
        var builder = new InspectableParameterSqlBuilder(parameterManager);
        builder.Select("owner.IntValue").From<Sample>("owner").Where<Sample>(sample => sample.IntValue, 7);
        var child = new TestSqlBuilder()
            .Select("IntValue")
            .From("source")
            .Where("Id", 2)
            .Where("Code", "child");
        var subquery = new SqlSubquery<Sample>(child, "summary", new[] { nameof(Sample.IntValue) },
            "test.sqlserver", null, null, null, null, null);
        var fromClause = Assert.IsType<FromClause>(builder.FromClause);
        var joinClause = Assert.IsType<JoinClause>(builder.JoinClause);
        Expression<Func<Sample, Sample, bool>> predicate = (owner, summary) =>
            owner.IntValue == 3 && owner.IntValue == summary.IntValue;
        var sqlBefore = builder.ToSql();
        var parametersBefore = builder.GetParams().OrderBy(item => item.Key).ToArray();
        var aliasesBefore = builder.AliasData.OrderBy(item => item.Key.FullName).ToArray();
        var operationBefore = builder.OperationKind;
        var sourceGraphBefore = CaptureSourceGraph(builder);
        var metadataBefore = CaptureParameterMetadata(builder);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            joinClause.Join(fromClause, subquery, predicate));

        // Assert
        Assert.Equal("测试增强参数管理器在第二个参数写入时失败。", exception.Message);
        Assert.Equal(sqlBefore, builder.ToSql());
        Assert.Equal(parametersBefore, builder.GetParams().OrderBy(item => item.Key));
        Assert.Equal(metadataBefore, CaptureParameterMetadata(builder));
        Assert.Equal(aliasesBefore, builder.AliasData.OrderBy(item => item.Key.FullName));
        Assert.Equal(operationBefore, builder.OperationKind);
        Assert.Equal(sourceGraphBefore, CaptureSourceGraph(builder));
        Assert.Empty(joinClause.GetTypedSources());
        Assert.Single(parameterManager.GetParams());
        Assert.Equal(7, parameterManager.GetValue("@_p_0"));

        // Retry
        parameterManager.FailAfterAdds = null;
        joinClause.Join(fromClause, subquery, predicate);

        // Assert
        Assert.Equal(new object[] { 7, 2, "child", 3 }, builder.GetParams().Values.ToArray());
        Assert.Equal(
            "Select [owner].[IntValue] \r\nFrom [Sample] As [owner] \r\nJoin (Select [IntValue] \r\nFrom [source] \r\nWhere [Id]=@_p_1 And [Code]=@_p_2) As [summary] On [owner].[IntValue]=@_p_3 And [owner].[IntValue]=[summary].[IntValue] \r\nWhere [owner].[IntValue]=@_p_0",
            builder.ToSql());
        Assert.Equal(4, CaptureParameterMetadata(builder).Length);
        Assert.Single(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 测试目的：自定义参数管理器在候选状态提交后失败时，必须恢复旧参数管理器引用并允许使用原序号重试。
    /// </summary>
    [Fact]
    public void TypedJoin_WhenCustomParameterCommitFails_ShouldRestoreStateAndAllowRetry()
    {
        // Arrange
        var parameterManager = new ThrowingParameterManager(TestDialect.Instance);
        var builder = new CommitFailingSqlBuilder(parameterManager);
        builder.Select<Sample>(sample => new object[] { sample.Email }).From<Sample>("s");
        var fromClause = Assert.IsType<FromClause>(builder.FromClause);
        var joinClause = Assert.IsType<CommitFailingJoinClause>(builder.JoinClause);
        var sqlBefore = builder.ToSql();
        var parametersBefore = builder.GetParams().OrderBy(item => item.Key).ToArray();
        var operationBefore = builder.OperationKind;
        var sourceCountBefore = joinClause.GetTypedSources().Count;
        joinClause.FailParameterCommit = true;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => joinClause.Join<Sample2>(fromClause,
            (Expression<Func<Sample, Sample2, bool>>)((left, right) => left.IntValue == 1 &&
                right.IntValue == 2), "p"));

        // Assert
        Assert.Equal("测试参数状态提交失败。", exception.Message);
        Assert.Equal(sqlBefore, builder.ToSql());
        Assert.Equal(parametersBefore, builder.GetParams().OrderBy(item => item.Key));
        Assert.Equal(operationBefore, builder.OperationKind);
        Assert.Equal(sourceCountBefore, joinClause.GetTypedSources().Count);
        Assert.Empty(parameterManager.GetParams());

        // Retry
        joinClause.FailParameterCommit = false;
        joinClause.Join<Sample2>(fromClause,
            (Expression<Func<Sample, Sample2, bool>>)((left, right) => left.IntValue == 1 &&
                right.IntValue == 2), "p");
        Assert.Equal("Select [s].[Email] \r\nFrom [Sample] As [s] \r\nJoin [Sample2] As [p] On [s].[IntValue]=@_p_0 And [p].[IntValue]=@_p_1",
            builder.ToSql());
        Assert.Equal(new object[] { 1, 2 }, builder.GetParams().Values.ToArray());
        Assert.Single(joinClause.GetTypedSources());
    }

    /// <summary>
    /// 测试目的：Cross Join 不允许通过任意 On 入口附加连接条件。
    /// </summary>
    [Fact]
    public void CrossJoin_WhenOnConfigured_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _builder.Select("s.Id").From("Samples", "s").CrossJoin("Reviews", "r");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _builder.AppendOn("s.Id=r.SampleId"));

        // Assert
        Assert.Equal("Cross Join 不支持 On 条件。", exception.Message);
    }

    private sealed class FreezeFailingSqlBuilder : TestSqlBuilder
    {
        public FreezeFailingSqlBuilder()
        {
        }

        protected override IJoinClause CreateJoinClause() => new FreezeFailingJoinClause(CreateClauseContext());

        public IReadOnlyDictionary<Type, string> AliasData => AliasRegister.Data;
    }

    private sealed class InspectableParameterSqlBuilder : TestSqlBuilder
    {
        public InspectableParameterSqlBuilder(IParameterManager parameterManager)
            : base(parameterManager: parameterManager)
        {
        }

        public IReadOnlyDictionary<Type, string> AliasData => AliasRegister.Data;
    }

    private sealed class FreezeFailingJoinClause : JoinClause
    {
        public FreezeFailingJoinClause(SqlClauseContext context) : base(context)
        {
        }

        public bool FailFreeze { get; set; }

        public bool FailAliasCommit { get; set; }

        public bool FailJoinCommit { get; set; }

        internal override void FreezeExistingProjectionAlias(Type entityType, SelectClause selectClause,
            IEntityAliasRegister register)
        {
            if (FailFreeze)
                throw new InvalidOperationException("测试投影别名冻结失败。");
            base.FreezeExistingProjectionAlias(entityType, selectClause, register);
        }

        internal override void CommitAliasRegister(IEntityAliasRegister aliasRegister)
        {
            base.CommitAliasRegister(aliasRegister);
            if (FailAliasCommit)
                throw new InvalidOperationException("测试别名提交失败。");
        }

        internal override void CommitJoinItem(JoinItem item)
        {
            base.CommitJoinItem(item);
            if (FailJoinCommit)
                throw new InvalidOperationException("测试连接项提交失败。");
        }
    }

    private sealed class CommitFailingSqlBuilder : TestSqlBuilder
    {
        public CommitFailingSqlBuilder(IParameterManager parameterManager) : base(parameterManager: parameterManager)
        {
        }

        protected override IJoinClause CreateJoinClause() => new CommitFailingJoinClause(CreateClauseContext());
    }

    private sealed class CommitFailingJoinClause : JoinClause
    {
        public CommitFailingJoinClause(SqlClauseContext context) : base(context)
        {
        }

        public bool FailParameterCommit { get; set; }

        internal override void CommitParameterManager(IParameterManager parameterManager, FromClause fromClause)
        {
            base.CommitParameterManager(parameterManager, fromClause);
            if (FailParameterCommit)
                throw new InvalidOperationException("测试参数状态提交失败。");
        }
    }

    private sealed class ThrowingParameterManager : IParameterManager
    {
        private readonly ParameterManager _inner;
        private int _addCount;

        public ThrowingParameterManager(IDialect dialect, int? failAfterAdds = null)
            : this(new ParameterManager(dialect), failAfterAdds)
        {
        }

        private ThrowingParameterManager(ParameterManager inner, int? failAfterAdds)
        {
            _inner = inner;
            FailAfterAdds = failAfterAdds;
        }

        public int? FailAfterAdds { get; set; }

        public string GenerateName() => _inner.GenerateName();

        public string NormalizeName(string name) => _inner.NormalizeName(name);

        public int Count => _inner.Count;

        public void Add(string name, object value, Operator? @operator = null)
        {
            if (FailAfterAdds.HasValue && _addCount++ >= FailAfterAdds.Value)
                throw new InvalidOperationException("测试参数管理器在第二个参数写入时失败。");
            _inner.Add(name, value, @operator);
        }

        public IReadOnlyDictionary<string, object> GetParams() => _inner.GetParams();

        public bool Contains(string name) => _inner.Contains(name);

        public object GetValue(string name) => _inner.GetValue(name);

        public IParameterManager Clone() => new ThrowingParameterManager(
            (ParameterManager)_inner.Clone(), FailAfterAdds);

        public void Clear()
        {
            _addCount = 0;
            _inner.Clear();
        }

        public IParameterManager CreateEmpty() => new ThrowingParameterManager(
            (ParameterManager)_inner.CreateEmpty(), FailAfterAdds);
    }

    private sealed class ThrowingAdvancedParameterManager : IAdvancedParameterManager
    {
        private readonly ParameterManager _inner;
        private int _addCount;

        public ThrowingAdvancedParameterManager(IDialect dialect, int? failAfterAdds = null)
            : this(new ParameterManager(dialect), failAfterAdds)
        {
        }

        private ThrowingAdvancedParameterManager(ParameterManager inner, int? failAfterAdds)
        {
            _inner = inner;
            FailAfterAdds = failAfterAdds;
        }

        public int? FailAfterAdds { get; set; }

        public string GenerateName() => _inner.GenerateName();

        public string NormalizeName(string name) => _inner.NormalizeName(name);

        public int Count => _inner.Count;

        public void Add(string name, object value, Operator? @operator = null)
        {
            if (FailAfterAdds.HasValue && _addCount++ >= FailAfterAdds.Value)
                throw new InvalidOperationException("测试增强参数管理器在第二个参数写入时失败。");
            _inner.Add(name, value, @operator);
        }

        public void Add(SqlParam parameter)
        {
            if (FailAfterAdds.HasValue && _addCount++ >= FailAfterAdds.Value)
                throw new InvalidOperationException("测试增强参数管理器在第二个参数写入时失败。");
            _inner.Add(parameter);
        }

        public IReadOnlyDictionary<string, object> GetParams() => _inner.GetParams();

        public IReadOnlyDictionary<string, SqlParam> GetSqlParams() => _inner.GetSqlParams();

        public IReadOnlyDictionary<string, object> ExportValues() => _inner.ExportValues();

        public bool Contains(string name) => _inner.Contains(name);

        public object GetValue(string name) => _inner.GetValue(name);

        public IParameterManager Clone() => new ThrowingAdvancedParameterManager(
            (ParameterManager)_inner.Clone(), FailAfterAdds);

        public void Clear()
        {
            _addCount = 0;
            _inner.Clear();
        }

        public IParameterManager CreateEmpty() => new ThrowingAdvancedParameterManager(
            (ParameterManager)_inner.CreateEmpty(), FailAfterAdds);
    }

    /// <summary>
    /// 获取参数及其关键元数据的稳定快照。
    /// </summary>
    private static string[] CaptureParameterMetadata(TestSqlBuilder builder)
    {
        var manager = Assert.IsAssignableFrom<IAdvancedParameterManager>(builder.ParameterManager);
        return CaptureParameterMetadata(manager);
    }

    /// <summary>
    /// 获取增强参数管理器的稳定元数据快照。
    /// </summary>
    private static string[] CaptureParameterMetadata(IAdvancedParameterManager manager) =>
        manager.GetSqlParams().OrderBy(item => item.Key).Select(item =>
            $"{item.Key}|{item.Value.Value}|{item.Value.OriginalValue}|{item.Value.DbType}|{item.Value.Direction}|" +
            $"{item.Value.EntityType?.FullName}|{item.Value.PropertyName}|{item.Value.ColumnName}|{item.Value.Source}|" +
            $"{item.Value.MetadataLevel}|{item.Value.StorageKind}|{item.Value.ConverterKind}|{item.Value.CustomConverterName}").ToArray();

    /// <summary>
    /// 获取类型化连接来源图的稳定快照。
    /// </summary>
    private static string[] CaptureSourceGraph(TestSqlBuilder builder) => builder.GetTypedJoinSources()
        .Select(source => $"{source.SourceId}|{source.EntityType?.FullName}|{source.Alias}")
        .ToArray();

    /// <summary>
    /// 测试目的：Cross Join 使用含常量的 Lambda On 条件时，必须在解析表达式和创建参数前拒绝，保持 Builder 状态不变。
    /// </summary>
    [Fact]
    public void CrossJoin_WhenLambdaOnContainsConstant_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        _builder.From<Sample>("s").CrossJoin<Sample2>("r");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _builder.On<Sample, Sample2>((left, right) => left.IntValue == 1));

        // Assert
        Assert.Equal("Cross Join 不支持 On 条件。", exception.Message);
        Assert.Empty(_builder.GetParams());
        Assert.Equal("Select * \r\nFrom [Sample] As [s] \r\nCross Join [Sample2] As [r]", _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：Append 原始 SQL 不应参与别名冲突校验。
    /// </summary>
    [Fact]
    public void AppendJoin_WhenSqlContainsAlias_ShouldNotRegisterAlias()
    {
        const string expectedSql = "Select * \r\nFrom (Select 1) As source \r\nJoin (Select 2) As source";
        _builder.AppendFrom("(Select 1) As source");
        _builder.AppendJoin("(Select 2) As source");

        SqlAssert.Equal(expectedSql, _builder.ToSql(), _builder.Provider.Key);
    }

    /// <summary>
    /// 添加Join子查询 - 委托
    /// </summary>
    [Fact]
    public void Test_Join_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        _builder.From("Test").Join(builder => builder.From("Test2").Where("Name", "a"), "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 内连接 - 添加原始Sql
    /// </summary>
    [Fact]
    public void Test_Join_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendJoin("c");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左外连接
    /// </summary>
    [Fact]
    public void Test_LeftJoin_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Left Join [c] As [d]");

        //执行
        _builder.Select("a")
            .From("b")
            .LeftJoin("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左外连接 - 泛型
    /// </summary>
    [Fact]
    public void Test_LeftJoin_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Left Join [d].[Sample] As [c]");

        //执行
        _builder.Select("a")
            .From("b")
            .LeftJoin<Sample>("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左外连接子查询
    /// </summary>
    [Fact]
    public void Test_LeftJoin_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Left Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        var builder2 = _builder.New().From("Test2").Where("Name", "a");
        _builder.From("Test").LeftJoin(builder2, "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 左外连接子查询 - 委托
    /// </summary>
    [Fact]
    public void Test_LeftJoin_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Left Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        _builder.From("Test").LeftJoin(builder => builder.From("Test2").Where("Name", "a"), "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 左外连接 - 添加原始Sql
    /// </summary>
    [Fact]
    public void Test_LeftJoin_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Left Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendLeftJoin("c");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左连接 - lambda表达式
    /// </summary>
    [Fact]
    public void Test_LeftJoin_7()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a].[Email],[a].[BoolValue],[b].[Description],[b].[IntValue] ");
        result.AppendLine("From [Sample] As [a] ");
        result.Append("Left Join [Sample2] As [b] On [a].[Email]=[b].[StringValue] And [a].[IntValue]<>[b].[IntValue]");

        //执行
        _builder.Select<Sample>(t => new object[] { t.Email, t.BoolValue })
            .Select<Sample2>(t => new object[] { t.Description, t.IntValue })
            .From<Sample>("a")
            .LeftJoin<Sample2>("b").On<Sample, Sample2>((l, r) => l.Email == r.StringValue && l.IntValue != r.IntValue);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 右外连接
    /// </summary>
    [Fact]
    public void Test_RightJoin_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Right Join [c] As [d]");

        //执行
        _builder.Select("a")
            .From("b")
            .RightJoin("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 右外连接 - 泛型
    /// </summary>
    [Fact]
    public void Test_RightJoin_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Right Join [d].[Sample] As [c]");

        //执行
        _builder.Select("a")
            .From("b")
            .RightJoin<Sample>("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 右外连接子查询
    /// </summary>
    [Fact]
    public void Test_RightJoin_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Right Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        var builder2 = _builder.New().From("Test2").Where("Name", "a");
        _builder.From("Test").RightJoin(builder2, "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 右外连接子查询 - 委托
    /// </summary>
    [Fact]
    public void Test_RightJoin_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Right Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        _builder.From("Test").RightJoin(builder => builder.From("Test2").Where("Name", "a"), "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 右外连接 - 添加原始Sql
    /// </summary>
    [Fact]
    public void Test_RightJoin_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Right Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendRightJoin("c");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件
    /// </summary>
    [Fact]
    public void Test_On_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join [c] As [d] On [b].[Id]<>@_p_0");

        //执行
        _builder.Select("a")
            .From("b")
            .Join("c", "d").On("b.Id", "c", Operator.NotEqual);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件 - 属性表达式
    /// </summary>
    [Fact]
    public void Test_On_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [Sample] As [b] ");
        result.Append("Join [Sample2] As [c] On [b].[IntValue]<>[c].[IntValue]");

        //执行
        _builder.Select("a")
            .From<Sample>("b")
            .Join<Sample2>("c").On<Sample, Sample2>(t => t.IntValue, t => t.IntValue, Operator.NotEqual);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件 - 布尔表达式
    /// </summary>
    [Fact]
    public void Test_On_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [Sample] As [b] ");
        result.Append("Join [Sample2] As [c] On [b].[IntValue]<>[c].[IntValue]");

        //执行
        _builder.Select("a")
            .From<Sample>("b")
            .Join<Sample2>("c").On<Sample, Sample2>((l, r) => l.IntValue != r.IntValue);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件 - 值为字面量
    /// </summary>
    [Fact]
    public void Test_On_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a],[b] ");
        result.AppendLine("From [Sample] As [s] ");
        result.Append("Left Join [Sample2] As [s2] On [s].[IntValue]=[s2].[IntValue] And [s].[StringValue]=@_p_0");

        //执行
        _builder.Select("a,b")
            .From<Sample>("s")
            .LeftJoin<Sample2>("s2").On<Sample, Sample2>((l, r) => l.IntValue == r.IntValue && l.StringValue == "a");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        _output.WriteLine(_builder.ToSql());
        Assert.Equal("a", _builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 连接条件 - 值为变量
    /// </summary>
    [Fact]
    public void Test_On_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a],[b] ");
        result.AppendLine("From [Sample] As [s] ");
        result.Append("Left Join [Sample2] As [s2] On [s].[IntValue]=[s2].[IntValue] And [s].[StringValue]=@_p_0");

        var a = "a";

        //执行
        _builder.Select("a,b")
            .From<Sample>("s")
            .LeftJoin<Sample2>("s2").On<Sample, Sample2>((l, r) => l.IntValue == r.IntValue && l.StringValue == a);
        _output.WriteLine(result.ToString());
        _output.WriteLine(_builder.ToSql());

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        _output.WriteLine(_builder.ToSql());
        Assert.Equal("a", _builder.GetParam("@_p_0"));
    }
}
