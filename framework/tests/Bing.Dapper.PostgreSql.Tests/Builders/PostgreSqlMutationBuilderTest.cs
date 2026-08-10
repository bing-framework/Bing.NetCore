using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Metadata;

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
    /// 测试目的：PostgreSQL 统一 Builder 应渲染结构化 Update From，并保留参数化条件的参数顺序。
    /// </summary>
    [Fact]
    public void UpdateFrom_WhenUnifiedBuilderIsConfigured_ShouldRenderPostgreSqlSql()
    {
        // Arrange
        var builder = new PostgreSqlBuilder()
            .Update(new SqlTableReference { Schema = "public", TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { Schema = "public", TableName = "sample_updates", Alias = "s" })
            .SetFrom("Name", "Name")
            .Set("Version", 2)
            .WhereFrom("Id", "Id");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Update \"public\".\"samples\" As \"t\" Set \"Name\" = \"s\".\"Name\", \"Version\" = @_p_0 " +
                     "From \"public\".\"sample_updates\" As \"s\" Where \"t\".\"Id\"=\"s\".\"Id\"",
            sql);
        Assert.Equal(2, builder.GetParams()["@_p_0"]);
        Assert.True(PostgreSqlSqlProvider.Instance.Profile.Mutation.SupportsUpdateFrom);
    }

    /// <summary>
    /// 测试目的：PostgreSQL 统一 Builder 应渲染结构化 Delete Using 和带别名的目标表。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenUnifiedBuilderIsConfigured_ShouldRenderPostgreSqlSql()
    {
        // Arrange
        var builder = new PostgreSqlBuilder()
            .DeleteFrom(new SqlTableReference { Schema = "public", TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { Schema = "public", TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Delete From \"public\".\"samples\" As \"t\" Using \"public\".\"sample_deletes\" As \"s\" " +
                     "Where \"t\".\"Id\"=\"s\".\"Id\"", sql);
        Assert.True(PostgreSqlSqlProvider.Instance.Profile.Mutation.SupportsDeleteUsing);
    }

    /// <summary>
    /// 测试目的：PostgreSQL Insert Values 应在语句尾部渲染结构化 Returning 投影。
    /// </summary>
    [Fact]
    public void Returning_WhenInsertValuesIsConfigured_ShouldRenderPostgreSqlSql()
    {
        // Arrange
        var builder = new PostgreSqlBuilder()
            .InsertInto(new SqlTableReference { Schema = "public", TableName = "samples" })
            .Columns("Name")
            .Values("Bing")
            .Returning("Id", "Name");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into \"public\".\"samples\" (\"Name\") Values (@_p_0) Returning \"Id\", \"Name\"", sql);
    }

    /// <summary>
    /// 测试目的：PostgreSQL Insert Select 应在完整查询子句之后渲染 Returning。
    /// </summary>
    [Fact]
    public void Returning_WhenInsertSelectIsConfigured_ShouldRenderAfterQuery()
    {
        // Arrange
        ISqlBuilder builder = new PostgreSqlBuilder();
        builder.InsertInto(new SqlTableReference { Schema = "public", TableName = "archive_samples" })
            .Columns("Id", "Name")
            .Select("Id", "Name")
            .From("samples")
            .Returning("Id");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into \"public\".\"archive_samples\" (\"Id\", \"Name\") \r\n" +
                     "Select \"Id\",\"Name\" \r\nFrom \"samples\" Returning \"Id\"", sql);
    }

    /// <summary>
    /// 测试目的：Update From Returning 应限定目标表别名，避免来源表同名列歧义。
    /// </summary>
    [Fact]
    public void Returning_WhenUpdateFromIsConfigured_ShouldQualifyTargetColumns()
    {
        // Arrange
        var builder = new PostgreSqlBuilder()
            .Update(new SqlTableReference { Schema = "public", TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { Schema = "public", TableName = "sample_updates", Alias = "s" })
            .SetFrom("Name", "Name")
            .WhereFrom("Id", "Id")
            .Returning("Id", "Name");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Update \"public\".\"samples\" As \"t\" Set \"Name\" = \"s\".\"Name\" " +
                     "From \"public\".\"sample_updates\" As \"s\" Where \"t\".\"Id\"=\"s\".\"Id\" " +
                     "Returning \"t\".\"Id\", \"t\".\"Name\"", sql);
    }

    /// <summary>
    /// 测试目的：Delete Using Returning 应返回删除前的目标表字段并保持 Clause 顺序。
    /// </summary>
    [Fact]
    public void Returning_WhenDeleteUsingIsConfigured_ShouldQualifyTargetColumns()
    {
        // Arrange
        var builder = new PostgreSqlBuilder()
            .DeleteFrom(new SqlTableReference { Schema = "public", TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { Schema = "public", TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id")
            .Returning("Id");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Delete From \"public\".\"samples\" As \"t\" Using \"public\".\"sample_deletes\" As \"s\" " +
                     "Where \"t\".\"Id\"=\"s\".\"Id\" Returning \"t\".\"Id\"", sql);
    }

    /// <summary>
    /// 测试目的：实体 Returning 投影应使用物理列并输出 CLR 属性别名，供 Dapper 稳定物化。
    /// </summary>
    [Fact]
    public void Returning_WhenMappedProjectionIsConfigured_ShouldRenderPhysicalColumnsWithClrAliases()
    {
        // Arrange
        ISqlBuilder builder = new PostgreSqlBuilder();
        builder.Update(new SqlTableReference { Schema = "public", TableName = "returning_samples" })
            .Set("sample_name", "Bing")
            .Where("sample_id", 7)
            .Returning<ReturningMutationSample>(item => new { item.Id, item.Name });

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Update \"public\".\"returning_samples\" Set \"sample_name\" = @_p_0 " +
                     "Where \"sample_id\"=@_p_1 Returning \"sample_id\" As \"Id\", \"sample_name\" As \"Name\"", sql);
        Assert.True(PostgreSqlSqlProvider.Instance.Profile.Mutation.SupportsReturning);
    }

    /// <summary>
    /// 测试目的：Returning 投影应随 Clone 独立复制，并在来源 Builder Clear 后完整移除。
    /// </summary>
    [Fact]
    public void Returning_WhenBuilderIsClonedAndCleared_ShouldKeepInstancesIndependent()
    {
        // Arrange
        ISqlBuilder source = new PostgreSqlBuilder();
        source.DeleteFrom(new SqlTableReference { Schema = "public", TableName = "samples" })
            .Where("Id", 7)
            .Returning("Id");

        // Act
        var clone = source.Clone();
        source.Clear();

        // Assert
        Assert.Equal("Delete From \"public\".\"samples\" Where \"Id\"=@_p_0 Returning \"Id\"", clone.ToSql());
        Assert.Equal(SqlOperationKind.None, source.OperationKind);
    }

    /// <summary>
    /// 测试目的：Returning 不接受通配符或表达式，避免扩大返回形状和 Raw SQL 注入面。
    /// </summary>
    [Theory]
    [InlineData("*")]
    [InlineData("Id,Name")]
    [InlineData("Count(Id)")]
    public void Returning_WhenColumnIsNotSingleIdentifier_ShouldThrowArgumentException(string column)
    {
        // Arrange
        var builder = new PostgreSqlBuilder()
            .DeleteFrom(new SqlTableReference { Schema = "public", TableName = "samples" })
            .AllowAllRows();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.Returning(column));

        // Assert
        Assert.Equal("columns", exception.ParamName);
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

    /// <summary>
    /// Returning 实体映射样例。
    /// </summary>
    [Table("returning_samples", Schema = "public")]
    private sealed class ReturningMutationSample
    {
        /// <summary>主键。</summary>
        [Key]
        [Column("sample_id")]
        public int Id { get; set; }

        /// <summary>名称。</summary>
        [Column("sample_name")]
        public string Name { get; set; }
    }
}