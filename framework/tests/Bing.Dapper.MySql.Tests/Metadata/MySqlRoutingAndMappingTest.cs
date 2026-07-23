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

    /// <summary>
    /// 测试 - Doris 结构化 From 应保留带点名称的分段语义。
    /// </summary>
    [Fact]
    public void Doris_StructuredFrom_ShouldKeepSegmentedName()
    {
        // Arrange
        var builder = new MySqlBuilder(options: CreateSqlOptions(DatabaseType.Doris));

        // Act
        var sql = builder.Select("Id").From("Merchants.Company").ToSql();

        // Assert
        Assert.Equal("Select `Id` \r\nFrom `Merchants`.`Company`", sql);
    }

    /// <summary>
    /// 测试 - Doris 原始 AppendFrom 文本不得应用方言转换。
    /// </summary>
    [Fact]
    public void Doris_AppendFrom_ShouldPreserveRawSql()
    {
        // Arrange
        var builder = new MySqlBuilder(options: CreateSqlOptions(DatabaseType.Doris));

        // Act
        var sql = builder.Select("c.Id")
            .AppendFrom("[Merchants.Company] c /* @tenant */")
            .ToSql();

        // Assert
        Assert.Equal("Select `c`.`Id` \r\nFrom [Merchants.Company] c /* @tenant */", sql);
    }

    /// <summary>
    /// 测试 - Doris 原始 AppendJoin 文本不得应用方言转换。
    /// </summary>
    [Fact]
    public void Doris_AppendJoin_ShouldPreserveRawSql()
    {
        // Arrange
        var builder = new MySqlBuilder(options: CreateSqlOptions(DatabaseType.Doris));

        // Act
        var sql = builder.Select("c.Id")
            .AppendFrom("Orders c")
            .AppendJoin("\"Audit.Log\" a On a.CompanyId=c.Id /* @tenant */")
            .ToSql();

        // Assert
        Assert.Equal("Select `c`.`Id` \r\nFrom Orders c \r\nJoin \"Audit.Log\" a On a.CompanyId=c.Id /* @tenant */", sql);
    }

    /// <summary>
    /// 测试目的：MySQL 类型化 From 和 Join 必须将带点物理表名作为原子标识符，并允许 schema 临时覆盖。
    /// </summary>
    [Fact]
    public void TypedTables_WhenPhysicalTableNamesContainDots_ShouldRemainAtomicAcrossRenderings()
    {
        // Arrange
        var metadataOptions = CreateDottedTableMetadataOptions();
        var builder = new MySqlBuilder(entityMappingResolver: new DefaultEntityMappingResolver(options: metadataOptions),
            metadataOptions: metadataOptions, options: CreateSqlOptions(DatabaseType.MySql));

        // Act
        builder.Select("c.Id,m.CompanyId")
            .From<RoutingSample>("c")
            .Join<RoutingJoinSample>("m", "archive_db")
            .On<RoutingSample, RoutingJoinSample>(left => left.Id, right => right.CompanyId)
            .Where<RoutingSample>(entity => entity.Name, "active")
            .OrderBy("c.Id")
            .Page(new Pager(3, 10, "c.Id"));
        var firstSql = builder.ToSql();
        var secondSql = builder.ToSql();
        var cloneSql = builder.Clone().ToSql();
        var newBuilder = builder.New();
        newBuilder.Select("c.Id,m.CompanyId")
            .From<RoutingSample>("c")
            .Join<RoutingJoinSample>("m")
            .On<RoutingSample, RoutingJoinSample>(left => left.Id, right => right.CompanyId);
        var newSql = newBuilder.ToSql();

        // Assert
        Assert.Equal("Select `c`.`Id`,`m`.`CompanyId` \r\nFrom `Merchants.Company` As `c` \r\nJoin `archive_db`.`Merchants.Merchant` As `m` On `c`.`Id`=`m`.`CompanyId` \r\nWhere `c`.`Name`=@_p_0 \r\nOrder By `c`.`Id` \r\nLimit @_p_2 OFFSET @_p_1", firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(firstSql, cloneSql);
        Assert.Equal("Select `c`.`Id`,`m`.`CompanyId` \r\nFrom `Merchants.Company` As `c` \r\nJoin `Merchants.Merchant` As `m` On `c`.`Id`=`m`.`CompanyId`", newSql);
        Assert.Equal(3, builder.GetParams().Count);
        Assert.Equal("active", builder.GetParam("_p_0"));
        Assert.Equal(20, builder.GetParam("_p_1"));
        Assert.Equal(10, builder.GetParam("_p_2"));
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
    /// 创建带点物理表名的元数据配置。
    /// </summary>
    private static SqlMetadataOptions CreateDottedTableMetadataOptions()
    {
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(RoutingSample),
            DbKey = "reporting",
            TableName = "Merchants.Company",
            Columns =
            {
                [nameof(RoutingSample.Id)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(RoutingSample.Id),
                    ColumnName = "Id"
                },
                [nameof(RoutingSample.Name)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(RoutingSample.Name),
                    ColumnName = "Name"
                }
            }
        });
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(RoutingJoinSample),
            DbKey = "reporting",
            TableName = "Merchants.Merchant",
            Columns =
            {
                [nameof(RoutingJoinSample.CompanyId)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(RoutingJoinSample.CompanyId),
                    ColumnName = "CompanyId"
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
        /// 标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 路由连接测试样例。
    /// </summary>
    private sealed class RoutingJoinSample
    {
        /// <summary>
        /// 公司标识。
        /// </summary>
        public int CompanyId { get; set; }
    }
}
