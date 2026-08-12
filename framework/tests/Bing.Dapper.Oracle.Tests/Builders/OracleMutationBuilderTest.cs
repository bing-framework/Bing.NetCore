using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle 实体写入 SQL 生成器测试。
/// </summary>
public sealed class OracleMutationBuilderTest
{
    /// <summary>
    /// 测试目的：Oracle Provider 必须显式声明不支持标准多行 Values，供 Auto 批处理安全回退。
    /// </summary>
    [Fact]
    public void Capabilities_WhenResolved_ShouldDisableStandardMultiRowValues()
    {
        // Arrange / Act
        var capabilities = OracleSqlProvider.Instance.Profile.Mutation;

        // Assert
        Assert.False(capabilities.SupportsMultiRowValues);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, OracleSqlProvider.Instance.Profile.Query.Except);
    }

    /// <summary>
    /// 测试目的：Oracle 插入应使用双引号标识符和无前缀的执行参数名称。
    /// </summary>
    [Fact]
    public void Insert_WhenMappedEntityIsProvided_ShouldRenderOracleSql()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(OracleSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var command = builder.Insert(new MutationSample { Name = "Bing" });

        // Assert
        Assert.Equal("Insert Into \"samples\" (\"Name\") Values (p_0)", command.Sql);
        Assert.Equal(":p_0", Assert.Single(command.Parameters).Name);
    }

    /// <summary>
    /// 测试目的：Oracle 不支持标准多行 Values 语法时，组合 Insert 应在 SQL 生成前拒绝。
    /// </summary>
    [Fact]
    public void InsertCombined_WhenMultipleEntitiesAreProvided_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(OracleSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.InsertCombined(new[]
        {
            new MutationSample { Name = "first" },
            new MutationSample { Name = "second" }
        }));

        // Assert
        Assert.Equal("Provider bing.oracle 不支持多行 Values。", exception.Message);
    }

    /// <summary>
    /// 测试 - Oracle 未声明 Update From 能力时，渲染必须明确拒绝。
    /// </summary>
    [Fact]
    public void UpdateFrom_WhenProviderDoesNotSupportIt_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new OracleBuilder()
            .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { TableName = "sample_updates", Alias = "s" })
            .SetFrom("Name", "Name")
            .WhereFrom("Id", "Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.oracle 不支持 Update From。", exception.Message);
        Assert.False(OracleSqlProvider.Instance.Profile.Mutation.SupportsUpdateFrom);
    }

    /// <summary>
    /// 测试 - Oracle 未声明 Delete Using 能力时，渲染必须明确拒绝。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenProviderDoesNotSupportIt_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new OracleBuilder()
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.oracle 不支持 Delete Using。", exception.Message);
        Assert.False(OracleSqlProvider.Instance.Profile.Mutation.SupportsDeleteUsing);
    }

    /// <summary>
    /// 测试 - Oracle 未声明 Returning 能力时，渲染必须明确拒绝。
    /// </summary>
    [Fact]
    public void Returning_WhenProviderDoesNotSupportIt_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new OracleBuilder()
            .InsertInto(new SqlTableReference { TableName = "samples" })
            .Columns("Name")
            .Values("Bing")
            .Returning("Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.oracle 不支持 Returning。", exception.Message);
        Assert.False(OracleSqlProvider.Instance.Profile.Mutation.SupportsReturning);
    }

    /// <summary>
    /// Oracle 样例实体。
    /// </summary>
    [Table("samples")]
    private sealed class MutationSample
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }
}