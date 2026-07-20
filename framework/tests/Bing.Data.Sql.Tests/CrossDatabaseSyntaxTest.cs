using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Shouldly;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 跨数据库标识符语法测试
/// </summary>
public class CrossDatabaseSyntaxTest
{
    /// <summary>
    /// 测试目的：不同数据库应使用各自合法的表标识符转义方式。
    /// </summary>
    [Theory]
    [InlineData(DatabaseType.SqlServer, "[sales].[Orders]")]
    [InlineData(DatabaseType.MySql, "`sales`.`Orders`")]
    [InlineData(DatabaseType.PgSql, "\"sales\".\"Orders\"")]
    [InlineData(DatabaseType.Oracle, "\"sales\".\"Orders\"")]
    [InlineData(DatabaseType.Sqlite, "`sales`.`Orders`")]
    [InlineData(DatabaseType.Doris, "`Orders`")]
    public void FormatTable_WhenDatabaseTypeSpecified_ShouldUseProviderSyntax(DatabaseType databaseType,
        string expected)
    {
        // Arrange
        var adapter = new DefaultDatabaseDialectAdapter();

        // Act
        var result = adapter.FormatTable(new TableIdentifier("sales", "Orders"), databaseType);

        // Assert
        result.ShouldBe(expected);
    }

    /// <summary>
    /// 测试目的：标识符中的结束转义符应被转义，避免拼接后改变 SQL 语义。
    /// </summary>
    [Fact]
    public void FormatColumn_WhenIdentifierContainsClosingDelimiter_ShouldEscapeDelimiter()
    {
        // Arrange
        var adapter = new DefaultDatabaseDialectAdapter();

        // Act
        var result = adapter.FormatColumn(new ColumnIdentifier("order]name"), DatabaseType.SqlServer);

        // Assert
        result.ShouldBe("[order]]name]");
    }

    /// <summary>
    /// 测试目的：包含语句分隔符的动态标识符必须被拒绝，避免 SQL 注入。
    /// </summary>
    [Fact]
    public void FormatTable_WhenIdentifierContainsStatementDelimiter_ShouldThrowArgumentException()
    {
        // Arrange
        var adapter = new DefaultDatabaseDialectAdapter();

        // Act
        var action = () => adapter.FormatTable(new TableIdentifier("sales", "Orders;drop"), DatabaseType.MySql);

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    /// <summary>
    /// 测试目的：同一数据源切换 Provider 时应使用独立映射缓存并保留结构化标识符。
    /// </summary>
    [Fact]
    public void EntityMappingResolver_WhenProviderChanges_ShouldUseIndependentMappingCache()
    {
        // Arrange
        var resolver = new DefaultEntityMappingResolver();
        var mySqlContext = new DatabaseContext
        {
            DbKey = "default",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.MySql }
        };
        var pgSqlContext = new DatabaseContext
        {
            DbKey = "default",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.PgSql }
        };

        // Act
        var mySqlMapping = resolver.Resolve(typeof(Sample), mySqlContext);
        var pgSqlMapping = resolver.Resolve(typeof(Sample), pgSqlContext);

        // Assert
        ReferenceEquals(mySqlMapping, pgSqlMapping).ShouldBeFalse();
        mySqlMapping.Table.Name.ShouldBe(nameof(Sample));
        mySqlMapping.Columns[nameof(Sample.StringValue)].Column.Name.ShouldBe(nameof(Sample.StringValue));
        mySqlMapping.TableReference.ResolvedTableName.ShouldBe(nameof(Sample));
        pgSqlMapping.TableReference.ResolvedTableName.ShouldBe(nameof(Sample));
        mySqlMapping.FullTableName.ShouldBe(nameof(Sample));
        pgSqlMapping.FullTableName.ShouldBe(nameof(Sample));
    }

    /// <summary>
    /// 测试目的：Doris 数据源必须默认禁用事务能力。
    /// </summary>
    [Fact]
    public void DorisSyntax_ShouldNotSupportTransactions()
    {
        // Arrange
        var adapter = new DefaultDatabaseDialectAdapter();

        // Act
        var syntax = adapter.GetSyntax(DatabaseType.Doris);

        // Assert
        syntax.SupportsTransactions.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：旧 Schema 应默认作为逻辑表名前缀，避免被误解为物理架构。
    /// </summary>
    [Fact]
    public void EntityMappingResolver_WhenLegacySchemaSpecified_ShouldResolveLogicalTablePrefix()
    {
        // Arrange
        var options = new SqlMetadataOptions();
    #pragma warning disable CS0618
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "mysql",
            Schema = "order",
            TableName = "orderinfo"
        });
