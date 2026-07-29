using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Update Mutation Builder 测试。
/// </summary>
public sealed class SqlUpdateBuilderTest
{
    /// <summary>
    /// 测试目的：Update Fluent API 应按 Update、Set、Where 顺序输出 SQL，并返回原 Builder。
    /// </summary>
    [Fact]
    public void Update_WhenSetAndWhereConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        builder.MutationContext.ParameterManager.Add("@_p_1", 7);

        // Act
        var result = builder.Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing")
            .Where(new EqualCondition("[Id]", "@_p_1"));

        // Assert
        Assert.Same(builder, result);
        Assert.Equal("Update [samples] Set [Name] = @_p_0 Where [Id]=@_p_1", builder.ToSql());
        Assert.Equal(2, builder.GetParameters().Count);
    }

    /// <summary>
    /// 测试目的：未显式允许时，无 Where 的 Update 必须被拒绝。
    /// </summary>
    [Fact]
    public void ToSql_WhenWhereIsMissingAndAllRowsNotAllowed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("拒绝执行无条件 Update 操作。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Clear 后 Builder 应保留共享配置并清空所有 Mutation 状态。
    /// </summary>
    [Fact]
    public void Clear_WhenBuilderHasState_ShouldAllowReuseWithoutPreviousState()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing")
            .AllowAllRows();

        // Act
        builder.Clear();
        builder.Update(new SqlTableReference { TableName = "samples" }).Set("Name", "Framework").AllowAllRows();

        // Assert
        Assert.Equal("Update [samples] Set [Name] = @_p_0", builder.ToSql());
        Assert.Single(builder.GetParameters());
    }
}