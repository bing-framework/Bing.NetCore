using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// PostgreSql 路由与映射测试
/// </summary>
public class PostgreSqlRoutingAndMappingTest
{
    /// <summary>
    /// 测试 - PostgreSql Builder 应使用 SqlOptions 绑定的多库上下文解析表名与列名。
    /// </summary>
    [Fact]
    public void Builder_WithSqlOptionsContext_ShouldUseReportingMapping()
    {
        // Arrange
        var metadataOptions = CreateMetadataOptions();
        var sqlOptions = new SqlOptions().SetDatabaseContext(new DatabaseContext
        {
            DbKey = "reporting",
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "reporting",
                DatabaseType = DatabaseType.PgSql,
                ConnectionString = "Host=reporting;Database=test;"
            }
        });
        var resolver = new DefaultEntityMappingResolver(null, null, metadataOptions);
        var builder = new PostgreSqlBuilder(entityMappingResolver: resolver, metadataOptions: metadataOptions,
            options: sqlOptions);

        // Act
        builder.From<RoutingSample>().Where<RoutingSample>(t => t.Name, "abc");
        var sql = builder.ToSql();

        // Assert
        Assert.Contains("users_reporting", sql);
        Assert.Contains("status_code", sql);
    }

    /// <summary>
    /// 测试目的：PostgreSql 类型化 From 应将物理架构与表名分段引用。
    /// </summary>
    [Fact]
    public void Builder_WhenPhysicalSchemaConfigured_ShouldRenderSchemaAndTable()
    {
        // Arrange
        var metadataOptions = CreateMetadataOptions();
        metadataOptions.EntityMappings[0].PhysicalSchema = "reports";
        var builder = new PostgreSqlBuilder(entityMappingResolver: new DefaultEntityMappingResolver(options: metadataOptions),
            metadataOptions: metadataOptions, options: CreateSqlOptions());

        // Act
        builder.From<RoutingSample>();

        // Assert
        Assert.Contains("\"reports\".\"users_reporting\"", builder.ToSql());
    }

    private static SqlOptions CreateSqlOptions() => new SqlOptions().SetDatabaseContext(new DatabaseContext
    {
        DbKey = "reporting",
        DataSource = new SqlDataSourceDescriptor { Key = "reporting", DatabaseType = DatabaseType.PgSql }
    });

    /// <summary>
    /// 创建 Sql 元数据配置
    /// </summary>
    /// <returns>Sql 元数据配置</returns>
    private static SqlMetadataOptions CreateMetadataOptions()
    {
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(RoutingSample),
            DbKey = "reporting",
            TableName = "users_reporting",
            Columns =
            {
                [nameof(RoutingSample.Name)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(RoutingSample.Name),
                    ColumnName = "status_code"
                }
            }
        });
        return options;
    }

    /// <summary>
    /// 路由测试样例
    /// </summary>
    private sealed class RoutingSample
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
    }
}
