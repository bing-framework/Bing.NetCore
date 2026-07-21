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
}