using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Enums;
using Bing.Data.Queries;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// 统一 SQL Builder 操作状态测试。
/// </summary>
public class SqlOperationStateTest
{
    /// <summary>
    /// 用于状态迁移测试的默认目标表引用。
    /// </summary>
    private static SqlTableReference Table => new() { TableName = "samples" };

    /// <summary>
    /// 测试 - Select 后调用 Update 应立即失败。
    /// </summary>
    [Fact]
    public void Update_WhenBuilderIsSelect_ShouldThrowImmediately()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Update(Table));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 Update。", exception.Message);
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：非空原始 From 应进入查询状态，避免后续 Mutation 静默忽略已配置来源。
    /// </summary>
    [Fact]
    public void Update_WhenRawFromWasConfigured_ShouldThrowImmediately()
    {
        // Arrange
        var builder = new TestSqlBuilder().AppendFrom("samples s");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Update(Table));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 Update。", exception.Message);
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：子查询 From 应进入查询状态，避免后续 Mutation 静默忽略已配置来源。
    /// </summary>
    [Fact]
    public void DeleteFrom_WhenSubqueryFromWasConfigured_ShouldThrowImmediately()
    {
        // Arrange
        var subquery = new TestSqlBuilder().Select("Id").From("source_samples");
        var builder = new TestSqlBuilder().From(subquery, "s");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.DeleteFrom(Table));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 DeleteFrom。", exception.Message);
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试 - Update 后调用 InsertInto 应立即失败。
    /// </summary>
    [Fact]
    public void InsertInto_WhenBuilderIsUpdate_ShouldThrowImmediately()
    {
        // Arrange
        var builder = new TestSqlBuilder().Update(Table);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.InsertInto(Table));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 InsertInto。", exception.Message);
        Assert.Equal(SqlOperationKind.Update, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：UpdateFrom 只能追加到 Update 状态，不能隐式创建缺少目标表的 Update。
    /// </summary>
    [Fact]
    public void UpdateFrom_WhenBuilderHasNoUpdate_ShouldThrowWithoutChangingClause()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        var source = new SqlTableReference { TableName = "sample_updates", Alias = "s" };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.UpdateFrom(source));

        // Assert
        Assert.Equal("当前 Builder 已处于 None 状态，不能调用 UpdateFrom。", exception.Message);
        Assert.Null(builder.UpdateFromClause.Table);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：DeleteUsing 只能追加到 Delete 状态，不能隐式创建缺少目标表的 Delete。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenBuilderHasNoDelete_ShouldThrowWithoutChangingClause()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        var source = new SqlTableReference { TableName = "sample_deletes", Alias = "s" };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.DeleteUsing(source));

        // Assert
        Assert.Equal("当前 Builder 已处于 None 状态，不能调用 DeleteUsing。", exception.Message);
        Assert.Null(builder.DeleteUsingClause.Table);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Returning 只能追加到已完成类型判定的 Mutation 状态。
    /// </summary>
    [Fact]
    public void Returning_WhenBuilderHasNoMutation_ShouldThrowWithoutChangingClause()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Returning("Id"));

        // Assert
        Assert.Equal("当前 Builder 已处于 None 状态，不能调用 Returning。", exception.Message);
        Assert.True(builder.ReturningClause.IsEmpty);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Select 状态的 Returning 必须在原始列名校验前被状态门禁拒绝。
    /// </summary>
    [Fact]
    public void Returning_WhenBuilderIsSelectAndColumnIsInvalid_ShouldThrowStateExceptionWithoutChangingClause()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Returning("Id;"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 Returning。", exception.Message);
        Assert.True(builder.ReturningClause.IsEmpty);
        Assert.Empty(builder.ParameterManager.GetParams());
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Select 状态的类型化 Returning 必须在空投影访问前被状态门禁拒绝。
    /// </summary>
    [Fact]
    public void ReturningTyped_WhenBuilderIsSelectAndExpressionIsNull_ShouldThrowStateExceptionWithoutChangingClause()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id");
        Expression<Func<Sample, object>> columns = null;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Returning<Sample>(columns));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 Returning。", exception.Message);
        Assert.True(builder.ReturningClause.IsEmpty);
        Assert.Empty(builder.ParameterManager.GetParams());
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试 - Delete 后调用 Set 应立即失败且不新增参数。
    /// </summary>
    [Fact]
    public void Set_WhenBuilderIsDelete_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder().DeleteFrom(Table);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Set("Name", "Bing"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Delete 状态，不能调用 Set。", exception.Message);
        Assert.Equal(0, builder.ParameterManager.Count);
    }

    /// <summary>
    /// 测试目的：Set 参数写入失败时，不得将待定 Builder 切换为 Update 状态或留下 Set 项。
    /// </summary>
    [Fact]
    public void Set_WhenParameterWriteFails_ShouldKeepNoneStateAndEmptySetClause()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 0, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Set("Name", "Bing"));

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 0；尝试添加后数量: 1；最大参数数量: 0。",
            exception.Message);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Equal(0, builder.SetClause.Count);
        Assert.Empty(parameterManager.GetParams());
        builder.Select("Id").From("source_samples");
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：聚合参数校验失败时，不得将空 Builder 切换为 Select 状态或写入投影。
    /// </summary>
    [Fact]
    public void Aggregate_WhenValidationFails_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            builder.Aggregate(SqlAggregateFunction.Sum, "*", "Total"));

        // Assert
        Assert.Equal("column", exception.ParamName);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：原始聚合参数校验失败时，不得将空 Builder 切换为 Select 状态或写入投影。
    /// </summary>
    [Fact]
    public void AggregateRaw_WhenValidationFails_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            builder.AggregateRaw(SqlAggregateFunction.Sum, "*", "Total"));

        // Assert
        Assert.Equal("argumentSql", exception.ParamName);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：可转换聚合表达式校验失败时，不得将空 Builder 切换为 Select 状态或写入投影。
    /// </summary>
    [Fact]
    public void AggregateExpression_WhenValidationFails_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            builder.AggregateExpression(SqlAggregateFunction.Sum, "[sample].[Amount] /*", "Total"));

        // Assert
        Assert.Equal("expressionSql", exception.ParamName);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.Update(Table).Set("Name", "Bing").AllowAllRows();
        Assert.Equal(SqlOperationKind.Update, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：实体 Lambda 聚合参数缺失时，不得将空 Builder 切换为 Select 状态或写入投影。
    /// </summary>
    [Fact]
    public void AggregateTyped_WhenExpressionIsNull_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.Aggregate<Sample>(SqlAggregateFunction.Sum, null, "Total"));

        // Assert
        Assert.Equal("expression", exception.ParamName);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：DTO MemberInit 投影解析失败时，不得将空 Builder 切换为 Select 状态或写入投影。
    /// </summary>
    [Fact]
    public void Select_WhenMemberInitProjectionFails_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.Select<Sample>(sample => new InvalidProjection
        {
            Name = sample.Email.ToUpper()
        }));

        // Assert
        Assert.Equal("不支持的 DTO 投影表达式节点类型：Call。仅支持当前实体的直接成员赋值。", exception.Message);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：空数组 Lambda 投影属于无操作输入，不得切换统一 Builder 状态。
    /// </summary>
    [Fact]
    public void SelectArray_WhenExpressionIsNull_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        Expression<Func<Sample, object[]>> expression = null;

        // Act
        builder.Select(expression);

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：空子查询投影属于无操作输入，不得切换统一 Builder 状态。
    /// </summary>
    [Fact]
    public void SelectSubquery_WhenBuilderIsNull_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.SelectClause.Select((ISqlBuilder)null, "Subquery");

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.Update(Table).Set("Name", "Bing").AllowAllRows();
        Assert.Equal(SqlOperationKind.Update, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：空子查询回调属于无操作输入，不得切换统一 Builder 状态。
    /// </summary>
    [Fact]
    public void SelectSubquery_WhenActionIsNull_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.SelectClause.Select((Action<ISqlBuilder>)null, "Subquery");

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：无法解析为实体列的类型化排序属于无操作输入，不得将空 Builder 切换为 Select 状态。
    /// </summary>
    [Fact]
    public void OrderBy_WhenExpressionDoesNotResolveColumn_ShouldKeepNoneStateAndEmptyClause()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.OrderBy<Sample>(sample => new object());

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.OrderByClause.ToSql());
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：无法解析为实体列的类型化分组属于无操作输入，不得将空 Builder 切换为 Select 状态。
    /// </summary>
    [Fact]
    public void GroupBy_WhenExpressionDoesNotResolveColumn_ShouldKeepNoneStateAndEmptyClause()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.GroupBy<Sample>(sample => new object());

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.GroupByClause.ToSql());
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：空普通投影属于无操作输入，不得将空 Builder 切换为 Select 状态。
    /// </summary>
    [Fact]
    public void Select_WhenColumnsAreBlank_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.Select("   ");

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：空原始投影属于无操作输入，不得将空 Builder 切换为 Select 状态。
    /// </summary>
    [Fact]
    public void AppendSelect_WhenSqlIsBlank_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.AppendSelect("   ");

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：原始 Select 方言标识符解析失败时，不得提交 Select 状态或投影内部状态。
    /// </summary>
    [Fact]
    public void AppendSelect_WhenIdentifierResolutionFails_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var dialect = new FailingIdentifierDialect();
        var builder = new TestSqlBuilder(dialect);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.AppendSelect("[Id]"));
        dialect.ShouldFail = false;
        builder.DeleteFrom(Table).AllowAllRows();

        // Assert
        Assert.Equal("Identifier rendering failed.", exception.Message);
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
    }

    /// <summary>
    /// 测试目的：原始 GroupBy 方言标识符解析失败时，不得提交查询状态或分组项。
    /// </summary>
    [Fact]
    public void AppendGroupBy_WhenIdentifierResolutionFails_ShouldKeepNoneStateAndEmptyClause()
    {
        // Arrange
        var dialect = new FailingIdentifierDialect();
        var builder = new TestSqlBuilder(dialect);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.AppendGroupBy("[Id]"));
        dialect.ShouldFail = false;
        builder.DeleteFrom(Table).AllowAllRows();

        // Assert
        Assert.Equal("Identifier rendering failed.", exception.Message);
        Assert.False(builder.GroupByClause.IsGroup);
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Having 方言标识符解析失败时，不得提交查询状态或隐式分组条件。
    /// </summary>
    [Fact]
    public void Having_WhenIdentifierResolutionFails_ShouldKeepNoneStateAndEmptyClause()
    {
        // Arrange
        var dialect = new FailingIdentifierDialect();
        var builder = new TestSqlBuilder(dialect);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Having("[Id]>0"));
        dialect.ShouldFail = false;
        builder.DeleteFrom(Table).AllowAllRows();

        // Assert
        Assert.Equal("Identifier rendering failed.", exception.Message);
        Assert.False(builder.GroupByClause.IsGroup);
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：原始 OrderBy 方言标识符解析失败时，不得提交查询状态或排序项。
    /// </summary>
    [Fact]
    public void AppendOrderBy_WhenIdentifierResolutionFails_ShouldKeepNoneStateAndEmptyClause()
    {
        // Arrange
        var dialect = new FailingIdentifierDialect();
        var builder = new TestSqlBuilder(dialect);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.AppendOrderBy("[Id]"));
        dialect.ShouldFail = false;
        builder.DeleteFrom(Table).AllowAllRows();

        // Assert
        Assert.Equal("Identifier rendering failed.", exception.Message);
        Assert.Null(builder.OrderByClause.ToSql());
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：无法解析为实体列的标量投影属于无操作输入，不得将空 Builder 切换为 Select 状态。
    /// </summary>
    [Fact]
    public void SelectScalar_WhenExpressionDoesNotResolveColumn_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.Select<Sample>(sample => new object());

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：无法解析为实体列的数组投影属于无操作输入，不得将空 Builder 切换为 Select 状态。
    /// </summary>
    [Fact]
    public void SelectArray_WhenExpressionDoesNotResolveColumn_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.Select<Sample>(sample => new object[] { new object() });

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：不包含任何成员绑定的 DTO 投影属于无操作输入，不得将空 Builder 切换为 Select 状态。
    /// </summary>
    [Fact]
    public void Select_WhenMemberInitHasNoBindings_ShouldKeepNoneStateAndEmptyProjection()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.Select<Sample>(sample => new EmptyProjection());

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.SelectClause.ProjectionCount);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：独立 Builder 条件的参数合并失败时，不得提交查询状态、条件或参数。
    /// </summary>
    [Fact]
    public void Where_WhenBuilderConditionParameterMergeFails_ShouldKeepNoneStateAndEmptyCondition()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 0, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager);
        var conditionBuilder = new TestSqlBuilder().Where("Id", 1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Where(conditionBuilder));

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 0；尝试添加后数量: 1；最大参数数量: 0。",
            exception.Message);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.WhereClause.ToSql());
        Assert.Empty(parameterManager.GetParams());
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：独立 Builder 的 Or 条件参数合并失败时，不得提交查询状态、条件或参数。
    /// </summary>
    [Fact]
    public void Or_WhenBuilderConditionParameterMergeFails_ShouldKeepNoneStateAndEmptyCondition()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 0, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager);
        var conditionBuilder = new TestSqlBuilder().Where("Id", 1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Or(conditionBuilder));

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 0；尝试添加后数量: 1；最大参数数量: 0。",
            exception.Message);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.WhereClause.ToSql());
        Assert.Empty(parameterManager.GetParams());
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Mutation 状态不支持查询型子查询筛选时，必须在渲染和合并子查询参数前失败。
    /// </summary>
    [Fact]
    public void WhereSubquery_WhenBuilderIsUpdate_ShouldRejectBeforeMergingSubqueryParameters()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .Update(Table)
            .Set("Name", "existing");
        var subquery = new TestSqlBuilder()
            .Select("Id")
            .From("source_samples")
            .Where("Id", 1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Where("Id", subquery, Operator.In));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", exception.Message);
        Assert.Equal(SqlOperationKind.Update, builder.OperationKind);
        Assert.Equal(new object[] { "existing" }, builder.GetParams().Values);
        Assert.Null(((ISqlQueryClauseAccessor)builder).WhereClause.ToSql());
        builder.AllowAllRows();
        Assert.Equal("Update [samples] Set [Name] = @_p_0", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：不支持的子查询操作符必须在渲染和合并子查询参数前失败，避免污染父 Builder 参数状态。
    /// </summary>
    [Fact]
    public void WhereSubquery_WhenOperatorIsUnsupported_ShouldNotMergeParameters()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id").From("orders");
        var subquery = new TestSqlBuilder().Select("Id").From("source_orders").Where("Status", 1);
        var expectedSql = builder.ToSql();

        // Act
        Assert.Throws<NotImplementedException>(() => builder.Where("Id", subquery, (Operator)999));

        // Assert
        Assert.Empty(builder.ParameterManager.GetParams());
        Assert.Equal(expectedSql, builder.ToSql());
    }

    /// <summary>
    /// 测试目的：空 Where 条件属于无操作输入，不得将空 Builder 切换为 Select 状态。
    /// </summary>
    [Fact]
    public void Where_WhenConditionIsNull_ShouldKeepNoneStateAndEmptyCondition()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.Where((ICondition)null);

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.WhereClause.ToSql());
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：渲染为空的 Or 条件属于无操作输入，不得将空 Builder 切换为 Select 状态。
    /// </summary>
    [Fact]
    public void Or_WhenConditionRendersBlank_ShouldKeepNoneStateAndEmptyCondition()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.Or(NullCondition.Instance);

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Null(builder.WhereClause.ToSql());
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：后续 CTE 分支参数超过上限时，不得保留前序 CTE 分支已合并的参数。
    /// </summary>
    [Fact]
    public void Cte_WhenLaterBranchParameterExceedsLimit_ShouldNotKeepEarlierBranchParameters()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 1, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager)
            .Select("Id")
            .From("Orders")
            .With("first", new TestSqlBuilder().Select("Id").From("FirstOrders").Where("Id", 1))
            .With("second", new TestSqlBuilder().Select("Id").From("SecondOrders").Where("Id", 2));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 1；尝试添加后数量: 2；最大参数数量: 1。",
            exception.Message);
        Assert.Empty(parameterManager.GetParams());
    }

    /// <summary>
    /// 测试目的：后续 Union 分支参数超过上限时，不得保留前序 Union 分支已合并的参数。
    /// </summary>
    [Fact]
    public void Union_WhenLaterBranchParameterExceedsLimit_ShouldNotKeepEarlierBranchParameters()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 1, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager)
            .Select("Id")
            .From("Orders")
            .Union(
                new TestSqlBuilder().Select("Id").From("FirstOrders").Where("Id", 1),
                new TestSqlBuilder().Select("Id").From("SecondOrders").Where("Id", 2));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 1；尝试添加后数量: 2；最大参数数量: 1。",
            exception.Message);
        Assert.Empty(parameterManager.GetParams());
    }

    /// <summary>
    /// 测试目的：批量 Insert 列中后续列名无效时，不得保留前序列或污染待定 Insert 状态。
    /// </summary>
    [Fact]
    public void InsertColumns_WhenLaterColumnIsInvalid_ShouldKeepEmptyColumnsAndPendingState()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table);

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.Columns("Id", " "));

        // Assert
        Assert.Equal("column", exception.ParamName);
        Assert.Empty(builder.InsertColumnsClause.Columns);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        builder.Select("Id").From("source_samples");
        Assert.Equal(SqlOperationKind.InsertSelect, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：实体表达式批量 Insert 列中后续主键不可写时，不得保留前序列或污染待定 Insert 状态。
    /// </summary>
    [Fact]
    public void InsertColumnsTyped_WhenLaterColumnIsNotWritable_ShouldKeepEmptyColumnsAndPendingState()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.InsertInto<InsertColumnAtomicitySample>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MutationClauseExtensions.Columns<TestSqlBuilder, InsertColumnAtomicitySample>(builder,
                item => new object[] { item.Name, item.Id }));

        // Assert
        Assert.Equal("实体 InsertColumnAtomicitySample 的属性 Id 不能用于插入。", exception.Message);
        Assert.Empty(builder.InsertColumnsClause.Columns);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        builder.Select("Id").From("source_samples");
        Assert.Equal(SqlOperationKind.InsertSelect, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Select 状态通过显式泛型 InsertInto 时必须在实体映射前被状态门禁拒绝。
    /// </summary>
    [Fact]
    public void InsertIntoTypedGeneric_WhenBuilderIsSelect_ShouldRejectBeforeResolvingMapping()
    {
        // Arrange
        var resolver = new ThrowingMappingResolver();
        var builder = new TestSqlBuilder(entityMappingResolver: resolver).Select("Id").From("source_samples");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MutationClauseExtensions.InsertInto<TestSqlBuilder, Sample>(builder));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 InsertInto。", exception.Message);
        Assert.Equal(0, resolver.ResolveCallCount);
        Assert.Null(builder.InsertClause.Table);
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Select 状态通过显式泛型 Update 时必须在实体映射前被状态门禁拒绝。
    /// </summary>
    [Fact]
    public void UpdateTypedGeneric_WhenBuilderIsSelect_ShouldRejectBeforeResolvingMapping()
    {
        // Arrange
        var resolver = new ThrowingMappingResolver();
        var builder = new TestSqlBuilder(entityMappingResolver: resolver).Select("Id").From("source_samples");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MutationClauseExtensions.Update<TestSqlBuilder, Sample>(builder));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 Update。", exception.Message);
        Assert.Equal(0, resolver.ResolveCallCount);
        Assert.Null(builder.UpdateClause.Table);
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Select 状态通过显式泛型 DeleteFrom 时必须在实体映射前被状态门禁拒绝。
    /// </summary>
    [Fact]
    public void DeleteFromTypedGeneric_WhenBuilderIsSelect_ShouldRejectBeforeResolvingMapping()
    {
        // Arrange
        var resolver = new ThrowingMappingResolver();
        var builder = new TestSqlBuilder(entityMappingResolver: resolver).Select("Id").From("source_samples");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MutationClauseExtensions.DeleteFrom<TestSqlBuilder, Sample>(builder));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 DeleteFrom。", exception.Message);
        Assert.Equal(0, resolver.ResolveCallCount);
        Assert.Null(builder.DeleteClause.Table);
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：实体 InsertInto 投影解析失败时，不得保留目标表或待定 Insert 状态。
    /// </summary>
    [Fact]
    public void InsertIntoTyped_WhenProjectionIsInvalid_ShouldKeepEmptyTargetAndNoneState()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            builder.InsertInto<InsertColumnAtomicitySample>(item => item.Name.ToUpper()));

        // Assert
        Assert.Equal("expression", exception.ParamName);
        Assert.Null(builder.InsertClause.Table);
        Assert.Empty(builder.InsertColumnsClause.Columns);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        builder.DeleteFrom(Table).AllowAllRows();
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Select 状态的原始 InsertInto 必须在表名解析前被状态门禁拒绝。
    /// </summary>
    [Fact]
    public void InsertInto_WhenBuilderIsSelectAndTableIsInvalid_ShouldThrowStateExceptionWithoutChangingTarget()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.InsertInto("samples;"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 InsertInto。", exception.Message);
        Assert.Null(builder.InsertClause.Table);
        Assert.Empty(builder.InsertColumnsClause.Columns);
        Assert.Empty(builder.ParameterManager.GetParams());
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Select 状态的类型化 InsertInto 必须在投影映射前被状态门禁拒绝。
    /// </summary>
    [Fact]
    public void InsertIntoTyped_WhenBuilderIsSelectAndProjectionIsInvalid_ShouldThrowStateExceptionWithoutChangingTarget()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.InsertInto<Sample>(sample => sample.Email.ToUpper()));

        // Assert
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 InsertInto。", exception.Message);
        Assert.Null(builder.InsertClause.Table);
        Assert.Empty(builder.InsertColumnsClause.Columns);
        Assert.Empty(builder.ParameterManager.GetParams());
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：分页的 Take 参数预检失败时，不得保留 Skip 参数、分页配置或部分分页 SQL。
    /// </summary>
    [Fact]
    public void Page_WhenTakeParameterExceedsLimit_ShouldKeepPagingStateUnchanged()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 1, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager).Select("Id").From("source_samples");
        var initialPager = builder.Pager;
        var initialPageSize = initialPager.PageSize;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Page(new Pager(2, 10)));

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 1；尝试添加后数量: 2；最大参数数量: 1。",
            exception.Message);
        Assert.Empty(parameterManager.GetParams());
        Assert.Same(initialPager, builder.Pager);
        Assert.Equal(1, builder.Pager.Page);
        Assert.Equal(initialPageSize, builder.Pager.PageSize);
        Assert.Equal("Select [Id] \r\nFrom [source_samples]", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：空 Builder 配置分页后应进入查询状态，禁止后续混用 Mutation。
    /// </summary>
    [Fact]
    public void Take_WhenBuilderIsNone_ShouldUseSelectStateAndRejectUpdate()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        builder.Take(10);
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Update(Table));

        // Assert
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
        Assert.Equal(new object[] { 10 }, builder.ParameterManager.GetParams().Values.ToArray());
        Assert.Equal("当前 Builder 已处于 Select 状态，不能调用 Update。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Update 状态不得接受 Skip，且不能写入分页参数。
    /// </summary>
    [Fact]
    public void Skip_WhenBuilderIsUpdate_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder().Update(Table);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Skip(5));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Paging。", exception.Message);
        Assert.Equal(SqlOperationKind.Update, builder.OperationKind);
        Assert.Empty(builder.ParameterManager.GetParams());
    }

    /// <summary>
    /// 测试目的：Delete 状态不得接受 Take，且不能写入分页参数。
    /// </summary>
    [Fact]
    public void Take_WhenBuilderIsDelete_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder().DeleteFrom(Table);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Take(10));

        // Assert
        Assert.Equal("当前 Builder 已处于 Delete 状态，不能调用 Paging。", exception.Message);
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
        Assert.Empty(builder.ParameterManager.GetParams());
    }

    /// <summary>
    /// 测试目的：所有 Insert 运行状态不得接受 Page，避免生成不会参与 Insert SQL 的孤立参数。
    /// </summary>
    [Theory]
    [InlineData("Insert", SqlOperationKind.None)]
    [InlineData("InsertValues", SqlOperationKind.InsertValues)]
    [InlineData("InsertSelect", SqlOperationKind.InsertSelect)]
    public void Page_WhenBuilderIsInsert_ShouldThrowWithoutChangingPagingState(string operationName,
        SqlOperationKind operationKind)
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table);
        switch (operationName)
        {
            case "InsertValues":
                builder.Columns("Id").Values(1);
                break;
            case "InsertSelect":
                builder.Select("Id").From("source_samples");
                break;
        }
        var initialPager = builder.Pager;
        var initialParameters = builder.ParameterManager.GetParams().Values.ToArray();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Page(new Pager(2, 10)));

        // Assert
        Assert.Equal($"当前 Builder 已处于 {operationName} 状态，不能调用 Paging。", exception.Message);
        Assert.Equal(operationKind, builder.OperationKind);
        Assert.Same(initialPager, builder.Pager);
        Assert.Equal(initialParameters, builder.ParameterManager.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：空 Builder 的分页参数写入失败时，不得切换为查询状态。
    /// </summary>
    [Fact]
    public void Take_WhenParameterWriteFailsOnEmptyBuilder_ShouldKeepNoneState()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 0, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Take(10));

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 0；尝试添加后数量: 1；最大参数数量: 0。",
            exception.Message);
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Empty(parameterManager.GetParams());
    }

    /// <summary>
    /// 测试目的：单独 Take 的参数写入失败时，不得保留限制参数名或启用部分分页 SQL。
    /// </summary>
    [Fact]
    public void Take_WhenParameterWriteFails_ShouldKeepPagingSqlUnchanged()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 0, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager).Select("Id").From("source_samples");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Take(10));

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 0；尝试添加后数量: 1；最大参数数量: 0。",
            exception.Message);
        Assert.Empty(parameterManager.GetParams());
        Assert.Equal("Select [Id] \r\nFrom [source_samples]", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：单独 Skip 的默认 Offset 参数写入失败时，不得保留参数名或启用部分分页 SQL。
    /// </summary>
    [Fact]
    public void Skip_WhenParameterWriteFails_ShouldKeepPagingSqlUnchanged()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 0, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager).Select("Id").From("source_samples");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Skip(5));

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 0；尝试添加后数量: 1；最大参数数量: 0。",
            exception.Message);
        Assert.Empty(parameterManager.GetParams());
        Assert.Equal("Select [Id] \r\nFrom [source_samples]", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：分页渲染补充默认 Offset 参数失败时，不得保留未绑定的 Offset 参数名。
    /// </summary>
    [Fact]
    public void ToSql_WhenDefaultOffsetParameterExceedsLimit_ShouldNotKeepUnboundOffsetParameter()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 1, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager)
            .Select("Id")
            .From("source_samples")
            .OrderBy("Id")
            .Take(10);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 1；尝试添加后数量: 2；最大参数数量: 1。",
            exception.Message);
        Assert.Equal(new object[] { 10 }, parameterManager.GetParams().Values.ToArray());
        Assert.Throws<InvalidOperationException>(() => builder.ToSql());
        Assert.Equal(new object[] { 10 }, parameterManager.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：分页渲染器在自动 Offset 参数创建后失败时，不得向 Builder 提交参数或参数名状态。
    /// </summary>
    [Fact]
    public void ToSql_WhenPaginationRendererFails_ShouldNotKeepDefaultOffsetParameter()
    {
        // Arrange
        var parameterManager = new ParameterManager(TestDialect.Instance);
        var builder = new FailingPaginationSqlBuilder(parameterManager);
        builder.Select("Id").From("source_samples").OrderBy("Id").Take(10);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Pagination rendering failed.", exception.Message);
        Assert.Equal(new object[] { 10 }, parameterManager.GetParams().Values.ToArray());
        Assert.Null(builder.OffsetParameterName);
        Assert.Throws<InvalidOperationException>(() => builder.ToSql());
        Assert.Equal(new object[] { 10 }, parameterManager.GetParams().Values.ToArray());
        Assert.Null(builder.OffsetParameterName);
    }

    /// <summary>
    /// 测试目的：清除分页配置时只能移除分页参数，必须保留普通查询参数且不能遗留旧分页绑定。
    /// </summary>
    [Fact]
    public void ClearPageParams_WhenPagingWasConfigured_ShouldRemoveOnlyPagingParameters()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.Select("Id").From("source_samples").Where("Status", 1).OrderBy("Id").Take(10);

        // Act
        builder.ClearPageParams();
        var sql = builder.ToSql();
        builder.Take(20);

        // Assert
        Assert.DoesNotContain("Limit", sql);
        Assert.Equal(new object[] { 1, 20 }, builder.ParameterManager.GetParams().Values.ToArray());
        Assert.Equal(2, builder.ParameterManager.GetParams().Count);
    }

    /// <summary>
    /// 测试目的：Union 集合在后续枚举失败时不得保留前序已克隆分支或改变父查询结构。
    /// </summary>
    [Fact]
    public void Union_WhenLaterEnumerationFails_ShouldNotKeepEarlierUnionItems()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id").From("orders");
        var expectedSql = builder.ToSql();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Union(ThrowAfterFirstUnionBuilder()));

        // Assert
        Assert.Equal("Union enumeration failed.", exception.Message);
        Assert.Empty(builder.UnionItems);
        Assert.Equal(expectedSql, builder.ToSql());
    }

    /// <summary>
    /// 测试 - Insert Values 后调用 Select 应立即失败。
    /// </summary>
    [Fact]
    public void Select_WhenBuilderIsInsertValues_ShouldThrowImmediately()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Values(1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Select("Id"));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertValues 状态，不能调用 Select。", exception.Message);
        Assert.Equal(SqlOperationKind.InsertValues, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Insert Values 状态下的标量 Where 应在参数创建前失败，不能遗留未引用参数。
    /// </summary>
    [Fact]
    public void Where_WhenBuilderIsInsertValues_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Values(1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Where("Name", "Bing"));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertValues 状态，不能调用 Where。", exception.Message);
        Assert.Equal(new object[] { 1 }, builder.ParameterManager.GetParams().Values.ToArray());
        Assert.Equal(SqlOperationKind.InsertValues, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：统一 Builder 的强类型 Where 在 Select 状态必须使用查询 Where 子句，保留查询状态和参数绑定。
    /// </summary>
    [Fact]
    public void WhereTyped_WhenBuilderIsSelect_ShouldRenderPredicateAndRemainSelect()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id").From("samples");

        // Act
        builder.Where<Sample, int>(sample => sample.IntValue, 7);

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [samples] \r\nWhere [IntValue]=@_p_0", builder.ToSql());
        Assert.Equal(7, builder.ParameterManager.GetValue("@_p_0"));
        Assert.Equal(SqlOperationKind.Select, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Insert Values 状态下的强类型 Where 必须在参数映射和表达式求值前保持既有查询状态门禁。
    /// </summary>
    [Fact]
    public void WhereTyped_WhenBuilderIsInsertValues_ShouldThrowWithoutAddingParameters()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Values(1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Where<Sample, int>(sample => sample.IntValue, 7));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertValues 状态，不能调用 Where。", exception.Message);
        Assert.Equal(new object[] { 1 }, builder.ParameterManager.GetParams().Values.ToArray());
        Assert.Equal(SqlOperationKind.InsertValues, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：强类型 Where 在 Insert Values 状态必须先拒绝操作，不能因空表达式偏离既有状态门禁异常。
    /// </summary>
    [Fact]
    public void WhereTyped_WhenBuilderIsInsertValuesAndExpressionIsNull_ShouldThrowStateExceptionWithoutAddingParameters()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Values(1);
        Expression<Func<Sample, int>> expression = null;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Where<Sample, int>(expression, 7));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertValues 状态，不能调用 Where。", exception.Message);
        Assert.Equal(new object[] { 1 }, builder.ParameterManager.GetParams().Values.ToArray());
        Assert.Equal(SqlOperationKind.InsertValues, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Mutation 标量 Where 的参数格式化失败时，不得保留未引用参数或改变已有 Where 状态。
    /// </summary>
    [Fact]
    public void MutationWhere_WhenParameterRenderingFails_ShouldNotAddScalarParameterOrCondition()
    {
        // Arrange
        var dialect = new FailingParameterDialect();
        var builder = new TestSqlBuilder(dialect).Update(Table).Set("Name", "existing");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => MutationClauseExtensions.Where(builder, "Id", 7));

        // Assert
        Assert.Equal("Parameter rendering failed.", exception.Message);
        Assert.Equal(new object[] { "existing" }, builder.ParameterManager.GetParams().Values.ToArray());
        Assert.True(((IMutationWhereClauseAccessor)builder).WhereClause.IsEmpty);
        Assert.Equal(SqlOperationKind.Update, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Mutation 类型化 Where 的参数格式化失败时，不得保留带元数据的未引用参数或改变已有 Where 状态。
    /// </summary>
    [Fact]
    public void MutationTypedWhere_WhenParameterRenderingFails_ShouldNotAddParameterOrCondition()
    {
        // Arrange
        var dialect = new FailingParameterDialect();
        var builder = new TestSqlBuilder(dialect).Update(Table).Set("Name", "existing");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Where<Sample, int>(sample => sample.IntValue, 7));

        // Assert
        Assert.Equal("Parameter rendering failed.", exception.Message);
        Assert.Equal(new object[] { "existing" }, builder.ParameterManager.GetParams().Values.ToArray());
        Assert.True(((IMutationWhereClauseAccessor)builder).WhereClause.IsEmpty);
        Assert.Equal(SqlOperationKind.Update, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Delete 标量 Where 的参数格式化失败时，不得保留未引用参数或写入 Mutation Where。
    /// </summary>
    [Fact]
    public void MutationDeleteWhere_WhenParameterRenderingFails_ShouldNotAddScalarParameterOrCondition()
    {
        // Arrange
        var builder = new TestSqlBuilder(new FailingParameterDialect()).DeleteFrom(Table);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => MutationClauseExtensions.Where(builder, "Id", 7));

        // Assert
        Assert.Equal("Parameter rendering failed.", exception.Message);
        Assert.Empty(builder.ParameterManager.GetParams());
        Assert.True(((IMutationWhereClauseAccessor)builder).WhereClause.IsEmpty);
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Delete 类型化 Where 的参数格式化失败时，不得保留带元数据的未引用参数或写入 Mutation Where。
    /// </summary>
    [Fact]
    public void MutationDeleteTypedWhere_WhenParameterRenderingFails_ShouldNotAddParameterOrCondition()
    {
        // Arrange
        var builder = new TestSqlBuilder(new FailingParameterDialect()).DeleteFrom(Table);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Where<Sample, int>(sample => sample.IntValue, 7));

        // Assert
        Assert.Equal("Parameter rendering failed.", exception.Message);
        Assert.Empty(builder.ParameterManager.GetParams());
        Assert.True(((IMutationWhereClauseAccessor)builder).WhereClause.IsEmpty);
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Insert Values 状态下的表达式 Where 应在表达式参数解析前失败，不能遗留参数。
    /// </summary>
    [Fact]
    public void WhereExpression_WhenBuilderIsInsertValues_ShouldThrowWithoutAddingParameters()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Values(1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Where<Sample>(sample => sample.Email == "bing@example.com"));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertValues 状态，不能调用 Where。", exception.Message);
        Assert.Equal(new object[] { 1 }, builder.ParameterManager.GetParams().Values.ToArray());
        Assert.Equal(SqlOperationKind.InsertValues, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Insert Values 状态下的 In 集合条件应在集合参数预分配前失败，不能遗留多个参数。
    /// </summary>
    [Fact]
    public void In_WhenBuilderIsInsertValues_ShouldThrowWithoutAddingParameters()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Values(1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.In("Id", new object[] { 2, 3 }));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertValues 状态，不能调用 Where。", exception.Message);
        Assert.Equal(new object[] { 1 }, builder.ParameterManager.GetParams().Values.ToArray());
        Assert.Equal(SqlOperationKind.InsertValues, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Insert Values 状态下的 Between 应在范围参数创建前失败，不能遗留参数。
    /// </summary>
    [Fact]
    public void Between_WhenBuilderIsInsertValues_ShouldThrowWithoutAddingParameters()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Values(1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Between("Id", 2, 3, Boundary.Both));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertValues 状态，不能调用 Where。", exception.Message);
        Assert.Equal(new object[] { 1 }, builder.ParameterManager.GetParams().Values.ToArray());
        Assert.Equal(SqlOperationKind.InsertValues, builder.OperationKind);
    }

    /// <summary>
    /// 测试 - Insert Select 后调用 Values 应立即失败且不新增参数。
    /// </summary>
    [Fact]
    public void Values_WhenBuilderIsInsertSelect_ShouldThrowWithoutAddingParameter()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Select("Id").From("source_samples");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Values(1));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertSelect 状态，不能调用 Values。", exception.Message);
        Assert.Equal(0, builder.ParameterManager.Count);
    }

    /// <summary>
    /// 测试目的：Insert Select 状态调用 Values 时应在枚举调用方集合前被状态门禁拒绝。
    /// </summary>
    [Fact]
    public void Values_WhenBuilderIsInsertSelect_ShouldRejectBeforeEnumeratingRows()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id").Select("Id").From("source_samples");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ValuesClause.AddRows(ThrowingInsertRows()));

        // Assert
        Assert.Equal("当前 Builder 已处于 InsertSelect 状态，不能调用 Values。", exception.Message);
        Assert.Equal(0, builder.ParameterManager.Count);
        Assert.Equal(SqlOperationKind.InsertSelect, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Update 状态调用 Insert Columns 时应优先报告状态错误，而不是校验调用方列名。
    /// </summary>
    [Fact]
    public void Columns_WhenBuilderIsUpdateAndColumnIsInvalid_ShouldThrowStateException()
    {
        // Arrange
        var builder = new TestSqlBuilder().Update(Table);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Columns(" "));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 InsertInto。", exception.Message);
        Assert.Empty(builder.InsertColumnsClause.Columns);
        Assert.Equal(SqlOperationKind.Update, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Delete 状态调用 Set 时应优先报告状态错误，而不是校验调用方列名。
    /// </summary>
    [Fact]
    public void Set_WhenBuilderIsDeleteAndColumnIsInvalid_ShouldThrowStateException()
    {
        // Arrange
        var builder = new TestSqlBuilder().DeleteFrom(Table);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Set(" ", "Bing"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Delete 状态，不能调用 Set。", exception.Message);
        Assert.Equal(0, builder.SetClause.Count);
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
    }

    /// <summary>
    /// 测试目的：Values 参数预检失败时，不得将统一 Builder 切换为 InsertValues 状态。
    /// </summary>
    [Fact]
    public void Values_WhenParameterPrevalidationFails_ShouldKeepPendingInsertState()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 1, "test");
        var builder = new TestSqlBuilder(parameterManager: parameterManager).InsertInto(Table).Columns("Id", "Name");

        // Act
        Assert.Throws<InvalidOperationException>(() => builder.Values(1, "Bing"));

        // Assert
        Assert.Equal(SqlOperationKind.None, builder.OperationKind);
        Assert.Empty(parameterManager.GetParams());
        builder.Select("Id").From("source_samples");
        Assert.Equal(SqlOperationKind.InsertSelect, builder.OperationKind);
    }

    /// <summary>
    /// 测试 - InsertInto 后公开状态应保持 None，直到 Values 或 Select 确定操作类型。
    /// </summary>
    [Fact]
    public void InsertInto_WhenSourceIsPending_ShouldExposeNoneUntilSourceIsSelected()
    {
        // Arrange
        var builder = new TestSqlBuilder().InsertInto(Table).Columns("Id");

        // Act
        var pending = builder.OperationKind;
        builder.Select("Id");

        // Assert
        Assert.Equal(SqlOperationKind.None, pending);
        Assert.Equal(SqlOperationKind.InsertSelect, builder.OperationKind);
    }

    /// <summary>
    /// 测试 - Clear 后应清空状态并允许切换到其他操作。
    /// </summary>
    [Fact]
    public void Clear_WhenBuilderHasOperation_ShouldResetStateAndAllowSwitch()
    {
        // Arrange
        var builder = new TestSqlBuilder().Update(Table).Set("Name", "Bing");

        // Act
        builder.Clear().DeleteFrom(Table).AllowAllRows();

        // Assert
        Assert.Equal(SqlOperationKind.Delete, builder.OperationKind);
        Assert.Equal("Delete From [samples]", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：清空 From 创建新子句失败时，必须保留原来源 SQL 和已注册的根别名。
    /// </summary>
    [Fact]
    public void ClearFrom_WhenClauseFactoryFails_ShouldKeepExistingSourceAndAlias()
    {
        // Arrange
        var builder = new FailingClauseFactorySqlBuilder();
        builder.Select("o.Id").From<Sample>("o");
        var expectedSql = builder.ToSql();
        builder.ClauseFactory.FailAt = FailingClause.From;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ClearFrom());

        // Assert
        Assert.Equal("Clause factory failed for From.", exception.Message);
        Assert.Equal(expectedSql, builder.ToSql());
        Assert.Throws<InvalidOperationException>(() => builder.Join<Sample>("o"));
    }

    /// <summary>
    /// 测试目的：清空 Join 创建新子句失败时，必须保留原关联 SQL 和已注册的连接别名。
    /// </summary>
    [Fact]
    public void ClearJoin_WhenClauseFactoryFails_ShouldKeepExistingJoinAndAlias()
    {
        // Arrange
        var builder = new FailingClauseFactorySqlBuilder();
        builder.Select("o.Id")
            .From<Sample>("o")
            .Join<Sample2>("i");
        var expectedSql = builder.ToSql();
        builder.ClauseFactory.FailAt = FailingClause.Join;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ClearJoin());

        // Assert
        Assert.Equal("Clause factory failed for Join.", exception.Message);
        Assert.Equal(expectedSql, builder.ToSql());
        Assert.Throws<InvalidOperationException>(() => builder.Join<Sample>("i"));
    }

    /// <summary>
    /// 测试目的：完整清空在首个子句工厂失败时，必须保留已有查询和别名注册状态。
    /// </summary>
    [Fact]
    public void Clear_WhenClauseFactoryFails_ShouldKeepExistingQueryAndAlias()
    {
        // Arrange
        var builder = new FailingClauseFactorySqlBuilder();
        builder.Select("o.Id").From<Sample>("o");
        var expectedSql = builder.ToSql();
        builder.ClauseFactory.FailAt = FailingClause.Select;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Clear());

        // Assert
        Assert.Equal("Clause factory failed for Select.", exception.Message);
        Assert.Equal(expectedSql, builder.ToSql());
        Assert.Throws<InvalidOperationException>(() => builder.Join<Sample>("o"));
    }

    /// <summary>
    /// 测试 - Clone 应保留操作状态且后续修改相互隔离。
    /// </summary>
    [Fact]
    public void Clone_WhenBuilderIsUpdate_ShouldPreserveIndependentState()
    {
        // Arrange
        var source = new TestSqlBuilder().Update(Table).Set("Name", "Bing").AllowAllRows();

        // Act
        var clone = source.Clone();
        source.Clear().DeleteFrom(Table).AllowAllRows();

        // Assert
        Assert.Equal(SqlOperationKind.Update, clone.OperationKind);
        Assert.Equal(SqlOperationKind.Delete, source.OperationKind);
        Assert.Equal("Update [samples] Set [Name] = @_p_0", clone.ToSql());
    }

    /// <summary>
    /// 测试 - New 应共享配置但返回空状态 Builder。
    /// </summary>
    [Fact]
    public void New_WhenSourceHasOperation_ShouldReturnNoneState()
    {
        // Arrange
        var source = new TestSqlBuilder().DeleteFrom(Table).AllowAllRows();

        // Act
        var result = source.New();

        // Assert
        Assert.Equal(SqlOperationKind.None, result.OperationKind);
        Assert.Equal(0, ((ISqlCommonPartAccessor)result).ParameterManager.Count);
    }

    /// <summary>
    /// 在返回首个 Union Builder 后抛出异常的枚举器。
    /// </summary>
    private static IEnumerable<ISqlBuilder> ThrowAfterFirstUnionBuilder()
    {
        yield return new TestSqlBuilder().Select("Id").From("archived_orders");
        throw new InvalidOperationException("Union enumeration failed.");
    }

    /// <summary>
    /// 在枚举 Insert Values 行前抛出异常的集合。
    /// </summary>
    private static IEnumerable<IReadOnlyList<object>> ThrowingInsertRows()
    {
        yield return new object[] { 1 };
        throw new InvalidOperationException("Insert Values rows must not be enumerated.");
    }

    /// <summary>
    /// 用于验证状态拒绝不得触发实体映射的解析器。
    /// </summary>
    private sealed class ThrowingMappingResolver : IEntityMappingResolver
    {
        /// <summary>
        /// Resolve 调用次数。
        /// </summary>
        public int ResolveCallCount { get; private set; }

        /// <inheritdoc />
        public EntityDescriptor GetDescriptor(Type entityType) =>
            throw new InvalidOperationException("Entity descriptor must not be resolved.");

        /// <inheritdoc />
        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext)
        {
            ResolveCallCount++;
            throw new InvalidOperationException("Entity mapping must not be resolved.");
        }
    }

    /// <summary>
    /// 用于验证不支持 MemberInit 表达式的 DTO 类型。
    /// </summary>
    private sealed class InvalidProjection
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 用于验证空 DTO 投影的测试类型。
    /// </summary>
    private sealed class EmptyProjection
    {
    }

    /// <summary>
    /// 使用失败分页渲染器的测试 Builder。
    /// </summary>
    private sealed class FailingPaginationSqlBuilder : SqlBuilderBase
    {
        /// <summary>
        /// 初始化测试 Builder。
        /// </summary>
        /// <param name="parameterManager">参数管理器。</param>
        public FailingPaginationSqlBuilder(IParameterManager parameterManager = null)
            : base(FailingPaginationSqlProvider.Instance, new SqlBuilderServices(), parameterManager)
        {
        }

        /// <summary>
        /// 获取当前 Offset 参数名，仅用于验证失败渲染状态。
        /// </summary>
        public string OffsetParameterName => OffsetParam;

        /// <inheritdoc />
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new FailingPaginationSqlBuilder(parameterManager);
    }

    /// <summary>
    /// 在分页渲染时失败的测试 Provider。
    /// </summary>
    private sealed class FailingPaginationSqlProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>
        /// 测试 Provider 单例。
        /// </summary>
        public static FailingPaginationSqlProvider Instance { get; } = new();

        /// <inheritdoc />
        public string Key => "test.failing-pagination";

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect => TestDialect.Instance;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new FailingPaginationRenderer();

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => new ParamLiteralsResolver();

        /// <inheritdoc />
        public SqlProviderProfile Profile { get; } = new()
        {
            Query = new SqlProviderQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
        };
    }

    /// <summary>
    /// 始终失败的分页 SQL 渲染器。
    /// </summary>
    private sealed class FailingPaginationRenderer : ISqlPaginationRenderer
    {
        /// <inheritdoc />
        public string Render(string offsetParameterName, string limitParameterName) =>
            throw new InvalidOperationException("Pagination rendering failed.");
    }

    /// <summary>
    /// 子句工厂的可控失败点。
    /// </summary>
    private enum FailingClause
    {
        /// <summary>不失败。</summary>
        None,

        /// <summary>Select 子句。</summary>
        Select,

        /// <summary>From 子句。</summary>
        From,

        /// <summary>Join 子句。</summary>
        Join
    }

    /// <summary>
    /// 使用可控失败子句工厂的测试 Builder。
    /// </summary>
    private sealed class FailingClauseFactorySqlBuilder : SqlBuilderBase
    {
        /// <summary>
        /// 初始化测试 Builder。
        /// </summary>
        /// <param name="parameterManager">参数管理器。</param>
        public FailingClauseFactorySqlBuilder(IParameterManager parameterManager = null)
            : this(new FailingClauseFactory(), parameterManager)
        {
        }

        /// <summary>
        /// 使用指定子句工厂初始化测试 Builder。
        /// </summary>
        /// <param name="clauseFactory">可控失败子句工厂。</param>
        /// <param name="parameterManager">参数管理器。</param>
        private FailingClauseFactorySqlBuilder(FailingClauseFactory clauseFactory, IParameterManager parameterManager)
            : base(new FailingClauseFactorySqlProvider(clauseFactory), new SqlBuilderServices(), parameterManager)
        {
            ClauseFactory = clauseFactory;
        }

        /// <summary>
        /// 获取可控失败子句工厂。
        /// </summary>
        public FailingClauseFactory ClauseFactory { get; }

        /// <inheritdoc />
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new FailingClauseFactorySqlBuilder(ClauseFactory, parameterManager);
    }

    /// <summary>
    /// 使用可控失败子句工厂的测试 Provider。
    /// </summary>
    private sealed class FailingClauseFactorySqlProvider : ISqlProvider
    {
        /// <summary>
        /// 初始化测试 Provider。
        /// </summary>
        /// <param name="clauseFactory">可控失败子句工厂。</param>
        public FailingClauseFactorySqlProvider(FailingClauseFactory clauseFactory) => ClauseFactory = clauseFactory;

        /// <inheritdoc />
        public string Key => "test.failing-clause-factory";

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect => TestDialect.Instance;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; }

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new TestPaginationRenderer();

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver { get; } = new ParamLiteralsResolver();
    }

    /// <summary>
    /// 按指定位置抛出异常的子句工厂。
    /// </summary>
    private sealed class FailingClauseFactory : ISqlClauseFactory
    {
        /// <summary>
        /// 默认子句工厂。
        /// </summary>
        private readonly DefaultSqlClauseFactory _inner = new();

        /// <summary>
        /// 获取或设置当前失败点。
        /// </summary>
        public FailingClause FailAt { get; set; }

        /// <inheritdoc />
        public ISelectClause CreateSelect(SqlClauseContext context)
        {
            ThrowIfFailed(FailingClause.Select);
            return _inner.CreateSelect(context);
        }

        /// <inheritdoc />
        public IFromClause CreateFrom(SqlClauseContext context)
        {
            ThrowIfFailed(FailingClause.From);
            return _inner.CreateFrom(context);
        }

        /// <inheritdoc />
        public IJoinClause CreateJoin(SqlClauseContext context)
        {
            ThrowIfFailed(FailingClause.Join);
            return _inner.CreateJoin(context);
        }

        /// <inheritdoc />
        public IWhereClause CreateWhere(SqlClauseContext context) => _inner.CreateWhere(context);

        /// <inheritdoc />
        public IGroupByClause CreateGroupBy(SqlClauseContext context) => _inner.CreateGroupBy(context);

        /// <inheritdoc />
        public IOrderByClause CreateOrderBy(SqlClauseContext context) => _inner.CreateOrderBy(context);

        /// <summary>
        /// 在匹配当前失败点时抛出异常。
        /// </summary>
        /// <param name="clause">待创建的子句。</param>
        private void ThrowIfFailed(FailingClause clause)
        {
            if (FailAt == clause)
                throw new InvalidOperationException($"Clause factory failed for {clause}.");
        }
    }

    /// <summary>
    /// 测试 Provider 使用的固定分页渲染器。
    /// </summary>
    private sealed class TestPaginationRenderer : ISqlPaginationRenderer
    {
        /// <inheritdoc />
        public string Render(string offsetParameterName, string limitParameterName) =>
            $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
    }

    /// <summary>
    /// 在参数标记格式化时失败的测试方言。
    /// </summary>
    private sealed class FailingParameterDialect : DialectBase
    {
        /// <inheritdoc />
        public override string GetParamName(string paramName)
        {
            if (paramName is "@_p_0" or "@_p_1")
                throw new InvalidOperationException("Parameter rendering failed.");
            return base.GetParamName(paramName);
        }
    }

    /// <summary>
    /// 在标识符格式化时失败的测试方言。
    /// </summary>
    private sealed class FailingIdentifierDialect : DialectBase
    {
        /// <summary>
        /// 是否在读取标识符时抛出异常。
        /// </summary>
        public bool ShouldFail { get; set; } = true;

        /// <inheritdoc />
        public override char OpeningIdentifier => ShouldFail
            ? throw new InvalidOperationException("Identifier rendering failed.")
            : base.OpeningIdentifier;
    }

    /// <summary>
    /// 用于验证实体 Insert 列原子性的测试实体。
    /// </summary>
    private sealed class InsertColumnAtomicitySample
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }
}