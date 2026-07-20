using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库物理身份。
/// </summary>
public sealed class SqlDatabaseIdentity : IEquatable<SqlDatabaseIdentity>
{
    /// <summary>
    /// 数据库类型。
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 数据库服务器或数据源端点。
    /// </summary>
    public string Server { get; set; }

    /// <summary>
    /// 数据库服务端口。
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// 数据库名称或目录。
    /// </summary>
    public string Database { get; set; }

    /// <summary>
    /// 数据库实例名称。
    /// </summary>
    public string Instance { get; set; }

    /// <summary>
    /// SQLite 数据库文件路径或内存数据库标识。
    /// </summary>
    public string FilePath { get; set; }

    /// <summary>
    /// Oracle 服务名称或 SID。
    /// </summary>
    public string ServiceName { get; set; }

    /// <summary>
    /// Oracle SID。
    /// </summary>
    public string Sid { get; set; }

    /// <summary>
    /// Oracle 未展开的数据源别名。
    /// </summary>
    public string OracleAlias { get; set; }

    /// <summary>
    /// SQLite 命名共享内存数据库名称。
    /// </summary>
    public string SharedMemoryName { get; set; }

    /// <summary>
    /// 是否可以安全比较物理身份。
    /// </summary>
    public bool IsComparable { get; set; } = true;

    /// <summary>
    /// 是否为仅当前连接可见的 SQLite 独占内存数据库。
    /// </summary>
    /// <remarks>
    /// 独占内存数据库不能作为 EF Core Shared 模式的可复用物理身份。
    /// </remarks>
    public bool IsExclusiveMemory { get; set; }

    /// <inheritdoc />
    public bool Equals(SqlDatabaseIdentity other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return DatabaseType == other.DatabaseType &&
               string.Equals(Server, other.Server, StringComparison.OrdinalIgnoreCase) &&
               Port == other.Port &&
               string.Equals(Database, other.Database, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Instance, other.Instance, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ServiceName, other.ServiceName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Sid, other.Sid, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(OracleAlias, other.OracleAlias, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(SharedMemoryName, other.SharedMemoryName, StringComparison.OrdinalIgnoreCase) &&
               IsExclusiveMemory == other.IsExclusiveMemory &&
               IsComparable == other.IsComparable;
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => Equals(obj as SqlDatabaseIdentity);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var hash = 17;
        hash = hash * 31 + DatabaseType.GetHashCode();
        hash = hash * 31 + (Server == null ? 0 : comparer.GetHashCode(Server));
        hash = hash * 31 + Port.GetHashCode();
        hash = hash * 31 + (Database == null ? 0 : comparer.GetHashCode(Database));
        hash = hash * 31 + (Instance == null ? 0 : comparer.GetHashCode(Instance));
        hash = hash * 31 + (FilePath == null ? 0 : comparer.GetHashCode(FilePath));
        hash = hash * 31 + (ServiceName == null ? 0 : comparer.GetHashCode(ServiceName));
        hash = hash * 31 + (Sid == null ? 0 : comparer.GetHashCode(Sid));
        hash = hash * 31 + (OracleAlias == null ? 0 : comparer.GetHashCode(OracleAlias));
        hash = hash * 31 + (SharedMemoryName == null ? 0 : comparer.GetHashCode(SharedMemoryName));
        hash = hash * 31 + IsExclusiveMemory.GetHashCode();
        hash = hash * 31 + IsComparable.GetHashCode();
        return hash;
    }
}