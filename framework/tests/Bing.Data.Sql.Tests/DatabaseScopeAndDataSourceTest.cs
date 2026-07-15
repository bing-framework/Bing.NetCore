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
}