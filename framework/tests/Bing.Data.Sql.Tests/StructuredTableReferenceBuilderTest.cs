using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 结构化表引用 Builder 测试。
/// </summary>
public class StructuredTableReferenceBuilderTest
{
    /// <summary>
    /// 测试 - 结构化 From 应输出完整的 SQL Server 三段表名和别名。
    /// </summary>
    [Fact]
    public void From_WhenUsingStructuredReference_ShouldRenderCompleteSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        var reference = new SqlTableReference
        {
            DatabaseType = DatabaseType.SqlServer,
            Catalog = "sales",
            PhysicalSchema = "dbo",
            ResolvedTableName = "orders",
            Alias = "o"
        };

        // Act
        builder.FromClause.From(reference);

        // Assert
        Assert.Equal("From [sales].[dbo].[orders] As [o]", builder.FromClause.ToSql());
    }

    /// <summary>
    /// 测试 - 结构化 Join 应输出完整的 SQL Server 三段表名和别名。
    /// </summary>
    [Fact]
    public void Join_WhenUsingStructuredReference_ShouldRenderCompleteSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.FromClause.From(new SqlTableReference
        {
            DatabaseType = DatabaseType.SqlServer,
            Catalog = "sales",
            PhysicalSchema = "dbo",
            ResolvedTableName = "orders",
            Alias = "o"
        });
        var reference = new SqlTableReference
        {
            DatabaseType = DatabaseType.SqlServer,
            Catalog = "sales",
            PhysicalSchema = "dbo",
            ResolvedTableName = "customers",
            Alias = "c"
        };

        // Act
        builder.JoinClause.Join(reference);

        // Assert
        Assert.Equal("Join [sales].[dbo].[customers] As [c]", builder.JoinClause.ToSql());
    }

    /// <summary>
    /// 测试 - 执行上下文数据库类型应优先于表引用数据库类型。
    /// </summary>
    [Fact]
    public void From_WhenExecutionContextHasDatabaseType_ShouldPreferExecutionContext()
    {
        // Arrange
        var context = new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
        };
        var builder = new TestSqlBuilder(options: new SqlOptions().SetDatabaseContext(context));
        var reference = new SqlTableReference
        {
            DatabaseType = DatabaseType.PgSql,
            Catalog = "sales",
            PhysicalSchema = "dbo",
            ResolvedTableName = "orders"
        };

        // Act
        builder.FromClause.From(reference);

        // Assert
        Assert.Equal("From [sales].[dbo].[orders]", builder.FromClause.ToSql());
    }

    /// <summary>
    /// 测试 - 单次 Join 渲染应只执行一次跨库校验。
    /// </summary>
    [Fact]
    public void Join_WhenRenderingRepeatedly_ShouldValidateCrossDatabaseOnce()
    {
        // Arrange
        var validator = new CountingCrossDatabaseQueryValidator();
        var builder = new TestSqlBuilder(crossDatabaseQueryValidator: validator);
        builder.FromClause.From(new SqlTableReference
        {
            DatabaseType = DatabaseType.SqlServer,
            PhysicalSchema = "dbo",
            ResolvedTableName = "orders"
        });
        builder.JoinClause.Join(new SqlTableReference
        {
            DatabaseType = DatabaseType.SqlServer,
            PhysicalSchema = "dbo",
            ResolvedTableName = "customers"
        });

        // Act
        var first = builder.JoinClause.ToSql();
        var second = builder.JoinClause.ToSql();

        // Assert
        Assert.Equal("Join [dbo].[customers]", first);
        Assert.Equal(first, second);
        Assert.Equal(1, validator.Count);
    }

    /// <summary>
    /// 计数型跨数据库查询校验器。
    /// </summary>
    private sealed class CountingCrossDatabaseQueryValidator : ISqlCrossDatabaseQueryValidator
    {
        /// <summary>
        /// 调用次数。
        /// </summary>
        public int Count { get; private set; }

        /// <inheritdoc />
        public void Validate(SqlTableReference source, SqlTableReference target, DatabaseContext executionContext) => Count++;
    }
}