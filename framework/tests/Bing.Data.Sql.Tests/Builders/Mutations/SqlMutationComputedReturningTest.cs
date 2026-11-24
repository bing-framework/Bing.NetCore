using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Mutation Returning 算术投影测试。
/// </summary>
public sealed class SqlMutationComputedReturningTest
{
    /// <summary>
    /// 提供基础数值和可空数值的四则运算投影及完整 Returning 列 SQL。
    /// </summary>
    public static TheoryData<Expression<Func<ComputedReturningEntity, object>>, string> NumericProjectionCases => new()
    {
        {
            row => new
            {
                Added = row.Int32 + row.Int32,
                Subtracted = (row.Int32 - row.Int32) / row.Int32,
                Multiplied = (row.Int32 * row.Int32) + row.Int32,
                Divided = row.Int32 / row.Int32
            },
            "(`sample_int32` + `sample_int32`) As `Added`, " +
            "((`sample_int32` - `sample_int32`) / `sample_int32`) As `Subtracted`, " +
            "((`sample_int32` * `sample_int32`) + `sample_int32`) As `Multiplied`, " +
            "(`sample_int32` / `sample_int32`) As `Divided`"
        },
        {
            row => new
            {
                Added = row.Int64 + row.Int64,
                Subtracted = (row.Int64 - row.Int64) / row.Int64,
                Multiplied = (row.Int64 * row.Int64) + row.Int64,
                Divided = row.Int64 / row.Int64
            },
            "(`sample_int64` + `sample_int64`) As `Added`, " +
            "((`sample_int64` - `sample_int64`) / `sample_int64`) As `Subtracted`, " +
            "((`sample_int64` * `sample_int64`) + `sample_int64`) As `Multiplied`, " +
            "(`sample_int64` / `sample_int64`) As `Divided`"
        },
        {
            row => new
            {
                Added = row.Decimal + row.Decimal,
                Subtracted = (row.Decimal - row.Decimal) / row.Decimal,
                Multiplied = (row.Decimal * row.Decimal) + row.Decimal,
                Divided = row.Decimal / row.Decimal
            },
            "(`sample_decimal` + `sample_decimal`) As `Added`, " +
            "((`sample_decimal` - `sample_decimal`) / `sample_decimal`) As `Subtracted`, " +
            "((`sample_decimal` * `sample_decimal`) + `sample_decimal`) As `Multiplied`, " +
            "(`sample_decimal` / `sample_decimal`) As `Divided`"
        },
        {
            row => new
            {
                Added = row.Double + row.Double,
                Subtracted = (row.Double - row.Double) / row.Double,
                Multiplied = (row.Double * row.Double) + row.Double,
                Divided = row.Double / row.Double
            },
            "(`sample_double` + `sample_double`) As `Added`, " +
            "((`sample_double` - `sample_double`) / `sample_double`) As `Subtracted`, " +
            "((`sample_double` * `sample_double`) + `sample_double`) As `Multiplied`, " +
            "(`sample_double` / `sample_double`) As `Divided`"
        },
        {
            row => new
            {
                Added = row.Single + row.Single,
                Subtracted = (row.Single - row.Single) / row.Single,
                Multiplied = (row.Single * row.Single) + row.Single,
                Divided = row.Single / row.Single
            },
            "(`sample_single` + `sample_single`) As `Added`, " +
            "((`sample_single` - `sample_single`) / `sample_single`) As `Subtracted`, " +
            "((`sample_single` * `sample_single`) + `sample_single`) As `Multiplied`, " +
            "(`sample_single` / `sample_single`) As `Divided`"
        },
        {
            row => new
            {
                Added = row.NullableInt32 + row.NullableInt32,
                Subtracted = (row.NullableInt32 - row.NullableInt32) / row.NullableInt32,
                Multiplied = (row.NullableInt32 * row.NullableInt32) + row.NullableInt32,
                Divided = row.NullableInt32 / row.NullableInt32
            },
            "(`sample_nullable_int32` + `sample_nullable_int32`) As `Added`, " +
            "((`sample_nullable_int32` - `sample_nullable_int32`) / `sample_nullable_int32`) As `Subtracted`, " +
            "((`sample_nullable_int32` * `sample_nullable_int32`) + `sample_nullable_int32`) As `Multiplied`, " +
            "(`sample_nullable_int32` / `sample_nullable_int32`) As `Divided`"
        },
        {
            row => new
            {
                Added = row.NullableInt64 + row.NullableInt64,
                Subtracted = (row.NullableInt64 - row.NullableInt64) / row.NullableInt64,
                Multiplied = (row.NullableInt64 * row.NullableInt64) + row.NullableInt64,
                Divided = row.NullableInt64 / row.NullableInt64
            },
            "(`sample_nullable_int64` + `sample_nullable_int64`) As `Added`, " +
            "((`sample_nullable_int64` - `sample_nullable_int64`) / `sample_nullable_int64`) As `Subtracted`, " +
            "((`sample_nullable_int64` * `sample_nullable_int64`) + `sample_nullable_int64`) As `Multiplied`, " +
            "(`sample_nullable_int64` / `sample_nullable_int64`) As `Divided`"
        },
        {
            row => new
            {
                Added = row.NullableDecimal + row.NullableDecimal,
                Subtracted = (row.NullableDecimal - row.NullableDecimal) / row.NullableDecimal,
                Multiplied = (row.NullableDecimal * row.NullableDecimal) + row.NullableDecimal,
                Divided = row.NullableDecimal / row.NullableDecimal
            },
            "(`sample_nullable_decimal` + `sample_nullable_decimal`) As `Added`, " +
            "((`sample_nullable_decimal` - `sample_nullable_decimal`) / `sample_nullable_decimal`) As `Subtracted`, " +
            "((`sample_nullable_decimal` * `sample_nullable_decimal`) + `sample_nullable_decimal`) As `Multiplied`, " +
            "(`sample_nullable_decimal` / `sample_nullable_decimal`) As `Divided`"
        },
        {
            row => new
            {
                Added = row.NullableDouble + row.NullableDouble,
                Subtracted = (row.NullableDouble - row.NullableDouble) / row.NullableDouble,
                Multiplied = (row.NullableDouble * row.NullableDouble) + row.NullableDouble,
                Divided = row.NullableDouble / row.NullableDouble
            },
            "(`sample_nullable_double` + `sample_nullable_double`) As `Added`, " +
            "((`sample_nullable_double` - `sample_nullable_double`) / `sample_nullable_double`) As `Subtracted`, " +
            "((`sample_nullable_double` * `sample_nullable_double`) + `sample_nullable_double`) As `Multiplied`, " +
            "(`sample_nullable_double` / `sample_nullable_double`) As `Divided`"
        },
        {
            row => new
            {
                Added = row.NullableSingle + row.NullableSingle,
                Subtracted = (row.NullableSingle - row.NullableSingle) / row.NullableSingle,
                Multiplied = (row.NullableSingle * row.NullableSingle) + row.NullableSingle,
                Divided = row.NullableSingle / row.NullableSingle
            },
            "(`sample_nullable_single` + `sample_nullable_single`) As `Added`, " +
            "((`sample_nullable_single` - `sample_nullable_single`) / `sample_nullable_single`) As `Subtracted`, " +
            "((`sample_nullable_single` * `sample_nullable_single`) + `sample_nullable_single`) As `Multiplied`, " +
            "(`sample_nullable_single` / `sample_nullable_single`) As `Divided`"
        }
    };

