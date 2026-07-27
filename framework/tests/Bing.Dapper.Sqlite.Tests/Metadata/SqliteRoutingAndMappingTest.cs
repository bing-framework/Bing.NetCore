using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// Sqlite 路由与映射测试
/// </summary>
public class SqliteRoutingAndMappingTest
{
    /// <summary>
    /// 测试 - Sqlite Builder 应使用 SqlOptions 绑定的多库上下文解析表名与列名。
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
                DatabaseType = DatabaseType.Sqlite,
                ConnectionString = "Data Source=reporting.db"
            }
        });
        var resolver = new DefaultEntityMappingResolver(null, null, metadataOptions);
        var builder = CreateBuilder(entityMappingResolver: resolver, metadataOptions: metadataOptions,
            options: sqlOptions);

        // Act
        builder.From<RoutingSample>().Where<RoutingSample>(t => t.Name, "abc");
        var sql = builder.ToSql();

        // Assert
        Assert.Contains("users_reporting", sql);
        Assert.Contains("status_code", sql);
    }

    /// <summary>
    /// 测试目的：Sqlite 类型化 From 应仅使用最终表名。
    /// </summary>
    [Fact]
    public void Builder_WhenMappingConfigured_ShouldRenderTableName()
    {
        // Arrange
        var metadataOptions = CreateMetadataOptions();
        var builder = CreateBuilder(entityMappingResolver: new DefaultEntityMappingResolver(options: metadataOptions),
            metadataOptions: metadataOptions, options: CreateSqlOptions());

        // Act
        builder.From<RoutingSample>();

        // Assert
        Assert.Contains("`users_reporting`", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：Sqlite 字符串 From 和 Join 应继续按句点分段渲染。
    /// </summary>
    [Fact]
    public void StringQualifiedTables_ShouldKeepSqliteSegmentedFormatting()
    {
        var builder = new SqliteBuilder();

        var sql = builder.Select("u.Id")
            .From("main.Users", "u")
            .Join("audit.Roles", "r")
            .ToSql();

        Assert.Equal("Select `u`.`Id` \r\nFrom `main`.`Users` As `u` \r\nJoin `audit`.`Roles` As `r`", sql);
    }

    private static SqlOptions CreateSqlOptions() => new SqlOptions().SetDatabaseContext(new DatabaseContext
    {
        DbKey = "reporting",
        DataSource = new SqlDataSourceDescriptor { Key = "reporting", DatabaseType = DatabaseType.Sqlite }
    });

    /// <summary>
    /// 使用公开共享服务创建 SQLite Builder。
    /// </summary>
    private static SqliteBuilder CreateBuilder(IEntityMappingResolver entityMappingResolver = null,
        SqlMetadataOptions metadataOptions = null, SqlOptions options = null) =>
        new(new SqlBuilderServices(entityMappingResolver: entityMappingResolver,
            metadataOptions: metadataOptions, options: options));

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
