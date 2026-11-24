using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// 实体 Returning 投影布局测试。
/// </summary>
public sealed class SqlMutationReturningProjectionTest
{
    /// <summary>
    /// 测试目的：匿名投影别名应保留，并按投影顺序保留重复的源列。
    /// </summary>
    [Fact]
    public void Returning_WhenAnonymousProjectionHasAliases_ShouldPreserveAliasesOrderAndDuplicates()
    {
        var builder = CreateSqliteUpdateBuilder()
            .Returning<ReturningEntity>(item => new
            {
                Display = item.Name,
                item.Id,
                RepeatedDisplay = item.Name
            });

        Assert.Equal("Update `mapped_samples` Set `sample_name` = @_p_0 Where `sample_id`=@_p_1 " +
                     "Returning `sample_name` As `Display`, `sample_id` As `Id`, " +
                     "`sample_name` As `RepeatedDisplay`", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：DTO 成员初始化应按目标 DTO 成员生成返回别名。
    /// </summary>
    [Fact]
    public void Returning_WhenDtoIsInitialized_ShouldUseDtoMemberNames()
    {
        var builder = CreatePostgreSqlUpdateBuilder()
            .Returning<ReturningEntity>(item => new ReturningDto
            {
                Id = item.Id,
                Display = item.Name
            });

        Assert.Equal("Update \"mapped_samples\" Set \"sample_name\" = @_p_0 " +
                     "Where \"sample_id\"=@_p_1 Returning \"sample_id\" As \"Id\", " +
                     "\"sample_name\" As \"Display\"", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：单属性 Returning 继续使用实体 CLR 属性名作为别名。
    /// </summary>
    [Fact]
    public void Returning_WhenSinglePropertyIsSelected_ShouldKeepClrPropertyAlias()
    {
        var builder = CreateSqliteUpdateBuilder()
            .Returning<ReturningEntity>(item => item.Id);

        Assert.Equal("Update `mapped_samples` Set `sample_name` = @_p_0 Where `sample_id`=@_p_1 " +
                     "Returning `sample_id` As `Id`", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：SQL Server 应依据写入类型使用 Inserted 或 Deleted 并保留投影别名。
    /// </summary>
    [Fact]
    public void Returning_WhenSqlServerMutationIsConfigured_ShouldUseInsertedAndDeleted()
    {
        var services = SqlBuilderServices.CreateDefault();
        var insert = new SqlInsertBuilder(SqlServerSqlProvider.Instance, services)
            .InsertInto("mapped_samples")
            .Columns("sample_name")
            .Values("Bing");
        insert.Returning<ReturningEntity>(item => new { Display = item.Name });

        var update = new SqlUpdateBuilder(SqlServerSqlProvider.Instance, services)
            .Update(new SqlTableReference { TableName = "mapped_samples" })
            .Set("sample_name", "Bing")
            .Where("sample_id", 7);
        update.Returning<ReturningEntity>(item => new { Display = item.Name });

        var delete = new SqlDeleteBuilder(SqlServerSqlProvider.Instance, services)
            .DeleteFrom(new SqlTableReference { TableName = "mapped_samples" })
            .Where("sample_id", 7);
        delete.Returning<ReturningEntity>(item => new { Display = item.Name });

        Assert.Equal("Insert Into [mapped_samples] ([sample_name]) Output [Inserted].[sample_name] " +
                     "As [Display] Values (@_p_0)", insert.ToSql());
        Assert.Equal("Update [mapped_samples] Set [sample_name] = @_p_0 " +
                     "Output [Inserted].[sample_name] As [Display] Where [sample_id]=@_p_1", update.ToSql());
        Assert.Equal("Delete From [mapped_samples] Output [Deleted].[sample_name] As [Display] " +
                     "Where [sample_id]=@_p_0", delete.ToSql());
    }

    /// <summary>
    /// 测试目的：PostgreSQL 与 SQLite 均应将源列映射和 DTO 别名渲染为完整 Returning SQL。
    /// </summary>
    [Fact]
    public void Returning_WhenPostgreSqlOrSqliteIsConfigured_ShouldRenderCompleteSql()
    {
        var sqlite = CreateSqliteUpdateBuilder()
            .Returning<ReturningEntity>(item => new { Display = item.Name });
        var postgreSql = CreatePostgreSqlUpdateBuilder()
            .Returning<ReturningEntity>(item => new { Display = item.Name });

        Assert.Equal("Update `mapped_samples` Set `sample_name` = @_p_0 Where `sample_id`=@_p_1 " +
                     "Returning `sample_name` As `Display`", sqlite.ToSql());
        Assert.Equal("Update \"mapped_samples\" Set \"sample_name\" = @_p_0 " +
                     "Where \"sample_id\"=@_p_1 Returning \"sample_name\" As \"Display\"", postgreSql.ToSql());
    }

    /// <summary>
    /// 测试目的：不支持 Returning 的 MySQL Provider 应继续拒绝该子句。
    /// </summary>
    [Fact]
    public void Returning_WhenProviderDoesNotSupportIt_ShouldThrowNotSupportedException()
    {
        var builder = new SqlInsertBuilder(MySqlSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .InsertInto("mapped_samples")
            .Columns("sample_name")
            .Values("Bing");
        builder.Returning<ReturningEntity>(item => new { Display = item.Name });

        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        Assert.Contains("不支持 Returning", exception.Message);
    }

    /// <summary>
    /// 测试目的：不支持的投影形状必须在追加子句前失败，后续合法投影仍可配置。
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidProjections))]
    public void Returning_WhenProjectionShapeIsUnsupported_ShouldNotChangeClause(
        LambdaExpression projection)
    {
        var builder = CreateSqliteUpdateBuilder();

        Assert.Throws<ArgumentException>(() => builder.Returning<ReturningEntity>(
            (Expression<Func<ReturningEntity, object>>)projection));

        builder.Returning<ReturningEntity>(item => new { Display = item.Name });
        Assert.Equal("Update `mapped_samples` Set `sample_name` = @_p_0 Where `sample_id`=@_p_1 " +
                     "Returning `sample_name` As `Display`", builder.ToSql());
    }

    /// <summary>
    /// 提供不支持的投影表达式形状。
    /// </summary>
    public static TheoryData<LambdaExpression> InvalidProjections => new()
    {
        (Expression<Func<ReturningEntity, object>>)(item => new { item.Id, Computed = item.Id + 1 }),
        (Expression<Func<ReturningEntity, object>>)(item => new { item.Id, NestedId = item.Details.Id }),
        (Expression<Func<ReturningEntity, object>>)(item => new ConstructorReturningDto(item.Id)),
        (Expression<Func<ReturningEntity, object>>)(item => new[] { item.Id }),
        CreateCollectionBindingProjection()
    };

    private static LambdaExpression CreateCollectionBindingProjection()
    {
        var parameter = Expression.Parameter(typeof(ReturningEntity), "item");
        var valuesProperty = typeof(CollectionReturningDto).GetProperty(nameof(CollectionReturningDto.Values));
        var addMethod = typeof(List<int>).GetMethod(nameof(List<int>.Add));
        var projection = Expression.MemberInit(Expression.New(typeof(CollectionReturningDto)),
            Expression.ListBind(valuesProperty,
                Expression.ElementInit(addMethod, Expression.Property(parameter, nameof(ReturningEntity.Id)))));

        return Expression.Lambda<Func<ReturningEntity, object>>(
            Expression.Convert(projection, typeof(object)), parameter);
    }

    private static ISqlUpdateBuilder CreateSqliteUpdateBuilder() =>
        new SqlUpdateBuilder(SqliteSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .Update(new SqlTableReference { TableName = "mapped_samples" })
            .Set("sample_name", "Bing")
            .Where("sample_id", 7);

    private static ISqlUpdateBuilder CreatePostgreSqlUpdateBuilder() =>
        new SqlUpdateBuilder(PostgreSqlSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .Update(new SqlTableReference { TableName = "mapped_samples" })
            .Set("sample_name", "Bing")
            .Where("sample_id", 7);

    [Table("mapped_samples")]
    private sealed class ReturningEntity
    {
        [Column("sample_id")]
        public int Id { get; set; }

        [Column("sample_name")]
        public string Name { get; set; }

        [NotMapped]
        public ReturningDetails Details { get; set; }
    }

    private sealed class ReturningDetails
    {
        public int Id { get; set; }
    }

    private sealed class ReturningDto
    {
        public int Id { get; set; }

        public string Display { get; set; }
    }

    private sealed class ConstructorReturningDto
    {
        public ConstructorReturningDto(int id) => Id = id;

        public int Id { get; }
    }

    private sealed class CollectionReturningDto
    {
        public List<int> Values { get; } = new();
    }
}