    /// <summary>既有直接列转换只表达返回布局，不生成 SQL CAST；混合计算列时也保持该合同。</summary>
    public static TheoryData<Expression<Func<ComputedReturningEntity, object>>, string> DirectConversionProjectionCases => new()
    {
        { row => (long)row.Id, "`sample_id` As `Id`" },
        { row => new { Key = (long)row.Id }, "`sample_id` As `Key`" },
        { row => new ConvertedReturningDto { Key = row.Id }, "`sample_id` As `Key`" },
        { row => new { Key = (long)row.Id, Total = row.Id + row.Id },
            "`sample_id` As `Key`, (`sample_id` + `sample_id`) As `Total`" }
    };

    /// <summary>
    /// 提供 Returning 不应接受的表达式形状。
    /// </summary>
    public static TheoryData<Expression<Func<ComputedReturningEntity, object>>> UnsupportedProjections => new()
    {
        row => new { Total = row.Int32 + 1 },
        CreateCapturedProjection(),
        row => new { Total = row.Name + row.Name },
        row => new { Total = row.Name.Trim() },
        row => new { Total = SqlFn.Sum(row.Int32) },
        row => new { Total = (long)row.Int32 + row.Int64 },
        row => new { Total = (long)(row.Int32 + row.Int32) },
        row => (long)(row.Int32 + row.Int32),
        row => new { Total = (object)(long)row.Int32 },
        row => new { Total = checked(row.Int32 + row.Int32) },
        row => new { Total = row.Int32 > 0 ? row.Int32 : row.Int32 }
    };

