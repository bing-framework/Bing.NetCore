using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// MySql 路由与映射测试
/// </summary>
public class MySqlRoutingAndMappingTest
{
    /// <summary>
    /// 测试 - MySql Builder 应使用 SqlOptions 绑定的多库上下文解析表名与列名。
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
                DatabaseType = DatabaseType.MySql,
                ConnectionString = "Server=reporting;Database=test;"
            }
        });
        var resolver = new DefaultEntityMappingResolver(null, null, metadataOptions);
        var builder = new MySqlBuilder(entityMappingResolver: resolver, metadataOptions: metadataOptions,
            options: sqlOptions);

        // Act
        builder.From<RoutingSample>().Where<RoutingSample>(t => t.Name, "abc");
        var sql = builder.ToSql();

        // Assert
        Assert.Contains("users_reporting", sql);
        Assert.Contains("status_code", sql);
    }

    /// <summary>
    /// 测试 - MySql 应将 Schema 和最终物理表名分别作为名称段输出。
    /// </summary>
    [Fact]
    public void Builder_WhenSchemaConfigured_ShouldRenderPhysicalSchemaAndTableName()
    {
        // Arrange
        var metadataOptions = CreateMetadataOptions();
        metadataOptions.EntityMappings[0].Schema = "order";
        metadataOptions.EntityMappings[0].TableName = "orderinfo";
        var builder = new MySqlBuilder(entityMappingResolver: new DefaultEntityMappingResolver(options: metadataOptions),
            metadataOptions: metadataOptions, options: CreateSqlOptions(DatabaseType.MySql));

        // Act
        builder.From<RoutingSample>();

        // Assert
        Assert.Equal("Select * \r\nFrom `order`.`orderinfo`", builder.ToSql());
    }

    /// <summary>
    /// 测试 - MySqlBuilder缺少数据库上下文时应使用MySql数据库类型。
    /// </summary>
    [Fact]
    public void Builder_WithoutDatabaseContext_ShouldUseMySqlDatabaseType()
    {
        // Arrange
        var metadataOptions = CreateMetadataOptions();
        metadataOptions.EntityMappings[0].DbKey = null;
        var builder = new MySqlBuilder(entityMappingResolver: new DefaultEntityMappingResolver(options: metadataOptions),
            metadataOptions: metadataOptions);

        // Act
        builder.From<RoutingSample>();

        // Assert
        Assert.Equal("From `users_reporting`", builder.FromClause.ToSql());
    }

    private static SqlOptions CreateSqlOptions(DatabaseType databaseType) => new SqlOptions().SetDatabaseContext(
        new DatabaseContext
        {
            DbKey = "reporting",
            DataSource = new SqlDataSourceDescriptor { Key = "reporting", DatabaseType = databaseType }
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
