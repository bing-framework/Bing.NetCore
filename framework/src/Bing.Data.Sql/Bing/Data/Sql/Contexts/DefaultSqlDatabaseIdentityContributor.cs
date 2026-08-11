using System.Data.Common;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// 内置 SQL 数据库物理身份解析贡献者。
/// </summary>
public sealed class DefaultSqlDatabaseIdentityContributor : ISqlDatabaseIdentityContributor
{
    /// <inheritdoc />
    public bool CanResolve(DatabaseType databaseType) => databaseType == DatabaseType.Sqlite ||
        databaseType == DatabaseType.SqlServer || databaseType == DatabaseType.MySql ||
        databaseType == DatabaseType.Doris ||
        databaseType == DatabaseType.PgSql || databaseType == DatabaseType.Oracle;

    /// <inheritdoc />
    public SqlDatabaseIdentity Resolve(DatabaseType databaseType, DbConnectionStringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        return databaseType switch
        {
            DatabaseType.Sqlite => ResolveSqlite(builder),
            DatabaseType.SqlServer => ResolveSqlServer(builder),
            DatabaseType.MySql or DatabaseType.Doris => ResolveServerDatabase(databaseType, builder, 3306, "Server", "Data Source", "Host"),
            DatabaseType.PgSql => ResolveServerDatabase(databaseType, builder, 5432, "Host", "Server", "Data Source"),
            DatabaseType.Oracle => ResolveOracle(builder),
            _ => throw new NotSupportedException($"数据库类型 {databaseType} 不支持物理数据库身份比较。")
        };
    }

    private static SqlDatabaseIdentity ResolveSqlServer(DbConnectionStringBuilder builder)
    {
        var endpoint = GetValue(builder, "Server", "Data Source", "DataSource", "Address", "Addr", "Network Address");
        var parsed = ParseSqlServerEndpoint(endpoint, ParsePort(GetValue(builder, "Port")));
        var database = Normalize(GetValue(builder, "Database", "Initial Catalog"));
        EnsureRequired(parsed.Server, "服务器地址");
        EnsureRequired(database, "数据库名称");
        return new SqlDatabaseIdentity
        {
            DatabaseType = DatabaseType.SqlServer,
            Server = parsed.Server,
            Instance = parsed.Instance,
            Port = parsed.Port ?? (string.IsNullOrWhiteSpace(parsed.Instance) ? 1433 : null),
            Database = database
        };
    }

    private static SqlDatabaseIdentity ResolveServerDatabase(DatabaseType databaseType,
        DbConnectionStringBuilder builder, int defaultPort, params string[] serverKeys)
    {
        var endpoint = ParseHostEndpoint(GetValue(builder, serverKeys), ParsePort(GetValue(builder, "Port")));
        var database = Normalize(GetValue(builder, "Database", "Initial Catalog"));
        EnsureRequired(endpoint.Server, "服务器地址");
        EnsureRequired(database, "数据库名称");
        return new SqlDatabaseIdentity
        {
            DatabaseType = databaseType,
            Server = endpoint.Server,
            Port = endpoint.Port ?? defaultPort,
            Database = database
        };
    }

    private static SqlDatabaseIdentity ResolveOracle(DbConnectionStringBuilder builder)
    {
        var dataSource = Normalize(GetValue(builder, "Data Source", "DataSource", "Server"));
        EnsureRequired(dataSource, "数据源");
        var serviceName = Normalize(GetValue(builder, "Service Name"));
        var sid = Normalize(GetValue(builder, "SID"));
        if (IsOracleTnsDescriptor(dataSource))
        {
            if (TryParseOracleTnsDescriptor(dataSource, out var tnsIdentity))
                return tnsIdentity;
            return new SqlDatabaseIdentity
            {
                DatabaseType = DatabaseType.Oracle,
                IsComparable = false
            };
        }
        var endpoint = ParseOracleEndpoint(dataSource);
        if (endpoint != null)
        {
            var parsedEndpoint = endpoint.Value;
            serviceName = serviceName ?? parsedEndpoint.Database;
            return new SqlDatabaseIdentity
            {
                DatabaseType = DatabaseType.Oracle,
                Server = parsedEndpoint.Server,
                Port = parsedEndpoint.Port ?? 1521,
                ServiceName = serviceName,
                Sid = sid,
                IsComparable = HasSingleOracleDatabaseTarget(serviceName, sid)
            };
        }

        if (IsSimpleHost(dataSource) && (string.IsNullOrWhiteSpace(serviceName) == false || string.IsNullOrWhiteSpace(sid) == false))
        {
            return new SqlDatabaseIdentity
            {
                DatabaseType = DatabaseType.Oracle,
                Server = dataSource,
                Port = ParsePort(GetValue(builder, "Port")) ?? 1521,
                ServiceName = serviceName,
                Sid = sid,
                IsComparable = HasSingleOracleDatabaseTarget(serviceName, sid)
            };
        }

        return new SqlDatabaseIdentity
        {
            DatabaseType = DatabaseType.Oracle,
            OracleAlias = dataSource,
            IsComparable = false
        };
    }