    /// <summary>
    /// 测试目的：相同数值类型的直接列运算应完整渲染并保留原 mutation 参数。
    /// </summary>
    [Theory]
    [MemberData(nameof(NumericProjectionCases))]
    public void Returning_WhenNumericColumnsAreCombined_ShouldRenderFullyParenthesizedSqlAndKeepParameters(
        Expression<Func<ComputedReturningEntity, object>> projection, string expectedProjection)
    {
        var builder = CreateSqliteUpdateBuilder();
        builder.Returning(projection);

        Assert.Equal(ExpectedSqliteUpdate(expectedProjection), builder.ToSql());
        AssertMutationParameters(builder);
    }

    /// <summary>单层直接列转换与计算投影共存时，仍使用原有映射列布局。</summary>
    [Theory]
    [MemberData(nameof(DirectConversionProjectionCases))]
    public void Returning_WhenDirectColumnHasOneConversion_ShouldKeepExistingLayout(
        Expression<Func<ComputedReturningEntity, object>> projection, string expectedProjection)
    {
        var builder = CreateSqliteUpdateBuilder();
        builder.Returning(projection);

        Assert.Equal(ExpectedSqliteUpdate(expectedProjection), builder.ToSql());
        AssertMutationParameters(builder);
    }

    /// <summary>
    /// 测试目的：DTO 投影应保留直接列与计算列各自的成员别名及实体映射。
    /// </summary>
    [Fact]
    public void Returning_WhenDtoContainsDirectAndComputedColumns_ShouldUseMappedColumnsAndDtoAliases()
    {
        var builder = CreateSqliteUpdateBuilder();
        builder.Returning<ComputedReturningEntity>(row => new ComputedReturningDto
        {
            Id = row.Id,
            Total = (row.Id + row.Id) * row.Id
        });

        Assert.Equal(ExpectedSqliteUpdate(
            "`sample_id` As `Id`, ((`sample_id` + `sample_id`) * `sample_id`) As `Total`"), builder.ToSql());
        AssertMutationParameters(builder);
    }

    /// <summary>
    /// 测试目的：SQLite 与 PostgreSQL 应为匿名算术投影输出完整 Provider SQL。
    /// </summary>
    [Fact]
    public void Returning_WhenSqliteOrPostgreSqlIsConfigured_ShouldRenderCompleteComputedSql()
    {
        var sqlite = CreateSqliteUpdateBuilder();
        sqlite.Returning<ComputedReturningEntity>(row => new { Total = (row.Id + row.Id) * row.Id });
        var postgreSql = CreatePostgreSqlUpdateBuilder();
        postgreSql.Returning<ComputedReturningEntity>(row => new { Total = (row.Id + row.Id) * row.Id });

        Assert.Equal(
            "Update `mapped_samples` Set `sample_name` = @_p_0 Where `sample_id`=@_p_1 " +
            "Returning ((`sample_id` + `sample_id`) * `sample_id`) As `Total`", sqlite.ToSql());
        Assert.Equal(
            "Update \"mapped_samples\" Set \"sample_name\" = @_p_0 Where \"sample_id\"=@_p_1 " +
            "Returning ((\"sample_id\" + \"sample_id\") * \"sample_id\") As \"Total\"", postgreSql.ToSql());
        AssertMutationParameters(sqlite);
        AssertMutationParameters(postgreSql);
    }

