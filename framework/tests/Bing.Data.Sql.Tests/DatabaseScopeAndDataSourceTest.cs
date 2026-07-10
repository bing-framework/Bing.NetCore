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
    /// 测试目的：当配置新数据源时，解析器应仅通过 dbKey 返回数据源描述。
    /// </summary>
    [Fact]
    public void Resolve_WhenDataSourceConfigured_ShouldReturnDescriptorByDbKey()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DbKey = "reporting",
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
    /// 测试目的：当只配置旧 Databases 时，数据源解析器应兼容旧描述信息。
    /// </summary>
    [Fact]
    public void Resolve_WhenLegacyDatabaseConfigured_ShouldAdaptDescriptor()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.Databases[SqlMetadataOptions.GetDatabaseDescriptorKey("archive", DatabaseType.PgSql)] =
            new DatabaseDescriptor
            {
                DbKey = "archive",
                DatabaseType = DatabaseType.PgSql,
                ConnectionString = "Host=archive;Database=test;",
                ReadOnly = true
            };
        var resolver = new DefaultSqlDataSourceResolver(options);

        // Act
        var result = resolver.Resolve("archive", new DatabaseScopeOptions { DatabaseType = DatabaseType.PgSql });

        // Assert
        Assert.Equal("archive", result.Key);
        Assert.Equal(DatabaseType.PgSql, result.DatabaseType);
        Assert.Equal("Host=archive;Database=test;", result.ConnectionString);
        Assert.True(result.IsReadOnly);
    }

    /// <summary>
    /// 测试目的：显式传入未配置 dbKey 时，不应被默认库覆盖。
    /// </summary>
    [Fact]
    public void Resolve_WhenExplicitDbKeyMissing_ShouldKeepRequestedDbKey()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.DefaultDatabaseContext = new DatabaseContext
        {
            DbKey = "default",
            DatabaseType = DatabaseType.SqlServer
        };
        var resolver = new DefaultSqlDataSourceResolver(options);

        // Act
        var result = resolver.Resolve("reporting", new DatabaseScopeOptions { DatabaseType = DatabaseType.Oracle });

        // Assert
        Assert.Equal("reporting", result.Key);
        Assert.Equal("reporting", result.DbKey);
        Assert.Equal(DatabaseType.Oracle, result.DatabaseType);
    }

    /// <summary>
    /// 测试目的：数据库作用域应使用新数据源解析结果，并在释放后恢复父级上下文。
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
                DatabaseType = DatabaseType.SqlServer,
                TenantId = "tenant-a",
                MappingProfile = "default-profile"
            }
        };
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DbKey = "reporting",
            DatabaseType = DatabaseType.MySql,
            IsReadOnly = true,
            MappingProfile = "reporting-profile"
        };
        var manager = new DatabaseScopeManager(accessor, options);

        // Act
        using (manager.Use("reporting"))
        {
            // Assert
            Assert.Equal("reporting", accessor.Current.DbKey);
            Assert.Equal(DatabaseType.MySql, accessor.Current.DatabaseType);
            Assert.True(accessor.Current.ReadOnly);
            Assert.Equal("reporting-profile", accessor.Current.MappingProfile);
            Assert.Equal("tenant-a", accessor.Current.TenantId);
        }

        Assert.Equal("default", accessor.Current.DbKey);
        Assert.Equal("default-profile", accessor.Current.MappingProfile);
    }
}