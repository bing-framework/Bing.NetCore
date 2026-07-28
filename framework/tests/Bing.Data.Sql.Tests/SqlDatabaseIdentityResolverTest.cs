using Bing.Data.Enums;
using Bing.Data.Sql;
using System.Data.Common;

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
    /// 测试 - SQLite 本地路径、相对文件 URI 和绝对文件 URI 指向同一文件时应识别为相同数据库。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqliteFileUriRefersToSameFile_ShouldReturnSameIdentity()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();
        var relativePath = Path.Combine("identity-tests", "uri-orders.db");
        var absolutePath = Path.GetFullPath(relativePath);
        var absoluteUriPath = absolutePath.Replace('\\', '/');

        // Act
        var local = resolver.Resolve(DatabaseType.Sqlite, $"Data Source={relativePath};");
        var relativeUri = resolver.Resolve(DatabaseType.Sqlite, $"Data Source=file:{relativePath}?cache=shared;");
        var absoluteUri = resolver.Resolve(DatabaseType.Sqlite, $"Data Source=file:///{absoluteUriPath}?mode=rwc;");

        // Assert
        Assert.Equal(local, relativeUri);
        Assert.Equal(local, absoluteUri);
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

    /// <summary>
    /// 测试目的：Oracle 相同数据源和服务名应忽略凭据等非物理身份字段。
    /// </summary>
    [Fact]
    public void Resolve_WhenOraclePhysicalIdentityMatches_ShouldReturnSameIdentity()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(DatabaseType.Oracle,
            "Data Source=oracle01;Service Name=orders;User Id=app;Password=secret;");
        var second = resolver.Resolve(DatabaseType.Oracle,
            "Password=other;Service Name=orders;Data Source=oracle01;User Id=worker;");

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 测试目的：Oracle 服务名不同时应识别为不同物理数据库。
    /// </summary>
    [Fact]
    public void Resolve_WhenOracleServiceNameDiffers_ShouldReturnDifferentIdentities()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(DatabaseType.Oracle, "Data Source=oracle01;Service Name=orders;");
        var second = resolver.Resolve(DatabaseType.Oracle, "Data Source=oracle01;Service Name=reporting;");

        // Assert
        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 测试目的：服务器型 Provider 缺少服务器地址时不能安全比较物理身份。
    /// </summary>
    [Theory]
    [InlineData(DatabaseType.SqlServer, "Database=orders;")]
    [InlineData(DatabaseType.MySql, "Database=orders;")]
    [InlineData(DatabaseType.PgSql, "Database=orders;")]
    public void Resolve_WhenServerAddressMissing_ShouldThrow(DatabaseType databaseType, string connectionString)
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(databaseType, connectionString));

        // Assert
        Assert.Contains("服务器地址", exception.Message);
        Assert.DoesNotContain(connectionString, exception.Message);
    }

    /// <summary>
    /// 测试目的：服务器型 Provider 缺少数据库名称时不能安全比较物理身份。
    /// </summary>
    [Theory]
    [InlineData(DatabaseType.SqlServer, "Server=sql01;")]
    [InlineData(DatabaseType.MySql, "Server=mysql01;")]
    [InlineData(DatabaseType.PgSql, "Host=pg01;")]
    public void Resolve_WhenDatabaseNameMissing_ShouldThrow(DatabaseType databaseType, string connectionString)
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(databaseType, connectionString));

        // Assert
        Assert.Contains("数据库名称", exception.Message);
        Assert.DoesNotContain(connectionString, exception.Message);
    }

    /// <summary>
    /// 测试目的：普通 SQLite 内存数据库应被标记为独占，不能用于 Shared 身份复用。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqliteUsesExclusiveMemory_ShouldMarkExclusiveMemory()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var identity = resolver.Resolve(DatabaseType.Sqlite, "Data Source=:memory:;");

        // Assert
        Assert.True(identity.IsExclusiveMemory);
        Assert.False(identity.IsComparable);
    }

    /// <summary>
    /// 测试目的：同名 SQLite 共享内存 URI 应识别为相同且可复用的物理身份。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqliteUsesSameNamedSharedMemory_ShouldReturnSameReusableIdentity()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(DatabaseType.Sqlite,
            "Data Source=file:identity-tests?mode=memory&cache=shared;");
        var second = resolver.Resolve(DatabaseType.Sqlite,
            "Data Source=file:identity-tests?cache=shared&mode=memory;");

        // Assert
        Assert.Equal(first, second);
        Assert.False(first.IsExclusiveMemory);
        Assert.True(first.IsComparable);
    }

    /// <summary>
    /// 测试目的：SQLite 构建器格式的命名共享内存与等价 URI 应解析为同一可复用身份。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqliteUsesBuilderNamedSharedMemory_ShouldReturnReusableIdentity()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var builderIdentity = resolver.Resolve(DatabaseType.Sqlite,
            "Data Source=identity-tests;Mode=Memory;Cache=Shared;");
        var uriIdentity = resolver.Resolve(DatabaseType.Sqlite,
            "Data Source=file:identity-tests?mode=memory&cache=shared;");

        // Assert
        Assert.Equal(builderIdentity, uriIdentity);
        Assert.Equal("identity-tests", builderIdentity.SharedMemoryName);
        Assert.False(builderIdentity.IsExclusiveMemory);
    }

    /// <summary>
    /// 测试目的：省略默认端口和显式默认端口应解析为同一服务器型数据库身份。
    /// </summary>
    [Theory]
    [InlineData(DatabaseType.MySql, "Server=mysql01;Database=orders;", "Server=mysql01;Port=3306;Database=orders;")]
    [InlineData(DatabaseType.PgSql, "Host=pgsql01;Database=orders;", "Host=pgsql01;Port=5432;Database=orders;")]
    [InlineData(DatabaseType.SqlServer, "Server=sql01;Database=orders;", "Server=sql01;Port=1433;Database=orders;")]
    public void Resolve_WhenDefaultPortIsOmitted_ShouldNormalizeToExplicitDefaultPort(DatabaseType databaseType,
        string withoutPort, string withPort)
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(databaseType, withoutPort);
        var second = resolver.Resolve(databaseType, withPort);

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 测试 - Oracle 省略默认端口时应与显式默认端口使用同一物理身份。
    /// </summary>
    [Fact]
    public void Resolve_WhenOracleDefaultPortIsOmitted_ShouldNormalizeToExplicitDefaultPort()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(DatabaseType.Oracle, "Data Source=oracle01/orders;");
        var second = resolver.Resolve(DatabaseType.Oracle, "Data Source=oracle01;Port=1521;Service Name=orders;");

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 测试目的：SQL Server 的 tcp 前缀和逗号端口端点应与等价字段格式解析为相同身份。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqlServerUsesTcpEndpoint_ShouldNormalizeHostAndPort()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var endpointIdentity = resolver.Resolve(DatabaseType.SqlServer,
            "Server=tcp:sql01,1433;Database=orders;");
        var fieldIdentity = resolver.Resolve(DatabaseType.SqlServer,
            "Server=sql01;Port=1433;Database=orders;");

        // Assert
        Assert.Equal(endpointIdentity, fieldIdentity);
        Assert.Equal("sql01", endpointIdentity.Server);
        Assert.Equal(1433, endpointIdentity.Port);
    }

    /// <summary>
    /// 测试 - SQL Server TCP IPv6 端点应与字段形式使用同一物理身份。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqlServerUsesTcpIpv6Endpoint_ShouldNormalizeHostAndPort()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var tcpIdentity = resolver.Resolve(DatabaseType.SqlServer,
            "Server=tcp:[::1],1433;Database=orders;");
        var endpointIdentity = resolver.Resolve(DatabaseType.SqlServer,
            "Server=[::1],1433;Database=orders;");
        var fieldIdentity = resolver.Resolve(DatabaseType.SqlServer,
            "Server=::1;Port=1433;Database=orders;");

        // Assert
        Assert.Equal(tcpIdentity, endpointIdentity);
        Assert.Equal(tcpIdentity, fieldIdentity);
        Assert.Equal("::1", tcpIdentity.Server);
    }

    /// <summary>
    /// 测试目的：Oracle EZConnect 端点和等价主机服务名格式应解析为同一身份。
    /// </summary>
    [Fact]
    public void Resolve_WhenOracleUsesEzConnectEndpoint_ShouldNormalizeHostPortAndServiceName()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var endpointIdentity = resolver.Resolve(DatabaseType.Oracle,
            "Data Source=//oracle01:1521/orders;");
        var fieldIdentity = resolver.Resolve(DatabaseType.Oracle,
            "Data Source=oracle01;Service Name=orders;");

        // Assert
        Assert.Equal(endpointIdentity, fieldIdentity);
        Assert.True(endpointIdentity.IsComparable);
    }

    /// <summary>
    /// 测试目的：普通主机和 EZConnect 同时给出服务名与 SID 时目标存在歧义，不能用于 Shared 身份比较。
    /// </summary>
    [Theory]
    [InlineData("Data Source=oracle01;Service Name=orders;SID=ORCL;")]
    [InlineData("Data Source=//oracle01:1521/orders;SID=ORCL;")]
    public void Resolve_WhenOracleServiceNameAndSidAreBothSpecified_ShouldMarkIdentityAsNotComparable(
        string connectionString)
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var identity = resolver.Resolve(DatabaseType.Oracle, connectionString);

        // Assert
        Assert.False(identity.IsComparable);
    }

    /// <summary>
    /// 测试目的：单地址 TCP TNS Descriptor 应在字段顺序和大小写不同时解析为可比较的服务名身份。
    /// </summary>
    [Fact]
    public void Resolve_WhenOracleUsesSingleAddressTnsDescriptorWithServiceName_ShouldReturnComparableIdentity()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var identity = resolver.Resolve(DatabaseType.Oracle,
            "Data Source=(description=(connect_data=(service_name=orders))(address=(port=1521)(host=oracle01)(protocol=tcp)));");

        // Assert
        Assert.True(identity.IsComparable);
        Assert.Equal("oracle01", identity.Server);
        Assert.Equal(1521, identity.Port);
        Assert.Equal("orders", identity.ServiceName);
        Assert.Null(identity.Sid);
    }

    /// <summary>
    /// 测试目的：单地址 TCP TNS Descriptor 使用 SID 时应解析为可比较的 SID 身份。
    /// </summary>
    [Fact]
    public void Resolve_WhenOracleUsesSingleAddressTnsDescriptorWithSid_ShouldReturnComparableIdentity()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var identity = resolver.Resolve(DatabaseType.Oracle,
            "Data Source=(DESCRIPTION=(ADDRESS=(HOST=oracle02)(PROTOCOL=TCP)(PORT=1522))(CONNECT_DATA=(SID=ORCL)));");

        // Assert
        Assert.True(identity.IsComparable);
        Assert.Equal("oracle02", identity.Server);
        Assert.Equal(1522, identity.Port);
        Assert.Null(identity.ServiceName);
        Assert.Equal("ORCL", identity.Sid);
    }

    /// <summary>
    /// 测试目的：存在多个地址、复杂结构、重复字段或歧义目标的 TNS Descriptor 必须拒绝物理身份比较。
    /// </summary>
    [Theory]
    [InlineData("(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle01)(PORT=1521))(ADDRESS=(PROTOCOL=TCP)(HOST=oracle02)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=orders)))")]
    [InlineData("(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle01)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=orders))(CONNECT_DATA=(SID=ORCL)))")]
    [InlineData("(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle01)(PORT=1521)))(CONNECT_DATA=(SERVICE_NAME=orders)))")]
    [InlineData("(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle01)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=orders)(SID=ORCL)))")]
    [InlineData("(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracle01)(HOST=oracle02)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=orders)))")]
    [InlineData("(DESCRIPTION=(ADDRESS=(PROTOCOL=IPC)(HOST=oracle01)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=orders)))")]
    public void Resolve_WhenOracleTnsDescriptorIsAmbiguous_ShouldMarkIdentityAsNotComparable(string descriptor)
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var identity = resolver.Resolve(DatabaseType.Oracle, $"Data Source={descriptor};");

        // Assert
        Assert.False(identity.IsComparable);
        Assert.Null(identity.OracleAlias);
    }

    /// <summary>
    /// 测试目的：无法展开的 Oracle 别名必须标记为不可安全比较，避免 Shared 模式误复用连接。
    /// </summary>
    [Fact]
    public void Resolve_WhenOracleUsesUnresolvedAlias_ShouldMarkIdentityAsNotComparable()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var identity = resolver.Resolve(DatabaseType.Oracle, "Data Source=OrdersAlias;");

        // Assert
        Assert.False(identity.IsComparable);
        Assert.Equal("OrdersAlias", identity.OracleAlias);
    }

    /// <summary>
    /// 测试目的：不同命名 SQLite 共享内存 URI 应识别为不同物理数据库。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqliteSharedMemoryNamesDiffer_ShouldReturnDifferentIdentities()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver();

        // Act
        var first = resolver.Resolve(DatabaseType.Sqlite,
            "Data Source=file:identity-first?mode=memory&cache=shared;");
        var second = resolver.Resolve(DatabaseType.Sqlite,
            "Data Source=file:identity-second?mode=memory&cache=shared;");

        // Assert
        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 测试 - 外部贡献器应优先于默认贡献器处理同一种数据库类型。
    /// </summary>
    [Fact]
    public void Resolve_WhenCustomContributorIsRegisteredAfterDefault_ShouldUseCustomContributor()
    {
        // Arrange
        var resolver = new DefaultSqlDatabaseIdentityResolver(new ISqlDatabaseIdentityContributor[]
        {
            new DefaultSqlDatabaseIdentityContributor(),
            new TestSqlServerIdentityContributor()
        });

        // Act
        var identity = resolver.Resolve(DatabaseType.SqlServer, "Server=sql01;Database=orders;");

        // Assert
        Assert.Equal("custom-sql-server", identity.Server);
    }

    /// <summary>
    /// 测试用 SQL Server 身份贡献器。
    /// </summary>
    private sealed class TestSqlServerIdentityContributor : ISqlDatabaseIdentityContributor
    {
        /// <inheritdoc />
        public bool CanResolve(DatabaseType databaseType) => databaseType == DatabaseType.SqlServer;

        /// <inheritdoc />
        public SqlDatabaseIdentity Resolve(DatabaseType databaseType, DbConnectionStringBuilder builder) => new()
        {
            DatabaseType = databaseType,
            Server = "custom-sql-server",
            Database = "custom"
        };
    }
}