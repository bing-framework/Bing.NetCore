using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySQL 实体写入 SQL 生成器测试。
/// </summary>
public sealed class MySqlMutationBuilderTest
{
    /// <summary>
    /// 测试目的：MySQL 插入应使用反引号标识符和标准参数前缀。
    /// </summary>
    [Fact]
    public void Insert_WhenMappedEntityIsProvided_ShouldRenderMySqlSql()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(MySqlSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var command = builder.Insert(new MutationSample { Name = "Bing" });

        // Assert
        Assert.Equal("Insert Into `samples` (`Name`) Values (@_p_0)", command.Sql);
        Assert.Equal("@_p_0", Assert.Single(command.Parameters).Name);
    }

    /// <summary>
    /// 测试 - MySQL 未声明 Update From 能力时，渲染必须明确拒绝。
    /// </summary>
    [Fact]
    public void UpdateFrom_WhenProviderDoesNotSupportIt_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new MySqlBuilder()
            .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { TableName = "sample_updates", Alias = "s" })
            .SetFrom("Name", "Name")
            .WhereFrom("Id", "Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.mysql 不支持 Update From。", exception.Message);
        Assert.False(MySqlSqlProvider.Instance.Profile.Mutation.SupportsUpdateFrom);
    }

    /// <summary>
    /// 测试 - MySQL 未声明 Delete Using 能力时，渲染必须明确拒绝。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenProviderDoesNotSupportIt_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new MySqlBuilder()
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.mysql 不支持 Delete Using。", exception.Message);
        Assert.False(MySqlSqlProvider.Instance.Profile.Mutation.SupportsDeleteUsing);
    }

    /// <summary>
    /// 测试 - MySQL 未声明 Returning 能力时，渲染必须明确拒绝。
    /// </summary>
    [Fact]
    public void Returning_WhenProviderDoesNotSupportIt_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new MySqlBuilder()
            .InsertInto(new SqlTableReference { TableName = "samples" })
            .Columns("Name")
            .Values("Bing")
            .Returning("Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.mysql 不支持 Returning。", exception.Message);
        Assert.False(MySqlSqlProvider.Instance.Profile.Mutation.SupportsReturning);
    }

    /// <summary>
    /// MySQL 样例实体。
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