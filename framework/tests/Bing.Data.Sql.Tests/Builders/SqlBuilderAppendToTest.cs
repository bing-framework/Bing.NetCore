using System.Text;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// SQL Builder AppendTo 合同测试。
/// </summary>
public class SqlBuilderAppendToTest
{
    /// <summary>
    /// 测试 - AppendTo 应输出与 ToSql 相同的 SQL。
    /// </summary>
    [Fact]
    public void AppendTo_WhenBuilderIsEmpty_ShouldRenderSameSqlAsToSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.CountColumn("u.Id", "UserCount", distinct: true).From("Users", "u").Where("u.Enabled", true);
        var result = new StringBuilder();

        // Act
        builder.AppendTo(result);

        // Assert
        Assert.Equal(builder.ToSql(), result.ToString());
    }

    /// <summary>
    /// 测试 - AppendTo 不应清空调用方已有内容。
    /// </summary>
    [Fact]
    public void AppendTo_WhenBuilderContainsPrefix_ShouldAppendWithoutClearing()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.Select("u.Id").From("Users", "u");
        var result = new StringBuilder("Prefix:");

        // Act
        builder.AppendTo(result);

        // Assert
        Assert.Equal($"Prefix:{builder.ToSql()}", result.ToString());
    }

    /// <summary>
    /// 测试 - AppendTo 重复调用应重复追加 SQL 而不重置输出。
    /// </summary>
    [Fact]
    public void AppendTo_WhenCalledTwice_ShouldAppendTwice()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.CountAll("Total").From("Users");
        var result = new StringBuilder();
        var sql = builder.ToSql();

        // Act
        builder.AppendTo(result);
        builder.AppendTo(result);

        // Assert
        Assert.Equal($"{sql}{sql}", result.ToString());
    }

    /// <summary>
    /// 测试 - AppendTo 传入 null 时应抛出参数异常。
    /// </summary>
    [Fact]
    public void AppendTo_WhenArgumentIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => builder.AppendTo(null));

        // Assert
        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// 测试 - AppendTo 应保留子查询 SQL 与参数合并行为。
    /// </summary>
    [Fact]
    public void AppendTo_WhenSubqueryHasParameters_ShouldMergeParametersAndRenderExpectedSql()
    {
        // Arrange
        const string expected = "Select (Select Count(Distinct [Id]) As [Count] \r\nFrom [Audit] \r\nWhere [Enabled]=@_p_0) As [AuditCount] \r\nFrom [Users] \r\nWhere [Enabled]=@_p_1";
        var builder = new TestSqlBuilder();
        var subquery = builder.New().CountColumn("Id", "Count", distinct: true).From("Audit").Where("Enabled", true);
        builder.Select(subquery, "AuditCount").From("Users").Where("Enabled", false);
        var result = new StringBuilder();

        // Act
        builder.AppendTo(result);

        // Assert
        Assert.Equal(expected, result.ToString());
        Assert.Equal(2, builder.GetParams().Count);
        Assert.Equal(true, builder.GetParam("@_p_0"));
        Assert.Equal(false, builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：动态全局过滤启用时，AppendTo 必须与 ToSql 使用相同的独立渲染快照。
    /// </summary>
    [Fact]
    public void AppendTo_WhenGlobalFilterIsEnabled_ShouldRenderSameSqlAsToSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.Select<Sample5>(item => item.StringValue).From<Sample5>("s");
        var result = new StringBuilder();

        // Act
        builder.AppendTo(result);

        // Assert
        Assert.Equal(builder.ToSql(), result.ToString());
        Assert.Empty(builder.GetSqlParams());
    }

    /// <summary>
    /// 测试目的：Mutation 延迟验证失败时，AppendTo 不得向调用方缓冲区保留部分 SQL。
    /// </summary>
    [Fact]
    public void AppendTo_WhenReturningIsUnsupported_ShouldKeepCallerBufferUnchanged()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.UpdateClause.UpdateTable(new SqlTableReference { TableName = "samples" });
        builder.SetClause.Set("Name", "Bing");
        builder.ParameterManager.Add("@id", 7);
        ((IMutationWhereClauseAccessor)builder).WhereClause.And(new EqualCondition("[Id]", "@id"));
        builder.Returning("Id");
        var result = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.AppendTo(result));

        // Assert
        Assert.Equal("Provider test.sqlserver 不支持 Returning。", exception.Message);
        Assert.Equal("Prefix:", result.ToString());
    }
}