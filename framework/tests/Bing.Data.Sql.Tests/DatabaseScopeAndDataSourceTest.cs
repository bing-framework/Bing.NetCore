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
    /// 测试目的：数据库作用域未指定读取偏好时应继承父级 Primary 偏好并继续路由到主库。
    /// </summary>
    [Fact]
    public void Use_WhenReadPreferenceIsNotSpecified_ShouldInheritParentPreference()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreatePrimaryReadOptions());

        // Act
        using (manager.Use(new DatabaseScopeOptions { DbKey = "read", ReadPreference = SqlReadPreference.Primary }))
        {
            using (manager.Use(new DatabaseScopeOptions { DbKey = "read" }))
            {
                // Assert
                Assert.Equal("primary", accessor.Current.DbKey);
                Assert.Equal(SqlReadPreference.Primary, accessor.Current.ReadPreference);
            }

            Assert.Equal("primary", accessor.Current.DbKey);
            Assert.Equal(SqlReadPreference.Primary, accessor.Current.ReadPreference);
        }
    }

    /// <summary>
    /// 测试目的：嵌套作用域仅变更租户或读取偏好时必须继承父级已解析的数据源，避免重新路由到默认数据源。
    /// </summary>
    [Fact]
    public void Use_WhenNestedScopeDoesNotSpecifyDbKey_ShouldInheritParentDataSource()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());

        // Act
        using (manager.Use("mysql"))
        {
            using (manager.Use(new DatabaseScopeOptions { TenantId = "tenant-a" }))
            {
                // Assert
                Assert.Equal("mysql", accessor.Current.DbKey);
                Assert.Equal("mysql", accessor.Current.DataSource.Key);
                Assert.Equal("tenant-a", accessor.Current.TenantId);
            }

            using (manager.Use(new DatabaseScopeOptions { ReadPreference = SqlReadPreference.Primary }))
            {
                // Assert
                Assert.Equal("mysql", accessor.Current.DbKey);
                Assert.Equal("mysql", accessor.Current.DataSource.Key);
                Assert.Equal(SqlReadPreference.Primary, accessor.Current.ReadPreference);
            }
        }
    }

    /// <summary>
    /// 测试目的：数据库作用域显式 Default 偏好应覆盖父级 Primary 偏好并在释放后恢复。
    /// </summary>
    [Fact]
    public void Use_WhenReadPreferenceIsExplicitDefault_ShouldOverrideAndRestoreParentPreference()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreatePrimaryReadOptions());

        // Act
        using (manager.Use(new DatabaseScopeOptions { DbKey = "read", ReadPreference = SqlReadPreference.Primary }))
        {
            using (manager.Use(new DatabaseScopeOptions { DbKey = "read", ReadPreference = SqlReadPreference.Default }))
            {
                // Assert
                Assert.Equal("read", accessor.Current.DbKey);
                Assert.Equal(SqlReadPreference.Default, accessor.Current.ReadPreference);
            }

            Assert.Equal("primary", accessor.Current.DbKey);
            Assert.Equal(SqlReadPreference.Primary, accessor.Current.ReadPreference);
        }
    }

    /// <summary>
    /// 测试目的：主从数据源 Provider 不一致时必须拒绝切换到主库。
    /// </summary>
    [Fact]
    public void Resolve_WhenPrimaryDataSourceProviderDiffers_ShouldThrow()
    {
        // Arrange
        var options = CreatePrimaryReadOptions();
        options.DataSources.DataSources["primary"].DatabaseType = DatabaseType.MySql;
        var resolver = new DefaultSqlDataSourceResolver(options);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("read",
            new DatabaseScopeOptions { ReadPreference = SqlReadPreference.Primary }));

        // Assert
        Assert.Contains("read", exception.Message);
        Assert.Contains("primary", exception.Message);
        Assert.Contains("Provider", exception.Message);
    }

    /// <summary>
    /// 测试目的：Doris 使用 MySQL Provider 且不支持本地事务时仍可作为主从数据源路由。
    /// </summary>
    [Fact]
    public void Resolve_WhenDorisUsesMySqlProvider_ShouldAllowPrimaryRouting()
    {
        // Arrange
        var options = CreatePrimaryReadOptions();
        options.DataSources.DataSources["primary"].DatabaseType = DatabaseType.MySql;
        options.DataSources.DataSources["read"].DatabaseType = DatabaseType.MySql;
        options.DataSources.DataSources["read"].SupportsTransactions = false;
        var resolver = new DefaultSqlDataSourceResolver(options);

        // Act
        var result = resolver.Resolve("read", new DatabaseScopeOptions
        {
            ReadPreference = SqlReadPreference.Primary
        });

        // Assert
        Assert.Equal("primary", result.Key);
        Assert.Equal(DatabaseType.MySql, result.DatabaseType);
    }

    /// <summary>
    /// 测试目的：作用域乱序释放应抛出异常且不能破坏当前栈顶上下文。
    /// </summary>
    [Fact]
    public void Use_WhenScopesAreDisposedOutOfOrder_ShouldThrowAndKeepTopContext()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());
        var parent = manager.Use("default");
        var child = manager.Use("mysql");

        try
        {
            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => parent.Dispose());

            // Assert
            Assert.Contains("LIFO", exception.Message);
            Assert.Equal("mysql", accessor.Current.DbKey);
        }
        finally
        {
            child.Dispose();
            parent.Dispose();
        }
    }

    /// <summary>
    /// 测试目的：读取 Current 返回的快照被修改时不应回写访问器内部上下文。
    /// </summary>
    [Fact]
    public void Current_WhenReturnedSnapshotIsMutated_ShouldNotChangeAccessorContext()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor
        {
            Current = new DatabaseContext
            {
                DbKey = "default",
                DataSource = new SqlDataSourceDescriptor
                {
                    Key = "default",
                    DatabaseType = DatabaseType.Sqlite
                }
            }
        };

        // Act
        var snapshot = accessor.Current;
        snapshot.DbKey = "changed";
        snapshot.DataSource.Key = "changed";

        // Assert
        Assert.Equal("default", accessor.Current.DbKey);
        Assert.Equal("default", accessor.Current.DataSource.Key);
    }

    /// <summary>
    /// 测试目的：Update 应将返回的新上下文写回访问器并返回独立快照。
    /// </summary>
    [Fact]
    public void Update_WhenUpdaterReturnsContext_ShouldWriteBackUpdatedSnapshot()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor
        {
            Current = new DatabaseContext { DbKey = "default", TenantId = "tenant-a" }
        };

        // Act
        var result = accessor.Update(context =>
        {
            context.DbKey = "mysql";
            return context;
        });
        result.DbKey = "changed";

        // Assert
        Assert.Equal("mysql", accessor.Current.DbKey);
        Assert.Equal("tenant-a", accessor.Current.TenantId);
    }

    /// <summary>
    /// 测试目的：子执行流释放继承的数据库作用域不应阻止父执行流随后恢复上下文。
    /// </summary>
    [Fact]
    public async Task Use_WhenChildFlowDisposesScope_ShouldRestoreContextInEachFlow()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());
        var scope = manager.Use("default");

        // Act
        await Task.Run(() =>
        {
            Assert.Equal("default", accessor.Current.DbKey);
            scope.Dispose();
            Assert.Null(accessor.Current);
        });

        // Assert
        Assert.Equal("default", accessor.Current.DbKey);
        scope.Dispose();
        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 测试目的：当前执行流已经释放作用域后重复释放应保持幂等，不影响后续上下文。
    /// </summary>
    [Fact]
    public async Task Use_WhenScopeIsAlreadyAbsentInCurrentFlow_ShouldRemainIdempotent()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());
        var scope = manager.Use("default");

        // Act
        await Task.Run(() => scope.Dispose());
        await Task.Run(() => scope.Dispose());

        // Assert
        Assert.Equal("default", accessor.Current.DbKey);
        scope.Dispose();
        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 测试目的：子执行流中乱序释放继承的父级作用域时应抛出异常且保持子流栈顶上下文。
    /// </summary>
    [Fact]
    public async Task Use_WhenChildFlowDisposesScopeOutOfOrder_ShouldThrowAndKeepChildContext()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());
        using var parent = manager.Use("default");

        // Act
        await Task.Run(() =>
        {
            using var child = manager.Use("mysql");
            var exception = Assert.Throws<InvalidOperationException>(() => parent.Dispose());

            // Assert
            Assert.Contains("LIFO", exception.Message);
            Assert.Equal("mysql", accessor.Current.DbKey);
        });

        Assert.Equal("default", accessor.Current.DbKey);
    }

    /// <summary>
    /// 测试目的：抑制 ExecutionContext 流转的任务不应取得或释放父级数据库作用域。
    /// </summary>
    [Fact]
    public async Task Use_WhenExecutionContextFlowIsSuppressed_ShouldNotChangeParentContext()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());
        using var scope = manager.Use("default");
        Task task;

        // Act
        using (ExecutionContext.SuppressFlow())
            task = Task.Run(() =>
            {
                Assert.Null(accessor.Current);
                scope.Dispose();
                Assert.Null(accessor.Current);
            });
        await task;

        // Assert
        Assert.Equal("default", accessor.Current.DbKey);
    }

    /// <summary>
    /// 测试 - TaskRun默认应流转当前数据库上下文。
    /// </summary>
    [Fact]
    public async Task Use_WhenTaskRunUsesDefaultExecutionContext_ShouldFlowDatabaseContext()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());

        // Act
        using (manager.Use("default"))
        {
            var dbKey = await Task.Run(() => accessor.Current?.DbKey);

            // Assert
            Assert.Equal("default", dbKey);
            Assert.Equal("default", accessor.Current.DbKey);
        }
    }

    /// <summary>
    /// 测试 - 数据库作用域内部抛出异常后应恢复父上下文。
    /// </summary>
    [Fact]
    public async Task Use_WhenChildScopeThrows_ShouldRestoreChildAndParentContexts()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());

        // Act
        using (manager.Use("default"))
        {
            var childRestoredDbKey = await Task.Run(() =>
            {
                try
                {
                    using (manager.Use("mysql"))
                    {
                        Assert.Equal("mysql", accessor.Current.DbKey);
                        throw new InvalidOperationException("test failure");
                    }
                }
                catch (InvalidOperationException)
                {
                    return accessor.Current?.DbKey;
                }
            });

            // Assert
            Assert.Equal("default", childRestoredDbKey);
            Assert.Equal("default", accessor.Current.DbKey);
        }
    }

    /// <summary>
    /// 测试 - 数据库作用域内部取消后应恢复父上下文。
    /// </summary>
    [Fact]
    public async Task Use_WhenChildScopeIsCanceled_ShouldRestoreChildAndParentContexts()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());
        var childRestoredDbKey = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        using (manager.Use("default"))
        {
            var task = Task.Run(() =>
            {
                try
                {
                    using (manager.Use("mysql"))
                    {
                        Assert.Equal("mysql", accessor.Current.DbKey);
                        throw new OperationCanceledException();
                    }
                }
                catch (OperationCanceledException)
                {
                    childRestoredDbKey.SetResult(accessor.Current?.DbKey);
                    throw;
                }
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

            // Assert
            Assert.Equal("default", await childRestoredDbKey.Task);
            Assert.Equal("default", accessor.Current.DbKey);
        }
    }

    /// <summary>
    /// 测试 - 并行任务更新TenantId不应相互污染。
    /// </summary>
    [Fact]
    public async Task Update_WhenParallelTasksChangeTenantId_ShouldKeepValuesIsolated()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor
        {
            Current = new DatabaseContext { DbKey = "default", TenantId = "parent" }
        };

        // Act
        var values = await Task.WhenAll(
            CaptureUpdatedContextAsync(accessor, "tenant-a", null),
            CaptureUpdatedContextAsync(accessor, "tenant-b", null));

        // Assert
        Assert.Contains(("tenant-a", (string)null), values);
        Assert.Contains(("tenant-b", (string)null), values);
        Assert.Equal("parent", accessor.Current.TenantId);
    }

    /// <summary>
    /// 测试 - 并行任务更新MappingProfile不应相互污染。
    /// </summary>
    [Fact]
    public async Task Update_WhenParallelTasksChangeMappingProfile_ShouldKeepValuesIsolated()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor
        {
            Current = new DatabaseContext { DbKey = "default", MappingProfile = "parent" }
        };

        // Act
        var values = await Task.WhenAll(
            CaptureUpdatedContextAsync(accessor, null, "profile-a"),
            CaptureUpdatedContextAsync(accessor, null, "profile-b"));

        // Assert
        Assert.Contains(((string)null, "profile-a"), values);
        Assert.Contains(((string)null, "profile-b"), values);
        Assert.Equal("parent", accessor.Current.MappingProfile);
    }

    /// <summary>
    /// 测试目的：并行执行流中的租户和映射配置应相互隔离，并在异常取消后恢复父级上下文。
    /// </summary>
    [Fact]
    public async Task Use_WhenParallelFlowsAreCanceled_ShouldKeepTenantAndMappingProfileIsolated()
    {
        // Arrange
        var accessor = new AsyncLocalDatabaseContextAccessor
        {
            Current = new DatabaseContext
            {
                DbKey = "default",
                TenantId = "parent-tenant",
                MappingProfile = "parent-profile"
            }
        };
        var manager = new DatabaseScopeManager(accessor, CreateMultiDataSourceOptions());

        // Act
        var first = Task.Run(async () =>
        {
            accessor.Update(context =>
            {
                context.TenantId = "tenant-a";
                context.MappingProfile = "profile-a";
                return context;
            });
            await Task.Yield();
            Assert.Equal("tenant-a", accessor.Current.TenantId);
            Assert.Equal("profile-a", accessor.Current.MappingProfile);
            throw new OperationCanceledException();
        });
        var second = Task.Run(async () =>
        {
            using (manager.Use("mysql"))
            {
                accessor.Update(context =>
                {
                    context.TenantId = "tenant-b";
                    context.MappingProfile = "profile-b";
                    return context;
                });
                await Task.Yield();
                return $"{accessor.Current.TenantId}:{accessor.Current.MappingProfile}";
            }
        });

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal("tenant-b:profile-b", await second);
        Assert.Equal("parent-tenant", accessor.Current.TenantId);
        Assert.Equal("parent-profile", accessor.Current.MappingProfile);
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
    /// 在独立执行流中更新并读取数据库上下文字段。
    /// </summary>
    /// <param name="accessor">数据库上下文访问器。</param>
    /// <param name="tenantId">待更新的租户标识。</param>
    /// <param name="mappingProfile">待更新的映射配置。</param>
    /// <returns>当前执行流读取到的租户标识与映射配置。</returns>
    private static async Task<(string TenantId, string MappingProfile)> CaptureUpdatedContextAsync(
        IDatabaseContextAccessor accessor, string tenantId, string mappingProfile)
    {
        return await Task.Run(async () =>
        {
            accessor.Update(context =>
            {
                context.TenantId = tenantId;
                context.MappingProfile = mappingProfile;
                return context;
            });
            await Task.Yield();
            return (accessor.Current.TenantId, accessor.Current.MappingProfile);
        });
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

    /// <summary>
    /// 创建主库读取策略测试配置。
    /// </summary>
    /// <returns>SQL 元数据配置。</returns>
    private static SqlMetadataOptions CreatePrimaryReadOptions()
    {
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["read"] = new SqlDataSourceDescriptor
        {
            Key = "read",
            DatabaseType = DatabaseType.Sqlite,
            ConnectionString = "Data Source=read.db",
            PrimaryReadStrategy = PrimaryReadStrategy.PrimaryDataSource,
            PrimaryDataSourceKey = "primary"
        };
        options.DataSources.DataSources["primary"] = new SqlDataSourceDescriptor
        {
            Key = "primary",
            DatabaseType = DatabaseType.Sqlite,
            ConnectionString = "Data Source=primary.db"
        };
        return options;
    }
}