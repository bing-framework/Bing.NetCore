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
    /// 测试目的：结构化 From 应输出完整的 SQL Server 三段表名和别名。
    /// </summary>
    [Fact]
    public void From_WhenUsingStructuredReference_ShouldRenderCompleteSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        var reference = new SqlTableReference
        {
            Database = "sales",
            Schema = "dbo",
            TableName = "orders",
            Alias = "o"
        };

        // Act
        builder.FromClause.From(reference);

        // Assert
        Assert.Equal("From [sales].[dbo].[orders] As [o]", builder.FromClause.ToSql());
    }

    /// <summary>
    /// 测试目的：执行上下文数据库类型应决定结构化表引用的渲染方言。
    /// </summary>
    [Fact]
    public void From_WhenExecutionContextHasDatabaseType_ShouldUseExecutionContext()
    {
        // Arrange
        var context = new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
        };
        var builder = new TestSqlBuilder(options: new SqlOptions().SetDatabaseContext(context));
        var reference = new SqlTableReference { Database = "sales", Schema = "dbo", TableName = "orders" };

        // Act
        builder.FromClause.From(reference);

        // Assert
        Assert.Equal("From [sales].[dbo].[orders]", builder.FromClause.ToSql());
    }

    /// <summary>
    /// 测试目的：结构化 Join 应输出完整的 SQL Server 三段表名和别名。
    /// </summary>
    [Fact]
    public void Join_WhenUsingStructuredReference_ShouldRenderCompleteSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        var reference = new SqlTableReference
        {
            Database = "sales",
            Schema = "dbo",
            TableName = "customers",
            Alias = "c"
        };

        // Act
        builder.JoinClause.Join(reference);

        // Assert
        Assert.Equal("Join [sales].[dbo].[customers] As [c]", builder.JoinClause.ToSql());
    }

    /// <summary>
    /// 测试目的：结构化 LeftJoin 应输出完整的 SQL Server 三段表名和别名。
    /// </summary>
    [Fact]
    public void LeftJoin_WhenUsingStructuredReference_ShouldRenderCompleteSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        var reference = new SqlTableReference
        {
            Database = "sales",
            Schema = "dbo",
            TableName = "customers",
            Alias = "c"
        };

        // Act
        builder.JoinClause.LeftJoin(reference);

        // Assert
        Assert.Equal("Left Join [sales].[dbo].[customers] As [c]", builder.JoinClause.ToSql());
    }

    /// <summary>
    /// 测试目的：结构化 RightJoin 应输出完整的 SQL Server 三段表名和别名。
    /// </summary>
    [Fact]
    public void RightJoin_WhenUsingStructuredReference_ShouldRenderCompleteSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        var reference = new SqlTableReference
        {
            Database = "sales",
            Schema = "dbo",
            TableName = "customers",
            Alias = "c"
        };

        // Act
        builder.JoinClause.RightJoin(reference);

        // Assert
        Assert.Equal("Right Join [sales].[dbo].[customers] As [c]", builder.JoinClause.ToSql());
    }

    /// <summary>
    /// 测试目的：原始 From 后的结构化 Join 在 PostgreSQL 目标包含 Database 时应拒绝跨库 SQL。
    /// </summary>
    [Fact]
    public void Join_WhenRawFromAndPostgreSqlTargetContainsDatabase_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = CreateBuilder(DatabaseType.PgSql);
        builder.FromClause.AppendSql("orders o");
        builder.JoinClause.Join(new SqlTableReference { Database = "reporting", TableName = "customers" });

        // Act
        var action = () => builder.JoinClause.ToSql();

        // Assert
        Assert.Throws<NotSupportedException>(action);
    }

    /// <summary>
    /// 测试目的：原始 From 后的结构化 Join 在 Oracle 目标包含 Database 时应拒绝跨库 SQL。
    /// </summary>
    [Fact]
    public void Join_WhenRawFromAndOracleTargetContainsDatabase_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = CreateBuilder(DatabaseType.Oracle);
        builder.FromClause.AppendSql("orders o");
        builder.JoinClause.Join(new SqlTableReference { Database = "reporting", TableName = "customers" });

        // Act
        var action = () => builder.JoinClause.ToSql();

        // Assert
        Assert.Throws<NotSupportedException>(action);
    }

    /// <summary>
    /// 创建带指定 Provider 执行上下文的测试 Builder。
    /// </summary>
    private static TestSqlBuilder CreateBuilder(DatabaseType databaseType)
    {
        var context = new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor { DatabaseType = databaseType }
        };
        return new TestSqlBuilder(options: new SqlOptions().SetDatabaseContext(context),
            crossDatabaseQueryValidator: new DefaultSqlCrossDatabaseQueryValidator());
    }
}