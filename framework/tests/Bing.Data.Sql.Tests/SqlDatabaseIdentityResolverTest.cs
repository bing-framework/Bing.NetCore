using Bing.Data.Enums;
using Bing.Data.Sql;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL 数据库物理身份解析测试。
/// </summary>
public class SqlDatabaseIdentityResolverTest
{
    /// <summary>
    /// 测试 - 同服务器不同数据库应识别为不同数据库。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqlServerCatalogDiffers_ShouldReturnDifferentIdentities()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(DatabaseType.SqlServer,
            "Server=sql01;Initial Catalog=orders;User Id=app;Password=secret;");
        var second = resolver.Resolve(DatabaseType.SqlServer,
            "Data Source=sql01;Database=reporting;User Id=app;Password=secret;");

        // Assert
        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 测试 - 连接字符串字段顺序不同但数据库相同时应识别为相同数据库。
    /// </summary>
    [Fact]
    public void Resolve_WhenConnectionStringOrderDiffers_ShouldReturnSameIdentity()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(DatabaseType.MySql,
            "Server=mysql01;Port=3306;Database=orders;User Id=app;Password=secret;");
        var second = resolver.Resolve(DatabaseType.MySql,
            "Password=other;Database=orders;Port=3306;Server=mysql01;User Id=another;");

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 测试 - 连接池参数不同不应影响数据库身份。
    /// </summary>
    [Fact]
    public void Resolve_WhenPoolOptionsDiffer_ShouldReturnSameIdentity()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(DatabaseType.PgSql,
            "Host=pg01;Port=5432;Database=orders;Pooling=true;Timeout=10;");
        var second = resolver.Resolve(DatabaseType.PgSql,
            "Database=orders;Host=pg01;Port=5432;Pooling=false;Timeout=30;Application Name=worker;");

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 测试 - SQLite 相对路径和绝对路径指向同一文件时应识别为相同数据库。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqlitePathsReferToSameFile_ShouldReturnSameIdentity()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();
        var relativePath = Path.Combine("identity-tests", "orders.db");
        var absolutePath = Path.GetFullPath(relativePath);

        // Act
        var first = resolver.Resolve(DatabaseType.Sqlite, $"Data Source={relativePath};Pooling=false;");
        var second = resolver.Resolve(DatabaseType.Sqlite, $"Data Source={absolutePath};Cache=Shared;");

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 测试 - SQLite 不同文件应识别为不同数据库。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqliteFilesDiffer_ShouldReturnDifferentIdentities()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(DatabaseType.Sqlite, "Data Source=first.db;");
        var second = resolver.Resolve(DatabaseType.Sqlite, "Data Source=second.db;");

        // Assert
        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 测试 - SQL Server 实例名称应参与数据库身份比较。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqlServerInstanceDiffers_ShouldReturnDifferentIdentities()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(DatabaseType.SqlServer, "Server=sql01\\first;Database=orders;");
        var second = resolver.Resolve(DatabaseType.SqlServer, "Server=sql01\\second;Database=orders;");

        // Assert
        Assert.NotEqual(first, second);
    }
}