    /// <summary>
    /// 测试目的：SQL Server Insert 与 Update 使用 Inserted，Delete 使用 Deleted 渲染计算列。
    /// </summary>
    [Fact]
    public void Returning_WhenSqlServerMutationUsesComputedProjection_ShouldUseInsertedAndDeleted()
    {
        var services = SqlBuilderServices.CreateDefault();
        var insert = new SqlInsertBuilder(SqlServerSqlProvider.Instance, services)
            .InsertInto("mapped_samples")
            .Columns("sample_name")
            .Values("Bing");
        insert.Returning<ComputedReturningEntity>(row => new { Total = (row.Id + row.Id) * row.Id });

        var update = new SqlUpdateBuilder(SqlServerSqlProvider.Instance, services);
        update.Update(new SqlTableReference { TableName = "mapped_samples" })
            .Set("sample_name", "Bing")
            .Where("sample_id", 7);
        update.Returning<ComputedReturningEntity>(row => new { Total = (row.Id + row.Id) * row.Id });

        var delete = new SqlDeleteBuilder(SqlServerSqlProvider.Instance, services);
        delete.DeleteFrom(new SqlTableReference { TableName = "mapped_samples" }).Where("sample_id", 7);
        delete.Returning<ComputedReturningEntity>(row => new { Total = (row.Id + row.Id) * row.Id });

        Assert.Equal(
            "Insert Into [mapped_samples] ([sample_name]) " +
            "Output (([Inserted].[sample_id] + [Inserted].[sample_id]) * [Inserted].[sample_id]) As [Total] " +
            "Values (@_p_0)", insert.ToSql());
        Assert.Equal(
            "Update [mapped_samples] Set [sample_name] = @_p_0 " +
            "Output (([Inserted].[sample_id] + [Inserted].[sample_id]) * [Inserted].[sample_id]) As [Total] " +
            "Where [sample_id]=@_p_1", update.ToSql());
        Assert.Equal(
            "Delete From [mapped_samples] " +
            "Output (([Deleted].[sample_id] + [Deleted].[sample_id]) * [Deleted].[sample_id]) As [Total] " +
            "Where [sample_id]=@_p_0", delete.ToSql());
    }

    /// <summary>
    /// 测试目的：匿名混合投影的直接列和计算列都应保留目标表别名限定符。
    /// </summary>
    [Fact]
    public void Returning_WhenTargetHasAlias_ShouldQualifyDirectAndComputedColumns()
    {
        var builder = new SqlUpdateBuilder(SqliteSqlProvider.Instance, SqlBuilderServices.CreateDefault());
        builder.Update(new SqlTableReference { TableName = "mapped_samples", Alias = "m" })
            .Set("sample_name", "Bing")
            .Where("sample_id", 7);
        builder.Returning<ComputedReturningEntity>(row => new
        {
            row.Id,
            Total = (row.Id + row.Id) * row.Id
        });

        Assert.Equal(
            "Update `mapped_samples` As `m` Set `sample_name` = @_p_0 Where `sample_id`=@_p_1 " +
            "Returning `m`.`sample_id` As `Id`, ((`m`.`sample_id` + `m`.`sample_id`) * `m`.`sample_id`) " +
            "As `Total`", builder.ToSql());
        AssertMutationParameters(builder);
    }

    /// <summary>
    /// 测试目的：计算投影应在重复渲染、克隆和清空时维持稳定且互不影响。
    /// </summary>
    [Fact]
    public void Returning_WhenComputedProjectionIsRenderedClonedAndCleared_ShouldRemainStableAndIndependent()
    {
        var source = CreateSqliteUpdateBuilder();
        source.Returning<ComputedReturningEntity>(row => new { Total = (row.Id + row.Id) * row.Id });
        var expected = ExpectedSqliteUpdate("((`sample_id` + `sample_id`) * `sample_id`) As `Total`");

        Assert.Equal(expected, source.ToSql());
        Assert.Equal(expected, source.ToSql());
        var clone = Assert.IsType<SqlUpdateBuilder>(source.Clone());
        Assert.Equal(expected, clone.ToSql());

        clone.ReturningClause.Clear();
        Assert.Equal("Update `mapped_samples` Set `sample_name` = @_p_0 Where `sample_id`=@_p_1", clone.ToSql());
        Assert.Equal(expected, source.ToSql());

        source.ReturningClause.Clear();
        Assert.Equal("Update `mapped_samples` Set `sample_name` = @_p_0 Where `sample_id`=@_p_1", source.ToSql());
        Assert.Equal(2, source.MutationContext.ParameterManager.GetParams().Count);
        Assert.Equal(2, clone.MutationContext.ParameterManager.GetParams().Count);
        AssertMutationParameters(source);
        AssertMutationParameters(clone);
    }

