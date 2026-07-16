using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 数据库作用域与数据源解析测试
/// </summary>
public class DatabaseScopeAndDataSourceTest
{
    /// <summary>
    /// 测试 - 仅传入 dbKey 时应解析数据库类型和连接字符串。
    /// </summary>
    [Fact]
    public void Resolve_WhenDataSourceConfigured_ShouldReturnDescriptorByDbKey()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.MySql,
            ConnectionString = "Server=reporting;Database=test;",
            IsReadOnly = true,
            MappingProfile = "reporting-v2"
        };
        var resolver = new DefaultSqlDataSourceResolver(options);

        // Act
        var result = resolver.Resolve("reporting");

        // Assert
        Assert.Equal("reporting", result.Key);
        Assert.Equal(DatabaseType.MySql, result.DatabaseType);
        Assert.Equal("Server=reporting;Database=test;", result.ConnectionString);
        Assert.True(result.IsReadOnly);
        Assert.Equal("reporting-v2", result.MappingProfile);
    }

    /// <summary>
    /// 测试 - 显式数据源不存在时应立即抛出异常。
    /// </summary>
    [Fact]
    public void Resolve_WhenExplicitDbKeyMissing_ShouldThrow()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["default"] = new SqlDataSourceDescriptor
        {
            Key = "default",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=default;Database=test;"
        };
        var resolver = new DefaultSqlDataSourceResolver(options);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("archive"));

        // Assert
        Assert.Contains("archive", exception.Message);
        Assert.Contains("default", exception.Message);
    }

    /// <summary>
    /// 测试 - 数据库作用域应使用数据源解析结果并恢复父级上下文。
    /// </summary>
    [Fact]
    public void Use_WhenDataSourceConfigured_ShouldSetContextAndRestoreParent()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor
        {
            Current = new DatabaseContext
            {
                DbKey = "default",
                TenantId = "tenant-a",
                MappingProfile = "default-profile"
            }
        };
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.MySql,
            ConnectionString = "Server=reporting;Database=test;",
            IsReadOnly = true,
            MappingProfile = "reporting-profile"
        };
        var manager = new DatabaseScopeManager(accessor, options);

        // Act
        using (manager.Use("reporting"))
        {
            // Assert
            Assert.Equal("reporting", accessor.Current.DbKey);
            Assert.Equal(DatabaseType.MySql, accessor.Current.DataSource.DatabaseType);
            Assert.True(accessor.Current.DataSource.IsReadOnly);
            Assert.Equal("reporting-profile", accessor.Current.MappingProfile);
            Assert.Equal("tenant-a", accessor.Current.TenantId);
        }

        Assert.Equal("default", accessor.Current.DbKey);
        Assert.Equal("default-profile", accessor.Current.MappingProfile);
    }

    /// <summary>
    /// 测试 - 未指定数据源时应使用默认数据源。
    /// </summary>
    [Fact]
    public void Resolve_WhenDbKeyNotSpecified_ShouldUseDefaultDataSource()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["default"] = new SqlDataSourceDescriptor
        {
            Key = "default",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=default;Database=test;"
        };
        options.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.MySql,
            ConnectionString = "Server=reporting;Database=test;"
        };
        var resolver = new DefaultSqlDataSourceResolver(options);

        // Act
        var result = resolver.Resolve();

        // Assert
        Assert.Equal("default", result.Key);
        Assert.Equal(DatabaseType.SqlServer, result.DatabaseType);
    }

    /// <summary>
    /// 测试 - 默认数据源未配置时应使用唯一数据源。
    /// </summary>
    [Fact]
    public void Resolve_WhenOnlyOneDataSourceConfigured_ShouldUseUniqueDataSource()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.MySql
        };
        var resolver = new DefaultSqlDataSourceResolver(options);

        // Act
        var result = resolver.Resolve();

        // Assert
        Assert.Equal("reporting", result.Key);
        Assert.Equal(DatabaseType.MySql, result.DatabaseType);
    }

    /// <summary>
    /// 测试 - 默认数据库上下文不应写入历史默认连接名称。
    /// </summary>
    [Fact]
    public void SqlMetadataOptions_DefaultContext_ShouldNotInjectLegacyDefaultDbKey()
    {
        // Arrange
        var options = new SqlMetadataOptions();

        // Act
        var context = options.DefaultDatabaseContext;

        // Assert
        Assert.NotNull(context);
        Assert.True(string.IsNullOrWhiteSpace(context.DbKey));
        Assert.Null(context.DataSource);
    }

    /// <summary>
    /// 测试 - 多数据源且默认数据源未配置时应抛出异常。
    /// </summary>
    [Fact]
    public void Resolve_WhenMultipleDataSourcesWithoutConfiguredDefault_ShouldThrow()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.DataSources.DefaultDataSourceKey = null;
        options.DataSources.DataSources["default"] = new SqlDataSourceDescriptor
        {
            Key = "default",
            DatabaseType = DatabaseType.SqlServer
        };
        options.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.MySql
        };
        var resolver = new DefaultSqlDataSourceResolver(options);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve());

        // Assert
        Assert.Contains("default,reporting", exception.Message);
        Assert.Contains(nameof(SqlDataSourceOptions.DefaultDataSourceKey), exception.Message);
    }

    /// <summary>
    /// 测试 - 异步子任务切换数据库不应影响父上下文。
    /// </summary>
    [Fact]
    public async Task Use_WhenAsyncChildChangesDatabase_ShouldNotChangeParentContext()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());

        // Act
        using (manager.Use("default"))
        {
            await ChangeDatabaseAsync(manager, accessor, "mysql");

            // Assert
            Assert.Equal("default", accessor.Current.DbKey);
            Assert.Equal(DatabaseType.Sqlite, accessor.Current.DataSource.DatabaseType);
        }

        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 测试 - Task.WhenAll 中不同数据库上下文应相互隔离。
    /// </summary>
    [Fact]
    public async Task Use_WhenParallelTasksChangeDatabase_ShouldKeepContextsIsolated()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());

        // Act
        using (manager.Use("default"))
        {
            var results = await Task.WhenAll(
                CaptureDatabaseAsync(manager, accessor, "mysql"),
                CaptureDatabaseAsync(manager, accessor, "pgsql"));

            // Assert
            Assert.Contains("mysql:MySql", results);
            Assert.Contains("pgsql:PgSql", results);
            Assert.Equal("default", accessor.Current.DbKey);
            Assert.Equal(DatabaseType.Sqlite, accessor.Current.DataSource.DatabaseType);
        }
    }

    /// <summary>
    /// 测试 - 嵌套异步作用域释放后应恢复上一层上下文。
    /// </summary>
    [Fact]
    public async Task Use_WhenNestedAsyncScopeDisposed_ShouldRestoreParentContext()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());

        // Act
        using (manager.Use("default"))
        {
            await Task.Yield();
            using (manager.Use("mysql"))
            {
                await Task.Yield();
                Assert.Equal("mysql", accessor.Current.DbKey);
            }

            // Assert
            Assert.Equal("default", accessor.Current.DbKey);
        }
    }

    /// <summary>
    /// 测试 - 多次释放数据库作用域不应重复修改上下文。
    /// </summary>
    [Fact]
    public void Use_WhenScopeDisposedMultipleTimes_ShouldRestoreContextOnce()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());
        using var parent = manager.Use("default");
        var scope = manager.Use("mysql");

        // Act
        scope.Dispose();
        scope.Dispose();

        // Assert
        Assert.Equal("default", accessor.Current.DbKey);
    }

    /// <summary>
    /// 测试 - 不同数据库上下文访问器不应相互影响。
    /// </summary>
    [Fact]
    public async Task Use_WhenAccessorsAreDifferent_ShouldKeepContextsIsolated()
    {
        // Arrange
        var firstAccessor = new AsyncLocalDatabaseContextAccessor();
        var secondAccessor = new AsyncLocalDatabaseContextAccessor();
        var options = CreateMultiDataSourceOptions();
        var firstManager = new DatabaseScopeManager(firstAccessor, options);
        var secondManager = new DatabaseScopeManager(secondAccessor, options);

        // Act
        using (firstManager.Use("mysql"))
        {
            await Task.Yield();
            using (secondManager.Use("pgsql"))
            {
                await Task.Yield();

                // Assert
                Assert.Equal("mysql", firstAccessor.Current.DbKey);
                Assert.Equal("pgsql", secondAccessor.Current.DbKey);
            }

            Assert.Null(secondAccessor.Current);
        }
    }

    /// <summary>
    /// 测试 - 未设置数据库上下文时应返回空上下文。
    /// </summary>
    [Fact]
    public void Current_WhenNoScopeUsed_ShouldReturnNull()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();

        // Act
        var context = accessor.Current;

        // Assert
        Assert.Null(context);
    }

    /// <summary>
    /// 测试 - 嵌套读取偏好作用域释放后应恢复父级偏好。
    /// </summary>
    [Fact]
    public async Task UseReadPreference_WhenNestedScopesDisposed_ShouldRestoreParentPreference()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var databaseManager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());
        var readPreferenceManager = new ReadPreferenceScopeManager(accessor);

        // Act
        using (databaseManager.Use("default"))
        using (readPreferenceManager.Use(SqlReadPreference.Primary))
        {
            await Task.Yield();
            Assert.Equal(SqlReadPreference.Primary, accessor.Current.ReadPreference);

            using (readPreferenceManager.Use(SqlReadPreference.Default))
            {
                await Task.Yield();
                Assert.Equal(SqlReadPreference.Default, accessor.Current.ReadPreference);
            }

            // Assert
            Assert.Equal(SqlReadPreference.Primary, accessor.Current.ReadPreference);
            Assert.Equal("default", accessor.Current.DbKey);
        }
    }

    /// <summary>
    /// 测试 - 并行读取偏好作用域应相互隔离。
    /// </summary>
    [Fact]
    public async Task UseReadPreference_WhenParallelTasksExecute_ShouldKeepPreferencesIsolated()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var databaseManager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());
        var readPreferenceManager = new ReadPreferenceScopeManager(accessor);

        // Act
        using (databaseManager.Use("default"))
        {
            var preferences = await Task.WhenAll(
                CaptureReadPreferenceAsync(readPreferenceManager, accessor, SqlReadPreference.Primary),
                CaptureReadPreferenceAsync(readPreferenceManager, accessor, SqlReadPreference.Default));

            // Assert
            Assert.Contains(SqlReadPreference.Primary, preferences);
            Assert.Contains(SqlReadPreference.Default, preferences);
            Assert.Equal(SqlReadPreference.Default, accessor.Current.ReadPreference);
        }
    }

    /// <summary>
    /// 切换并读取指定数据库上下文。
    /// </summary>
    /// <param name="manager">数据库上下文作用域管理器。</param>
    /// <param name="accessor">数据库上下文访问器。</param>
    /// <param name="dbKey">数据库标识。</param>
    /// <returns>异步任务。</returns>
    private static async Task ChangeDatabaseAsync(IDatabaseScopeManager manager,
        IDatabaseContextAccessor accessor, string dbKey)
    {
        using (manager.Use(dbKey))
        {
            await Task.Yield();
            Assert.Equal(dbKey, accessor.Current.DbKey);
        }
    }

    /// <summary>
    /// 切换并捕获数据库上下文。
    /// </summary>
    /// <param name="manager">数据库上下文作用域管理器。</param>
    /// <param name="accessor">数据库上下文访问器。</param>
    /// <param name="dbKey">数据库标识。</param>
    /// <returns>数据库上下文摘要。</returns>
    private static async Task<string> CaptureDatabaseAsync(IDatabaseScopeManager manager,
        IDatabaseContextAccessor accessor, string dbKey)
    {
        using (manager.Use(dbKey))
        {
            await Task.Yield();
            return $"{accessor.Current.DbKey}:{accessor.Current.DataSource.DatabaseType}";
        }
    }

    /// <summary>
    /// 切换并捕获读取偏好。
    /// </summary>
    /// <param name="manager">读取偏好作用域管理器。</param>
    /// <param name="accessor">数据库上下文访问器。</param>
    /// <param name="readPreference">读取偏好。</param>
    /// <returns>读取偏好。</returns>
    private static async Task<SqlReadPreference> CaptureReadPreferenceAsync(IReadPreferenceScopeManager manager,
        IDatabaseContextAccessor accessor, SqlReadPreference readPreference)
    {
        using (manager.Use(readPreference))
        {
            await Task.Yield();
            return accessor.Current.ReadPreference;
        }
    }

    /// <summary>
    /// 创建多数据源测试配置。
    /// </summary>
    /// <returns>SQL 元数据配置。</returns>
    private static SqlMetadataOptions CreateMultiDataSourceOptions()
    {
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["default"] = new SqlDataSourceDescriptor
        {
            Key = "default",
            DatabaseType = DatabaseType.Sqlite,
            ConnectionString = "Data Source=default.db"
        };
        options.DataSources.DataSources["mysql"] = new SqlDataSourceDescriptor
        {
            Key = "mysql",
            DatabaseType = DatabaseType.MySql,
            ConnectionString = "Server=mysql;Database=test;"
        };
        options.DataSources.DataSources["pgsql"] = new SqlDataSourceDescriptor
        {
            Key = "pgsql",
            DatabaseType = DatabaseType.PgSql,
            ConnectionString = "Host=pgsql;Database=test;"
        };
        return options;
    }
}