using Bing.Data.Enums;
using Bing.Data;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Shouldly;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 多数据库上下文与映射测试
/// </summary>
public class DatabaseRoutingAndMappingTest
{
    /// <summary>
    /// 测试 - 嵌套数据库上下文作用域在释放后应恢复父级上下文。
    /// </summary>
    [Fact]
    public void DatabaseScopeManager_NestedScope_ShouldRestoreParentContext()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, new SqlMetadataOptions());

        // Act
        using (manager.Use("default", DatabaseType.MySql))
        {
            accessor.Current.ShouldNotBeNull();
            accessor.Current.DbKey.ShouldBe("default");
            accessor.Current.DatabaseType.ShouldBe(DatabaseType.MySql);

            using (manager.Use("reporting", DatabaseType.PgSql, DatabaseRole.Reporting))
            {
                accessor.Current.DbKey.ShouldBe("reporting");
                accessor.Current.DatabaseType.ShouldBe(DatabaseType.PgSql);
                accessor.Current.Role.ShouldBe(DatabaseRole.Reporting);
            }

            // Assert
            accessor.Current.ShouldNotBeNull();
            accessor.Current.DbKey.ShouldBe("default");
            accessor.Current.DatabaseType.ShouldBe(DatabaseType.MySql);
            accessor.Current.Role.ShouldBe(DatabaseRole.Default);
        }

        accessor.Current.ShouldBeNull();
    }

    /// <summary>
    /// 测试 - 数据库描述解析器应按 DbKey、DatabaseType 与 Role 命中配置。
    /// </summary>
    [Fact]
    public void DatabaseDescriptorResolver_ShouldResolveByDbKeyDatabaseTypeAndRole()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.Databases[SqlMetadataOptions.GetDatabaseDescriptorKey("reporting", DatabaseType.MySql,
            DatabaseRole.Reporting)] = new DatabaseDescriptor
        {
            DbKey = "reporting",
            DatabaseType = DatabaseType.MySql,
            Role = DatabaseRole.Reporting,
            ConnectionString = "Server=reporting;",
            ReadOnly = true
        };
        var resolver = new DefaultDatabaseDescriptorResolver(options);

        // Act
        var descriptor = resolver.Resolve(new DatabaseContext
        {
            DbKey = "reporting",
            DatabaseType = DatabaseType.MySql,
            Role = DatabaseRole.Reporting
        });

        // Assert
        descriptor.DbKey.ShouldBe("reporting");
        descriptor.DatabaseType.ShouldBe(DatabaseType.MySql);
        descriptor.Role.ShouldBe(DatabaseRole.Reporting);
        descriptor.ConnectionString.ShouldBe("Server=reporting;");
        descriptor.ReadOnly.ShouldBeTrue();
    }

    /// <summary>
    /// 测试 - 同一实体在不同数据库上下文下应使用不同表名与列名。
    /// </summary>
    [Fact]
    public void EntityMappingResolver_SameEntityDifferentDb_ShouldUseDifferentTableAndColumn()
    {
        // Arrange
        var options = CreateMetadataOptions();
        var resolver = new DefaultEntityMappingResolver(new TestEntityMetadata(), null, options);

        // Act
        var defaultMapping = resolver.Resolve(typeof(Sample), new DatabaseContext
        {
            DbKey = "default",
            DatabaseType = DatabaseType.MySql,
            Role = DatabaseRole.Default
        });
        var reportingMapping = resolver.Resolve(typeof(Sample), new DatabaseContext
        {
            DbKey = "reporting",
            DatabaseType = DatabaseType.MySql,
            Role = DatabaseRole.Reporting
        });

        // Assert
        defaultMapping.TableName.ShouldBe("users");
        defaultMapping.Columns[nameof(Sample.StringValue)].ColumnName.ShouldBe("status");
        reportingMapping.TableName.ShouldBe("users_reporting");
        reportingMapping.Columns[nameof(Sample.StringValue)].ColumnName.ShouldBe("status_code");
    }

    /// <summary>
    /// 测试 - 同一实体在不同数据库上下文下应使用不同缓存键。
    /// </summary>
    [Fact]
    public void EntityMappingResolver_SameEntityDifferentDb_ShouldUseDifferentCacheKey()
    {
        // Arrange
        var options = CreateMetadataOptions();
        var resolver = new DefaultEntityMappingResolver(new TestEntityMetadata(), null, options);
        var defaultContext = new DatabaseContext
        {
            DbKey = "default",
            DatabaseType = DatabaseType.MySql,
            Role = DatabaseRole.Default
        };
        var reportingContext = new DatabaseContext
        {
            DbKey = "reporting",
            DatabaseType = DatabaseType.MySql,
            Role = DatabaseRole.Reporting
        };

        // Act
        var defaultMapping1 = resolver.Resolve(typeof(Sample), defaultContext);
        var defaultMapping2 = resolver.Resolve(typeof(Sample), defaultContext);
        var reportingMapping = resolver.Resolve(typeof(Sample), reportingContext);

        // Assert
        ReferenceEquals(defaultMapping1, defaultMapping2).ShouldBeTrue();
        ReferenceEquals(defaultMapping1, reportingMapping).ShouldBeFalse();
    }

    /// <summary>
    /// 测试 - Lambda Where 在不同数据库上下文下应拼接不同列名。
    /// </summary>
    [Fact]
    public void LambdaWhere_DifferentDbContext_ShouldUseDifferentColumnName()
    {
        // Arrange
        var metadata = new TestEntityMetadata();
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var options = CreateMetadataOptions();
        var resolver = new DefaultEntityMappingResolver(metadata, accessor, options);
        var scopeManager = new DatabaseScopeManager(accessor, options);

        // Act
        string defaultCondition;
        using (scopeManager.Use("default", DatabaseType.MySql))
        {
            var builder = new TestSqlBuilder(TestDialect.Instance, metadata, entityMappingResolver: resolver,
                databaseContextAccessor: accessor, metadataOptions: options);
            builder.Where<Sample>(t => t.StringValue, "abc");
            defaultCondition = builder.GetCondition();
        }

        string reportingCondition;
        using (scopeManager.Use("reporting", DatabaseType.MySql, DatabaseRole.Reporting))
        {
            var builder = new TestSqlBuilder(TestDialect.Instance, metadata, entityMappingResolver: resolver,
                databaseContextAccessor: accessor, metadataOptions: options);
            builder.Where<Sample>(t => t.StringValue, "abc");
            reportingCondition = builder.GetCondition();
        }

        // Assert
        defaultCondition.ShouldBe("[status]=@_p_0");
        reportingCondition.ShouldBe("[status_code]=@_p_0");
    }

    /// <summary>
    /// 测试 - SqlOptions 绑定的数据库上下文应优先于当前作用域上下文。
    /// </summary>
    [Fact]
    public void LambdaWhere_WithSqlOptionsContext_ShouldUseBoundContext()
    {
        // Arrange
        var metadata = new TestEntityMetadata();
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var options = CreateMetadataOptions();
        var resolver = new DefaultEntityMappingResolver(metadata, accessor, options);
        var sqlOptions = new SqlOptions().SetDatabaseContext(new DatabaseContext
        {
            DbKey = "reporting",
            DatabaseType = DatabaseType.MySql,
            Role = DatabaseRole.Reporting
        });
        accessor.Current = new DatabaseContext
        {
            DbKey = "default",
            DatabaseType = DatabaseType.MySql,
            Role = DatabaseRole.Default
        };
        var builder = new TestSqlBuilder(TestDialect.Instance, metadata, entityMappingResolver: resolver,
            databaseContextAccessor: accessor, metadataOptions: options, options: sqlOptions);

        // Act
        builder.Where<Sample>(t => t.StringValue, "abc");

        // Assert
        builder.GetCondition().ShouldBe("[status_code]=@_p_0");
    }

    /// <summary>
    /// 创建 Sql 元数据配置
    /// </summary>
    /// <returns>Sql 元数据配置</returns>
    private static SqlMetadataOptions CreateMetadataOptions()
    {
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "default",
            DatabaseType = DatabaseType.MySql,
            Role = DatabaseRole.Default,
            TableName = "users",
            Columns =
            {
                [nameof(Sample.StringValue)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(Sample.StringValue),
                    ColumnName = "status"
                }
            }
        });
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "reporting",
            DatabaseType = DatabaseType.MySql,
            Role = DatabaseRole.Reporting,
            TableName = "users_reporting",
            Columns =
            {
                [nameof(Sample.StringValue)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(Sample.StringValue),
                    ColumnName = "status_code"
                }
            }
        });
        return options;
    }
}