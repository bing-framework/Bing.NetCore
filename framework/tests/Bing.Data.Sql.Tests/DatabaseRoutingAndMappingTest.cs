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
        var options = CreateDataSourceOptions();
        var manager = new DatabaseScopeManager(accessor, options);

        // Act
        using (manager.Use("default"))
        {
            accessor.Current.ShouldNotBeNull();
            accessor.Current.DbKey.ShouldBe("default");
            accessor.Current.DataSource.DatabaseType.ShouldBe(DatabaseType.MySql);

            using (manager.Use("reporting"))
            {
                accessor.Current.DbKey.ShouldBe("reporting");
                accessor.Current.DataSource.DatabaseType.ShouldBe(DatabaseType.PgSql);
            }

            // Assert
            accessor.Current.ShouldNotBeNull();
            accessor.Current.DbKey.ShouldBe("default");
            accessor.Current.DataSource.DatabaseType.ShouldBe(DatabaseType.MySql);
        }

        accessor.Current.ShouldBeNull();
    }

    /// <summary>
    /// 测试 - 数据源解析器应按 DbKey 命中配置。
    /// </summary>
    [Fact]
    public void SqlDataSourceResolver_ShouldResolveByDbKey()
    {
        // Arrange
        var options = CreateDataSourceOptions();
        var resolver = new DefaultSqlDataSourceResolver(options);

        // Act
        var descriptor = resolver.Resolve("reporting");

        // Assert
        descriptor.Key.ShouldBe("reporting");
        descriptor.DatabaseType.ShouldBe(DatabaseType.PgSql);
        descriptor.ConnectionString.ShouldBe("Server=reporting;");
        descriptor.IsReadOnly.ShouldBeTrue();
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
            DataSource = CreateDataSource("default", DatabaseType.MySql)
        });
        var reportingMapping = resolver.Resolve(typeof(Sample), new DatabaseContext
        {
            DbKey = "reporting",
            DataSource = CreateDataSource("reporting", DatabaseType.PgSql)
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
            DataSource = CreateDataSource("default", DatabaseType.MySql)
        };
        var reportingContext = new DatabaseContext
        {
            DbKey = "reporting",
            DataSource = CreateDataSource("reporting", DatabaseType.PgSql)
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
    /// 测试 - 不同 MappingProfile 应使用独立映射缓存。
    /// </summary>
    [Fact]
    public void EntityMappingResolver_DifferentMappingProfiles_ShouldUseIndependentCache()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "default",
            MappingProfile = "read",
            TableName = "users_read"
        });
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "default",
            MappingProfile = "write",
            TableName = "users_write"
        });
        var resolver = new DefaultEntityMappingResolver(new TestEntityMetadata(), null, options);
        var readContext = new DatabaseContext
        {
            DbKey = "default",
            MappingProfile = "read",
            DataSource = CreateDataSource("default", DatabaseType.MySql)
        };
        var writeContext = new DatabaseContext
        {
            DbKey = "default",
            MappingProfile = "write",
            DataSource = CreateDataSource("default", DatabaseType.MySql)
        };

        // Act
        var readMapping = resolver.Resolve(typeof(Sample), readContext);
        var writeMapping = resolver.Resolve(typeof(Sample), writeContext);

        // Assert
        readMapping.TableName.ShouldBe("users_read");
        writeMapping.TableName.ShouldBe("users_write");
        ReferenceEquals(readMapping, writeMapping).ShouldBeFalse();
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
        AddDataSources(options);
        var resolver = new DefaultEntityMappingResolver(metadata, accessor, options);
        var scopeManager = new DatabaseScopeManager(accessor, options);

        // Act
        string defaultCondition;
        using (scopeManager.Use("default"))
        {
            var builder = new TestSqlBuilder(TestDialect.Instance, metadata, entityMappingResolver: resolver,
                databaseContextAccessor: accessor, metadataOptions: options);
            builder.Where<Sample>(t => t.StringValue, "abc");
            defaultCondition = builder.GetCondition();
        }

        string reportingCondition;
        using (scopeManager.Use("reporting"))
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
            DataSource = CreateDataSource("reporting", DatabaseType.PgSql)
        });
        accessor.Current = new DatabaseContext
        {
            DbKey = "default",
            DataSource = CreateDataSource("default", DatabaseType.MySql)
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

    /// <summary>
    /// 创建数据源配置。
    /// </summary>
    /// <returns>Sql 元数据配置。</returns>
    private static SqlMetadataOptions CreateDataSourceOptions()
    {
        var options = new SqlMetadataOptions();
        AddDataSources(options);
        return options;
    }

    /// <summary>
    /// 添加数据源配置。
    /// </summary>
    /// <param name="options">Sql 元数据配置。</param>
    private static void AddDataSources(SqlMetadataOptions options)
    {
        options.DataSources.DataSources["default"] = CreateDataSource("default", DatabaseType.MySql, "Server=default;");
        options.DataSources.DataSources["reporting"] = CreateDataSource("reporting", DatabaseType.PgSql, "Server=reporting;", true);
    }

    /// <summary>
    /// 创建数据源描述。
    /// </summary>
    /// <param name="key">数据源标识。</param>
    /// <param name="databaseType">数据库类型。</param>
    /// <param name="connectionString">连接字符串。</param>
    /// <param name="isReadOnly">是否只读。</param>
    /// <returns>数据源描述。</returns>
    private static SqlDataSourceDescriptor CreateDataSource(string key, DatabaseType databaseType,
        string connectionString = "Server=test;", bool isReadOnly = false) => new()
    {
        Key = key,
        DatabaseType = databaseType,
        ConnectionString = connectionString,
        IsReadOnly = isReadOnly
    };
}