using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

    /// <summary>
    /// 测试目的：强类型 Delete Where 应通过实体映射创建参数化物理列条件，不能拼接调用方输入。
    /// </summary>
    [Fact]
    public void DeleteFrom_WhenTypedWhereConfigured_ShouldRenderMappedParameterizedSql()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        builder.DeleteFrom<TypedDeleteSample>()
            .Where<TypedDeleteSample, int>(item => item.Id, 7);

        // Assert
        var command = builder.BuildCommand();
        Assert.Equal("Delete From [typed_delete_samples] Where [Id]=@_p_0", command.Sql);
        Assert.Equal(new object[] { 7 }, command.Parameters.Select(item => item.Value));
    }

    /// <summary>
    /// 测试目的：Delete Using 应按目标表、来源表和结构化列条件的固定顺序输出完整 SQL。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenStructuredTablesAreConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Delete From [samples] As [t] Using [sample_deletes] As [s] Where [t].[Id]=[s].[Id]", sql);
    }

    /// <summary>
    /// 测试目的：Delete Using 不能替代 Delete 的无条件写保护。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenWhereIsMissing_ShouldRejectUnconditionalDelete()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("拒绝执行无条件 Delete 操作。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Delete Using 来源表必须具有别名，确保结构化列引用可唯一定位。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenSourceAliasIsMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes" })
            .AllowAllRows();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Delete Using 来源表必须指定别名。", exception.Message);
    }

    /// <summary>
    /// 测试目的：WhereUsing 必须要求 Delete 目标表别名，失败时不得写入 Where。
    /// </summary>
    [Fact]
    public void WhereUsing_WhenTargetAliasIsMissing_ShouldThrowWithoutChangingWhere()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.WhereUsing("Id", "Id"));

        // Assert
        Assert.Equal("WhereUsing 要求 Delete 目标表指定别名。", exception.Message);
        Assert.True(builder.WhereClause.IsEmpty);
    }

    /// <summary>
    /// 测试目的：WhereUsing 应拒绝表达式和限定列名，避免调用方绕过结构化标识符边界。
    /// </summary>
    [Theory]
    [InlineData("t.Id")]
    [InlineData("Id = 1")]
    [InlineData("Id;Delete")]
    public void WhereUsing_WhenColumnIsNotSingleIdentifier_ShouldThrowArgumentException(string column)
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" });

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.WhereUsing(column, "Id"));

        // Assert
        Assert.Equal("列名必须是单段结构化标识符。 (Parameter 'targetColumn')", exception.Message);
    }

    /// <summary>
    /// 测试目的：Clone 应保留独立 Delete Using 状态，Clear 不得影响副本。
    /// </summary>
    [Fact]
    public void Clone_WhenDeleteUsingIsConfigured_ShouldRemainIndependentAfterClear()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id");

        // Act
        var clone = builder.Clone();
        builder.Clear();

        // Assert
        Assert.Equal("Delete From [samples] As [t] Using [sample_deletes] As [s] Where [t].[Id]=[s].[Id]",
            clone.ToSql());
        Assert.Null(builder.DeleteUsingClause.Table);
    }

    /// <summary>
    /// 强类型 Delete 条件的映射实体。
    /// </summary>
    [Table("typed_delete_samples")]
    private sealed class TypedDeleteSample
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }
    }
}