    private static bool HasSingleOracleDatabaseTarget(string serviceName, string sid) =>
        string.IsNullOrWhiteSpace(serviceName) != string.IsNullOrWhiteSpace(sid);

    private static SqlDatabaseIdentity ResolveSqlite(DbConnectionStringBuilder builder)
    {
        var dataSource = GetValue(builder, "Data Source", "DataSource", "Filename");
        if (string.IsNullOrWhiteSpace(dataSource))
            throw new InvalidOperationException("SQLite 连接字符串缺少 Data Source 或 Filename，无法解析物理数据库身份。");
        var normalizedSource = dataSource.Trim();
        var mode = Normalize(GetValue(builder, "Mode"));
        var cache = Normalize(GetValue(builder, "Cache"));
        var uriMode = GetSqliteUriOption(normalizedSource, "mode");
        var uriCache = GetSqliteUriOption(normalizedSource, "cache");
        var effectiveMode = mode ?? uriMode;
        var effectiveCache = cache ?? uriCache;
        var isMemory = string.Equals(normalizedSource, ":memory:", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(effectiveMode, "memory", StringComparison.OrdinalIgnoreCase);
        if (isMemory)
        {
            var name = GetSqliteMemoryName(normalizedSource);
            var isSharedMemory = string.Equals(normalizedSource, ":memory:", StringComparison.OrdinalIgnoreCase) == false &&
                                 string.IsNullOrWhiteSpace(name) == false &&
                                 string.Equals(effectiveCache, "shared", StringComparison.OrdinalIgnoreCase);
            return new SqlDatabaseIdentity
            {
                DatabaseType = DatabaseType.Sqlite,
                FilePath = isSharedMemory ? $"memory:{name}" : "memory:exclusive",
                SharedMemoryName = isSharedMemory ? name : null,
                IsExclusiveMemory = !isSharedMemory,
                IsComparable = isSharedMemory
            };
        }
        if (normalizedSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = GetSqliteFilePath(normalizedSource);
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidOperationException("SQLite 文件 URI 缺少有效文件路径，无法解析物理数据库身份。");
            return new SqlDatabaseIdentity
            {
                DatabaseType = DatabaseType.Sqlite,
                FilePath = Path.GetFullPath(filePath)
            };
        }
        return new SqlDatabaseIdentity
        {
            DatabaseType = DatabaseType.Sqlite,
            FilePath = Path.GetFullPath(normalizedSource)
        };
    }

    private static (string Server, string Instance, int? Port) ParseSqlServerEndpoint(string value, int? configuredPort)
    {
        var endpoint = Normalize(value);
        if (endpoint?.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase) == true)
            endpoint = endpoint.Substring(4);
        if (string.IsNullOrWhiteSpace(endpoint))
            return (null, null, configuredPort);
        var commaIndex = endpoint.LastIndexOf(',');
        var port = configuredPort;
        if (commaIndex > -1 && int.TryParse(endpoint.Substring(commaIndex + 1), out var embeddedPort))
        {
            port = embeddedPort;
            endpoint = endpoint.Substring(0, commaIndex);
        }
        var instanceIndex = endpoint.IndexOf('\\');
        return instanceIndex < 0
            ? (NormalizeSqlServerHost(endpoint), null, port)
            : (Normalize(endpoint.Substring(0, instanceIndex)), Normalize(endpoint.Substring(instanceIndex + 1)), port);
    }

    private static string NormalizeSqlServerHost(string value)
    {
        var host = Normalize(value);
        return host?.Length > 2 && host[0] == '[' && host[host.Length - 1] == ']'
            ? Normalize(host.Substring(1, host.Length - 2))
            : host;
    }

    private static (string Server, int? Port) ParseHostEndpoint(string value, int? configuredPort)
    {
        var endpoint = Normalize(value);
        if (string.IsNullOrWhiteSpace(endpoint))
            return (null, configuredPort);
        if (endpoint.StartsWith("[", StringComparison.Ordinal) && endpoint.IndexOf("]:", StringComparison.Ordinal) > 0)
        {
            var separatorIndex = endpoint.LastIndexOf(':');
            return int.TryParse(endpoint.Substring(separatorIndex + 1), out var ipv6Port)
                ? (Normalize(endpoint.Substring(1, separatorIndex - 2)), configuredPort ?? ipv6Port)
                : (endpoint, configuredPort);
        }
        var colonIndex = endpoint.LastIndexOf(':');
        if (colonIndex > -1 && endpoint.IndexOf(':') == colonIndex && int.TryParse(endpoint.Substring(colonIndex + 1), out var port))
            return (Normalize(endpoint.Substring(0, colonIndex)), configuredPort ?? port);
        return (endpoint, configuredPort);
    }

