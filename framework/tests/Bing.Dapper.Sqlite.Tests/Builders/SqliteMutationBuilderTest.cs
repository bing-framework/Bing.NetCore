using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Mutations;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQLite 实体写入 SQL 生成器测试。
/// </summary>
public sealed class SqliteMutationBuilderTest
{
    /// <summary>
    /// 测试目的：插入、更新和删除应生成完整 SQLite SQL，且 Identity 列不得进入插入或更新列。
    /// </summary>
    [Fact]
    public void MutationBuilder_WhenMappedEntityIsProvided_ShouldRenderSqlAndParameters()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(SqliteSqlProvider.Instance, new SqlBuilderServices());
        var entity = new MutationSample { Id = 7, Name = "Bing", Secret = "v1" };

        // Act
        var insert = builder.Insert(entity);
        var update = builder.Update(entity, new SqlUpdateOptions<MutationSample>
        {
            IncludeProperties = new[] { nameof(MutationSample.Name) }
        }.Original(item => item.Secret, "v1"));
        var delete = builder.Delete(entity, new SqlDeleteOptions<MutationSample>()
            .Original(item => item.Secret, "v1"));

        // Assert
        Assert.Equal("Insert Into `samples` (`Name`, `Secret`) Values (@_p_0, @_p_1)", insert.Sql);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, insert.Parameters.Select(parameter => parameter.Name));
        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id` = @_p_1 And `Secret` = @_p_2", update.Sql);
        Assert.Equal("Delete From `samples` Where `Id` = @_p_0 And `Secret` = @_p_1", delete.Sql);
    }

    /// <summary>
    /// 测试目的：没有主键的实体更新或删除应在生成 SQL 前被拒绝，避免全表写入。
    /// </summary>
    [Fact]
    public void MutationBuilder_WhenEntityHasNoKey_ShouldRejectUnsafeUpdateAndDelete()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(SqliteSqlProvider.Instance, new SqlBuilderServices());
        var entity = new KeylessMutationSample { Name = "unsafe" };

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => builder.Update(entity));
        Assert.Throws<InvalidOperationException>(() => builder.Delete(entity));
    }

    /// <summary>
    /// 测试目的：仅声明并发令牌但未声明主键的实体不得执行 Update 或 Delete，避免并发列被误用为实体写入条件。
    /// </summary>
    [Fact]
    public void MutationBuilder_WhenEntityHasConcurrencyTokenButNoKey_ShouldRejectUpdateAndDelete()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(SqliteSqlProvider.Instance, new SqlBuilderServices());
        var entity = new KeylessConcurrencyMutationSample { Name = "unsafe", Secret = "v1" };

        // Act
        var updateException = Assert.Throws<InvalidOperationException>(() => builder.Update(entity));
        var deleteException = Assert.Throws<InvalidOperationException>(() => builder.Delete(entity));

        // Assert
        Assert.Equal("实体 KeylessConcurrencyMutationSample 没有主键，不能执行更新。", updateException.Message);
        Assert.Equal("实体 KeylessConcurrencyMutationSample 没有主键，不能执行删除。", deleteException.Message);
    }

    /// <summary>
    /// 映射到 SQLite 样例表的实体。
    /// </summary>
    [Table("samples")]
    private sealed class MutationSample
    {
        /// <summary>
        /// 数据库生成的主键。
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 并发令牌。
        /// </summary>
        [ConcurrencyCheck]
        public string Secret { get; set; }
    }

    /// <summary>
    /// 未定义主键的实体。
    /// </summary>
    [Table("samples")]
    private sealed class KeylessMutationSample
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 未定义主键但声明并发令牌的实体。
    /// </summary>
    [Table("samples")]
    private sealed class KeylessConcurrencyMutationSample
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 并发令牌。
        /// </summary>
        [ConcurrencyCheck]
        public string Secret { get; set; }
    }
}