#pragma warning restore CS0618
        var resolver = new DefaultEntityMappingResolver(options: options);
        var context = new DatabaseContext
        {
            DbKey = "mysql",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.MySql }
        };

        // Act
        var mapping = resolver.Resolve(typeof(Sample), context);

        // Assert
        mapping.LogicalSchema.ShouldBe("order");
        mapping.PhysicalSchema.ShouldBeEmpty();
        mapping.TableReference.ResolvedTableName.ShouldBe("order_orderinfo");
    }

    /// <summary>
    /// 测试目的：旧 Schema 在 SQL Server 下应按物理架构解释，避免生成错误逻辑前缀。
    /// </summary>
    [Fact]
    public void EntityMappingResolver_WhenLegacySchemaUsedForSqlServer_ShouldResolvePhysicalSchema()
    {
        // Arrange
        var options = new SqlMetadataOptions();
    #pragma warning disable CS0618
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "sqlserver",
            Schema = "sales",
            TableName = "orders"
        });
    #pragma warning restore CS0618
        var context = new DatabaseContext
        {
            DbKey = "sqlserver",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
        };

        // Act
        var mapping = new DefaultEntityMappingResolver(options: options).Resolve(typeof(Sample), context);

        // Assert
        mapping.PhysicalSchema.ShouldBe("sales");
        mapping.LogicalSchema.ShouldBeEmpty();
        mapping.TableReference.ResolvedTableName.ShouldBe("orders");
    }

    /// <summary>
    /// 测试目的：映射结果应保留实体类型和最终表名，供延迟 SQL 渲染使用。
    /// </summary>
    [Fact]
    public void EntityMappingResolver_WhenMappingResolved_ShouldKeepEntityTypeAndResolvedTableName()
    {
        // Arrange
        var resolver = new DefaultEntityMappingResolver();

        // Act
        var mapping = resolver.Resolve(typeof(Sample), null);

        // Assert
        mapping.TableReference.EntityType.ShouldBe(typeof(Sample));
        mapping.TableReference.TableName.ShouldBe(nameof(Sample));
        mapping.TableReference.ResolvedTableName.ShouldBe(nameof(Sample));
    }

    /// <summary>
    /// 测试目的：对象名称格式化应按当前方言转义结束引用符。
    /// </summary>
    [Fact]
    public void Format_WhenIdentifierContainsClosingDelimiter_ShouldEscapeForCurrentDialect()
    {
        // Arrange
        var formatter = new DefaultSqlObjectNameFormatter();
        var reference = new SqlTableReference { ResolvedTableName = "order]name" };

        // Act
        var result = formatter.Format(reference, TestDialect.Instance, DatabaseType.SqlServer);

        // Assert
        result.ShouldBe("[order]]name]");
    }

    /// <summary>
    /// 测试目的：同一 DbKey 的 MySQL 跨 Catalog Join 应由能力模型允许。
    /// </summary>
    [Fact]
    public void Validate_WhenMySqlReferencesUseSameDbKeyAndDifferentCatalog_ShouldNotThrow()
    {
        // Arrange
        var validator = new DefaultSqlCrossDatabaseQueryValidator();
        var context = new DatabaseContext
        {
            DbKey = "mysql",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.MySql }
        };
        var source = new SqlTableReference { DbKey = "mysql", Catalog = "primary", ResolvedTableName = "users" };
        var target = new SqlTableReference { DbKey = "mysql", Catalog = "reporting", ResolvedTableName = "orders" };

        // Act
        // Assert
        Should.NotThrow(() => validator.Validate(source, target, context));
    }

    /// <summary>
    /// 测试目的：类型化 From 应按 SQL Server 的 Catalog、物理架构和表名逐段输出。
    /// </summary>
    [Fact]
    public void From_WhenStructuredSqlServerReference_ShouldRenderEachPartSeparately()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "sqlserver",
            Catalog = "reporting",
            PhysicalSchema = "dbo",
            TableName = "users",
            NamingMode = LogicalTableNamingMode.None
        });
        var context = new DatabaseContext
        {
            DbKey = "sqlserver",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
        };
        var builder = new TestSqlBuilder(TestDialect.Instance, entityMappingResolver:
            new DefaultEntityMappingResolver(options: options), metadataOptions: options,
            options: new SqlOptions().SetDatabaseContext(context));

        // Act
        builder.From<Sample>();

        // Assert
        builder.FromClause.ToSql().ShouldBe("From [reporting].[dbo].[users]");
    }

    /// <summary>
    /// 测试目的：PostgreSql 不支持的 Catalog 限定必须显式失败，不能静默忽略。
    /// </summary>
    [Fact]
    public void Format_WhenPostgreSqlReferenceContainsCatalog_ShouldThrowNotSupportedException()
    {
        // Arrange
        var formatter = new DefaultSqlObjectNameFormatter();
        var reference = new SqlTableReference
        {
            Catalog = "reporting",
            ResolvedTableName = "users"
        };

        // Act
        var action = () => formatter.Format(reference, TestDialect.Instance, DatabaseType.PgSql);

        // Assert
        action.ShouldThrow<NotSupportedException>();
    }

    /// <summary>
    /// 测试目的：类型化 Join 的连接表与执行查询使用不同 DbKey 时必须失败。
    /// </summary>
    [Fact]
    public void ValidateJoin_WhenReferenceUsesDifferentDbKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var validator = new DefaultSqlCrossDatabaseQueryValidator();
        var reference = new SqlTableReference
        {
            DbKey = "reporting",
            ResolvedTableName = "users"
        };

        // Act
        var action = () => validator.ValidateJoin("primary", reference);

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }
}