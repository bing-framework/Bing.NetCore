using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSQL 实体写入 SQL 生成器测试。
/// </summary>
public sealed class PostgreSqlMutationBuilderTest
{
    /// <summary>
    /// 测试目的：PostgreSQL 插入应使用双引号标识符和标准参数前缀。
    /// </summary>
    [Fact]
    public void Insert_WhenMappedEntityIsProvided_ShouldRenderPostgreSqlSql()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(PostgreSqlSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var command = builder.Insert(new MutationSample { Name = "Bing" });

        // Assert
        Assert.Equal("Insert Into \"samples\" (\"Name\") Values (@_p_0)", command.Sql);
        Assert.Equal("@_p_0", Assert.Single(command.Parameters).Name);
    }

    /// <summary>
    /// 测试目的：PostgreSQL 优化批量 Update 应使用 UPDATE FROM VALUES，且更新值、主键与并发值必须按行固定排序。
    /// </summary>
    [Fact]
    public void Render_WhenMappedEntitiesAreProvided_ShouldRenderUpdateFromValuesWithPairedConcurrencyValues()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(PostgreSqlSqlProvider.Instance,
            new SqlBuilderServices());
        var context = ((ISqlBatchUpdateRenderContextBuilder)builder).CreateUpdateRenderContext(new[]
        {
            new UpdateMutationSample { Id = 1, Name = "first", Version = "v1" },
            new UpdateMutationSample { Id = 2, Name = "second", Version = "v2" }
        }, new SqlUpdateOptions { IncludeProperties = new[] { nameof(UpdateMutationSample.Name) } });

        // Act
        var command = new PostgreSqlBatchUpdateRenderer().Render(context);

        // Assert
        Assert.Equal("Update \"samples\" As t Set \"Name\" = v.\"__mutation_u_0\" From (Values " +
                 "(@_p_0, @_p_1, @_p_2), (@_p_3, @_p_4, @_p_5)) As v(\"__mutation_u_0\", \"__mutation_k_0\", \"__mutation_c_0\") " +
                 "Where t.\"Id\" = v.\"__mutation_k_0\" And t.\"Version\" = v.\"__mutation_c_0\"", command.Sql);
        Assert.Equal(new object[] { "first", 1, "v1", "second", 2, "v2" },
            command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：物理更新列与旧内部条件别名重名时，优化批量 Update 仍必须生成唯一的 Values 别名。
    /// </summary>
    [Fact]
    public void Render_WhenPhysicalColumnMatchesLegacyConditionAlias_ShouldRenderDistinctValuesAliases()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(PostgreSqlSqlProvider.Instance,
            new SqlBuilderServices());
        var context = ((ISqlBatchUpdateRenderContextBuilder)builder).CreateUpdateRenderContext(new[]
        {
            new AliasCollisionMutationSample { Id = 1, Value = "updated" }
        }, new SqlUpdateOptions { IncludeProperties = new[] { nameof(AliasCollisionMutationSample.Value) } });

        // Act
        var command = new PostgreSqlBatchUpdateRenderer().Render(context);

        // Assert
        Assert.Equal("Update \"alias_collision_samples\" As t Set \"__key_Id\" = v.\"__mutation_u_0\" From " +
                     "(Values (@_p_0, @_p_1)) As v(\"__mutation_u_0\", \"__mutation_k_0\") " +
                     "Where t.\"Id\" = v.\"__mutation_k_0\"", command.Sql);
        Assert.Equal(new object[] { "updated", 1 }, command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// PostgreSQL 样例实体。
    /// </summary>
    [Table("samples")]
    private sealed class MutationSample
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// PostgreSQL 批量 Update 样例实体。
    /// </summary>
    [Table("samples")]
    private sealed class UpdateMutationSample
    {
        /// <summary>主键。</summary>
        [Key]
        public int Id { get; set; }

        /// <summary>名称。</summary>
        public string Name { get; set; }

        /// <summary>并发令牌。</summary>
        [ConcurrencyCheck]
        public string Version { get; set; }
    }

    /// <summary>
    /// 映射到具有内部保留样式列名的测试表。
    /// </summary>
    [Table("alias_collision_samples")]
    private sealed class AliasCollisionMutationSample
    {
        /// <summary>主键。</summary>
        [Key]
        public int Id { get; set; }

        /// <summary>更新值。</summary>
        [Column("__key_Id")]
        public string Value { get; set; }
    }
}