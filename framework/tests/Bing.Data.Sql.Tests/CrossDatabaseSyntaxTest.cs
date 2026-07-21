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
    /// 测试 - SQL Server 结构化表引用应按三段名称格式化。
    /// </summary>
    [Fact]
    public void Format_WhenSqlServerReferenceHasCatalogAndSchema_ShouldFormatThreeParts()
    {
        // Arrange
        var formatter = new DefaultSqlObjectNameFormatter();
        var reference = new SqlTableReference
        {
            Catalog = "sales",
            PhysicalSchema = "dbo",
            ResolvedTableName = "Orders"
        };

        // Act
        var result = formatter.Format(reference, TestDialect.Instance, DatabaseType.SqlServer);

        // Assert
        result.ShouldBe("[sales].[dbo].[Orders]");
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
        mySqlMapping.Columns[nameof(Sample.StringValue)].ColumnName.ShouldBe(nameof(Sample.StringValue));
        mySqlMapping.TableReference.ResolvedTableName.ShouldBe(nameof(Sample));
        pgSqlMapping.TableReference.ResolvedTableName.ShouldBe(nameof(Sample));
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
    /// 测试 - 无法确定数据库类型时不应默认使用SqlServer。
    /// </summary>
    [Fact]
    public void Format_WhenDatabaseTypeCannotBeResolved_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var formatter = new DefaultSqlObjectNameFormatter();
        var reference = new SqlTableReference { ResolvedTableName = "orders" };

        // Act
        var action = () => formatter.Format(reference, TestDialect.Instance, null);

        // Assert
        action.ShouldThrow<InvalidOperationException>();
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
    /// 测试 - PostgreSql 不支持的 Catalog 限定必须显式失败，不能静默忽略。
    /// </summary>
    [Fact]
    public void Validate_WhenPostgreSqlReferenceContainsCatalog_ShouldThrowNotSupportedException()
    {
        // Arrange
        var validator = new DefaultSqlTableReferenceValidator();
        var reference = new SqlTableReference
        {
            Catalog = "reporting",
            ResolvedTableName = "users"
        };

        // Act
        var action = () => validator.Validate(reference, DatabaseType.PgSql);

        // Assert
        action.ShouldThrow<NotSupportedException>();
    }

    /// <summary>
    /// 测试 - 类型化 Join 的源表与连接表使用不同 DbKey 时必须失败。
    /// </summary>
    [Fact]
    public void Validate_WhenReferencesUseDifferentDbKey_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var validator = new DefaultSqlCrossDatabaseQueryValidator();
        var reference = new SqlTableReference
        {
            DbKey = "reporting",
            ResolvedTableName = "users"
        };
        var source = new SqlTableReference
        {
            DbKey = "primary",
            ResolvedTableName = "orders"
        };

        // Act
        var action = () => validator.Validate(source, reference, null);

        // Assert
        action.ShouldThrow<InvalidOperationException>();
    }
}