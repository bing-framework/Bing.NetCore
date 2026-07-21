using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Shouldly;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 结构化对象名称与映射测试。
/// </summary>
public class CrossDatabaseSyntaxTest
{
    /// <summary>
    /// 测试目的：SQL Server 应按 Database、Schema 和 TableName 依次格式化名称段。
    /// </summary>
    [Fact]
    public void Format_WhenSqlServerReferenceHasDatabaseAndSchema_ShouldFormatThreeParts()
    {
        // Arrange
        var formatter = new DefaultSqlObjectNameFormatter();
        var reference = new SqlTableReference { Database = "sales", Schema = "dbo", TableName = "Orders" };

        // Act
        var result = formatter.Format(reference, TestDialect.Instance, DatabaseType.SqlServer);

        // Assert
        result.ShouldBe("[sales].[dbo].[Orders]");
    }

    /// <summary>
    /// 测试目的：映射缓存键应区分执行上下文的数据库类型。
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
        mySqlMapping.Table.TableName.ShouldBe(nameof(Sample));
        pgSqlMapping.Table.TableName.ShouldBe(nameof(Sample));
    }

    /// <summary>
    /// 测试目的：配置的 Schema 应作为最终映射 Schema，不进行 Provider 特定重解释。
    /// </summary>
    [Fact]
    public void EntityMappingResolver_WhenSchemaConfigured_ShouldKeepConfiguredSchema()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "sqlserver",
            Schema = "sales",
            TableName = "orders"
        });
        var context = new DatabaseContext
        {
            DbKey = "sqlserver",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
        };

        // Act
        var mapping = new DefaultEntityMappingResolver(options: options).Resolve(typeof(Sample), context);

        // Assert
        mapping.Table.Schema.ShouldBe("sales");
        mapping.Table.TableName.ShouldBe("orders");
    }

    /// <summary>
    /// 测试目的：对象名称格式化应转义方言结束引用符。
    /// </summary>
    [Fact]
    public void Format_WhenTableNameContainsClosingDelimiter_ShouldEscapeForCurrentDialect()
    {
        // Arrange
        var formatter = new DefaultSqlObjectNameFormatter();
        var reference = new SqlTableReference { TableName = "order]name" };

        // Act
        var result = formatter.Format(reference, TestDialect.Instance, DatabaseType.SqlServer);

        // Assert
        result.ShouldBe("[order]]name]");
    }

    /// <summary>
    /// 测试目的：无执行数据库上下文时，普通结构化 Join 不应被跨数据库校验器阻断。
    /// </summary>
    [Fact]
    public void Validate_WhenExecutionContextMissing_ShouldSkipCapabilityValidation()
    {
        // Arrange
        var validator = new DefaultSqlCrossDatabaseQueryValidator();
        var source = new SqlTableReference { TableName = "users" };
        var target = new SqlTableReference { TableName = "orders" };

        // Act
        var exception = Record.Exception(() => validator.Validate(null, source, target));

        // Assert
        exception.ShouldBeNull();
    }
}