    /// <summary>
    /// 测试目的：计算投影中的常量、捕获值、方法、SQL 函数、转换、checked 和条件节点应被拒绝。
    /// </summary>
    [Theory]
    [MemberData(nameof(UnsupportedProjections))]
    public void Returning_WhenProjectionContainsUnsupportedExpression_ShouldThrowWithoutAddingColumns(
        Expression<Func<ComputedReturningEntity, object>> projection)
    {
        var builder = CreateSqliteUpdateBuilder();
        builder.Returning<ComputedReturningEntity>(row => new { row.Id });

        Assert.Throws<ArgumentException>(() => builder.Returning(projection));

        Assert.Equal(ExpectedSqliteUpdate("`sample_id` As `Id`"), builder.ToSql());
        AssertMutationParameters(builder);
    }

    /// <summary>
    /// 测试目的：合法投影已存在时，后续包含非法项的匿名投影失败应保持原 Returning 不变。
    /// </summary>
    [Fact]
    public void Returning_WhenLegalProjectionPrecedesIllegalProjection_ShouldRejectAtomically()
    {
        var builder = CreateSqliteUpdateBuilder();
        builder.Returning<ComputedReturningEntity>(row => new { row.Id });

        Assert.Throws<ArgumentException>(() => builder.Returning<ComputedReturningEntity>(row => new
        {
            Display = row.Name,
            Total = row.Id + 1
        }));

        Assert.Equal(ExpectedSqliteUpdate("`sample_id` As `Id`"), builder.ToSql());
        AssertMutationParameters(builder);
    }

    private static Expression<Func<ComputedReturningEntity, object>> CreateCapturedProjection()
    {
        var captured = 3;
        return row => new { Total = row.Int32 + captured };
    }

    private static SqlUpdateBuilder CreateSqliteUpdateBuilder()
    {
        var builder = new SqlUpdateBuilder(SqliteSqlProvider.Instance, SqlBuilderServices.CreateDefault());
        builder.Update(new SqlTableReference { TableName = "mapped_samples" })
            .Set("sample_name", "Bing")
            .Where("sample_id", 7);
        return builder;
    }

    private static SqlUpdateBuilder CreatePostgreSqlUpdateBuilder()
    {
        var builder = new SqlUpdateBuilder(PostgreSqlSqlProvider.Instance, SqlBuilderServices.CreateDefault());
        builder.Update(new SqlTableReference { TableName = "mapped_samples" })
            .Set("sample_name", "Bing")
            .Where("sample_id", 7);
        return builder;
    }

    private static string ExpectedSqliteUpdate(string projection) =>
        "Update `mapped_samples` Set `sample_name` = @_p_0 Where `sample_id`=@_p_1 Returning " + projection;

    private static void AssertMutationParameters(SqlUpdateBuilder builder)
    {
        var parameters = builder.MutationContext.ParameterManager.GetParams();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("Bing", parameters["@_p_0"]);
        Assert.Equal(7, parameters["@_p_1"]);
    }

    [Table("mapped_samples")]
    public sealed class ComputedReturningEntity
    {
        [Column("sample_id")]
        public int Id { get; set; }

        [Column("sample_name")]
        public string Name { get; set; }

        [Column("sample_int32")]
        public int Int32 { get; set; }

        [Column("sample_int64")]
        public long Int64 { get; set; }

        [Column("sample_decimal")]
        public decimal Decimal { get; set; }

        [Column("sample_double")]
        public double Double { get; set; }

        [Column("sample_single")]
        public float Single { get; set; }

        [Column("sample_nullable_int32")]
        public int? NullableInt32 { get; set; }

        [Column("sample_nullable_int64")]
        public long? NullableInt64 { get; set; }

        [Column("sample_nullable_decimal")]
        public decimal? NullableDecimal { get; set; }

        [Column("sample_nullable_double")]
        public double? NullableDouble { get; set; }

        [Column("sample_nullable_single")]
        public float? NullableSingle { get; set; }
    }

    private sealed class ComputedReturningDto
    {
        public int Id { get; set; }

        public int Total { get; set; }
    }

    private sealed class ConvertedReturningDto
    {
        public long Key { get; set; }
    }
}
