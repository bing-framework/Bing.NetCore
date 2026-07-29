using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Delete Mutation Builder 测试。
/// </summary>
public sealed class SqlDeleteBuilderTest
{
    /// <summary>
    /// 测试目的：未显式允许时，无 Where 的 Delete 必须被拒绝。
    /// </summary>
    [Fact]
    public void ToSql_WhenWhereIsMissingAndAllRowsNotAllowed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples" });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("拒绝执行无条件 Delete 操作。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Delete 允许全表时应输出无 Where SQL，Where Fluent 应返回原对象。
    /// </summary>
    [Fact]
    public void DeleteFrom_WhenAllRowsAllowed_ShouldRenderExpectedSql()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var result = builder.DeleteFrom(new SqlTableReference { TableName = "samples" }).AllowAllRows();

        // Assert
        Assert.Same(builder, result);
        Assert.Equal("Delete From [samples]", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：Delete Where 应复用标准 Condition 组合模型。
    /// </summary>
    [Fact]
    public void Where_WhenMultipleConditionsConfigured_ShouldComposeWithAnd()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples" });

        // Act
        builder.Where(new EqualCondition("[Id]", "@_p_0"))
            .Where(new EqualCondition("[TenantId]", "@_p_1"));

        // Assert
        Assert.Equal("Delete From [samples] Where [Id]=@_p_0 And [TenantId]=@_p_1", builder.ToSql());
    }
}