    private static (string Server, int? Port, string Database)? ParseOracleEndpoint(string value)
    {
        var endpoint = Normalize(value);
        if (endpoint?.StartsWith("//", StringComparison.Ordinal) == true)
            endpoint = endpoint.Substring(2);
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.IndexOfAny(new[] { '(', ')', '=' }) >= 0)
            return null;
        var slashIndex = endpoint.IndexOf('/');
        if (slashIndex < 1 || slashIndex == endpoint.Length - 1)
            return null;
        var host = endpoint.Substring(0, slashIndex);
        var database = Normalize(endpoint.Substring(slashIndex + 1));
        var parsedHost = ParseHostEndpoint(host, null);
        return string.IsNullOrWhiteSpace(parsedHost.Server) || string.IsNullOrWhiteSpace(database)
            ? null
            : (parsedHost.Server, parsedHost.Port, database);
    }

    private static bool IsOracleTnsDescriptor(string value) => string.IsNullOrWhiteSpace(value) == false &&
        value.IndexOfAny(new[] { '(', ')', '=' }) >= 0;

    private static bool TryParseOracleTnsDescriptor(string value, out SqlDatabaseIdentity identity)
    {
        identity = null;
        var parser = new OracleTnsDescriptorParser(value);
        if (parser.TryParse(out var descriptor) == false ||
            string.Equals(descriptor.Name, "DESCRIPTION", StringComparison.OrdinalIgnoreCase) == false ||
            descriptor.Children == null || descriptor.Children.Count != 2)
            return false;

        OracleTnsDescriptorNode address = null;
        OracleTnsDescriptorNode connectData = null;
        foreach (var child in descriptor.Children)
        {
            if (string.Equals(child.Name, "ADDRESS", StringComparison.OrdinalIgnoreCase))
            {
                if (address != null)
                    return false;
                address = child;
                continue;
            }
            if (string.Equals(child.Name, "CONNECT_DATA", StringComparison.OrdinalIgnoreCase))
            {
                if (connectData != null)
                    return false;
                connectData = child;
                continue;
            }
            return false;
        }

        if (TryGetTnsFields(address, new[] { "PROTOCOL", "HOST", "PORT" }, out var addressFields) == false ||
            TryGetTnsFields(connectData, new[] { "SERVICE_NAME", "SID" }, out var connectDataFields) == false ||
            addressFields.ContainsKey("PROTOCOL") == false || addressFields.ContainsKey("HOST") == false ||
            addressFields.ContainsKey("PORT") == false ||
            string.Equals(addressFields["PROTOCOL"], "TCP", StringComparison.OrdinalIgnoreCase) == false)
            return false;

        var port = ParsePort(addressFields["PORT"]);
        if (port == null)
            return false;

        connectDataFields.TryGetValue("SERVICE_NAME", out var serviceNameValue);
        connectDataFields.TryGetValue("SID", out var sidValue);
        var serviceName = Normalize(serviceNameValue);
        var sid = Normalize(sidValue);
        if (string.IsNullOrWhiteSpace(addressFields["HOST"]) ||
            (string.IsNullOrWhiteSpace(serviceName) == string.IsNullOrWhiteSpace(sid)))
            return false;

        identity = new SqlDatabaseIdentity
        {
            DatabaseType = DatabaseType.Oracle,
            Server = Normalize(addressFields["HOST"]),
            Port = port.Value,
            ServiceName = serviceName,
            Sid = sid
        };
        return true;
    }

    private static bool TryGetTnsFields(OracleTnsDescriptorNode node, IReadOnlyCollection<string> allowedNames,
        out Dictionary<string, string> fields)
    {
        fields = null;
        if (node?.Children == null || node.Children.Count == 0)
            return false;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in node.Children)
        {
            if (child.Children != null || string.IsNullOrWhiteSpace(child.Value) || allowedNames.Contains(child.Name,
                    StringComparer.OrdinalIgnoreCase) == false || result.ContainsKey(child.Name))
                return false;
            result.Add(child.Name, child.Value);
        }
        fields = result;
        return true;
    }

    private static bool IsSimpleHost(string value) => string.IsNullOrWhiteSpace(value) == false &&
        value.IndexOfAny(new[] { '/', '\\', ':', '(', ')', '=' }) < 0;

    private sealed class OracleTnsDescriptorParser
    {
        private readonly string _value;
        private int _position;

        public OracleTnsDescriptorParser(string value) => _value = value;

        public bool TryParse(out OracleTnsDescriptorNode node)
        {
            node = null;
            SkipWhitespace();
            if (TryParseNode(out node) == false)
                return false;
            SkipWhitespace();
            return _position == _value.Length;
        }

        private bool TryParseNode(out OracleTnsDescriptorNode node)
        {
            node = null;
            SkipWhitespace();
            if (Read('(') == false)
                return false;
            var nameStart = _position;
            while (_position < _value.Length && _value[_position] != '=')
            {
                if (_value[_position] is '(' or ')')
                    return false;
                _position++;
            }
            if (_position == nameStart || Read('=') == false)
                return false;
            var name = Normalize(_value.Substring(nameStart, _position - nameStart - 1));
            if (string.IsNullOrWhiteSpace(name))
                return false;

            SkipWhitespace();
            if (_position < _value.Length && _value[_position] == '(')
            {
                var children = new List<OracleTnsDescriptorNode>();
                while (true)
                {
                    SkipWhitespace();
                    if (_position >= _value.Length || _value[_position] != '(')
                        break;
                    if (TryParseNode(out var child) == false)
                        return false;
                    children.Add(child);
                }
                if (children.Count == 0 || Read(')') == false)
                    return false;
                node = new OracleTnsDescriptorNode(name, null, children);
                return true;
            }

            var valueStart = _position;
            while (_position < _value.Length && _value[_position] != ')')
            {
                if (_value[_position] is '(' or '=')
                    return false;
                _position++;
            }
            if (_position == valueStart || Read(')') == false)
                return false;
            var scalarValue = Normalize(_value.Substring(valueStart, _position - valueStart - 1));
            if (string.IsNullOrWhiteSpace(scalarValue))
                return false;
            node = new OracleTnsDescriptorNode(name, scalarValue, null);
            return true;
        }

        private bool Read(char expected)
        {
            if (_position >= _value.Length || _value[_position] != expected)
                return false;
            _position++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_position < _value.Length && char.IsWhiteSpace(_value[_position]))
                _position++;
        }
    }

    private sealed class OracleTnsDescriptorNode
    {
        public OracleTnsDescriptorNode(string name, string value, List<OracleTnsDescriptorNode> children)
        {
            Name = name;
            Value = value;
            Children = children;
        }

        public string Name { get; }
        public string Value { get; }
        public List<OracleTnsDescriptorNode> Children { get; }
    }

    private static string GetSqliteUriOption(string dataSource, string name)
    {
        if (dataSource?.StartsWith("file:", StringComparison.OrdinalIgnoreCase) != true)
            return null;
        var queryIndex = dataSource.IndexOf('?');
        if (queryIndex < 0 || queryIndex == dataSource.Length - 1)
            return null;
        foreach (var parameter in dataSource.Substring(queryIndex + 1).Split('&'))
        {
            var separatorIndex = parameter.IndexOf('=');
            if (separatorIndex > 0 && string.Equals(parameter.Substring(0, separatorIndex), name, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(parameter.Substring(separatorIndex + 1));
        }
        return null;
    }

    private static string GetSqliteMemoryName(string dataSource)
    {
        if (string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
            return null;
        var source = dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            ? dataSource.Substring("file:".Length) : dataSource;
        var queryIndex = source.IndexOf('?');
        var name = queryIndex < 0 ? source : source.Substring(0, queryIndex);
        return string.IsNullOrWhiteSpace(name) ? null : Uri.UnescapeDataString(name.Trim());
    }

    private static string GetSqliteFilePath(string dataSource)
    {
        if (dataSource.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate(dataSource, UriKind.Absolute, out var uri) && uri.IsFile)
            return uri.LocalPath;
        var source = dataSource.Substring("file:".Length);
        var queryIndex = source.IndexOf('?');
        var path = queryIndex < 0 ? source : source.Substring(0, queryIndex);
        return string.IsNullOrWhiteSpace(path) ? null : Uri.UnescapeDataString(path.Trim());
    }

    private static void EnsureRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"数据库连接字符串缺少{fieldName}，无法安全比较物理数据库身份。");
    }

    private static string GetValue(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value) && string.IsNullOrWhiteSpace(value?.ToString()) == false)
                return value.ToString();
        }
        return null;
    }

    private static int? ParsePort(string value) => int.TryParse(value, out var port) && port > 0 && port <= 65535 ? port : null;

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}