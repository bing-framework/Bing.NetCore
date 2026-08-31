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

    /// <summary>
    /// 从 SQL Server 连接字符串解析可比较的物理数据库身份。
    /// </summary>
    /// <param name="builder">已解析的 SQL Server 连接字符串。</param>
    /// <returns>包含服务器、实例或端口及数据库名称的物理数据库身份。</returns>
    /// <exception cref="InvalidOperationException">缺少服务器地址或数据库名称时抛出。</exception>
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

    /// <summary>
    /// 从使用主机与数据库名称的连接字符串解析物理数据库身份。
    /// </summary>
    /// <param name="databaseType">要写入身份的数据库类型。</param>
    /// <param name="builder">已解析的连接字符串。</param>
    /// <param name="defaultPort">连接字符串未显式指定端口时使用的默认端口。</param>
    /// <param name="serverKeys">按优先级读取服务器地址的连接字符串键。</param>
    /// <returns>包含服务器、端口和数据库名称的物理数据库身份。</returns>
    /// <exception cref="InvalidOperationException">缺少服务器地址或数据库名称时抛出。</exception>
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

    /// <summary>
    /// 从 Oracle 连接字符串解析物理数据库身份。
    /// </summary>
    /// <param name="builder">已解析的 Oracle 连接字符串。</param>
    /// <returns>可唯一识别目标时可比较的 Oracle 身份；别名或歧义目标返回不可比较身份。</returns>
    /// <exception cref="InvalidOperationException">缺少数据源时抛出。</exception>
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

    /// <summary>
    /// 判断 Oracle 连接是否只指定了一个数据库目标。
    /// </summary>
    /// <param name="serviceName">Oracle 服务名。</param>
    /// <param name="sid">Oracle SID。</param>
    /// <returns>仅指定服务名或 SID 之一时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private static bool HasSingleOracleDatabaseTarget(string serviceName, string sid) =>
        string.IsNullOrWhiteSpace(serviceName) != string.IsNullOrWhiteSpace(sid);

    /// <summary>
    /// 从 SQLite 连接字符串解析物理数据库身份。
    /// </summary>
    /// <param name="builder">已解析的 SQLite 连接字符串。</param>
    /// <returns>SQLite 文件或内存数据库的物理身份。</returns>
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

    /// <summary>
    /// 解析 SQL Server 的服务器、实例和端口端点。
    /// </summary>
    /// <param name="value">连接字符串中的服务器端点。</param>
    /// <param name="configuredPort">连接字符串中单独配置的端口。</param>
    /// <returns>解析出的服务器、实例和端口；无法提供端点时服务器和实例为空。</returns>
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

    /// <summary>
    /// 规范化 SQL Server 主机名称并移除外围方括号。
    /// </summary>
    /// <param name="value">原始主机名称。</param>
    /// <returns>去除空白和外围方括号后的主机名称。</returns>
    private static string NormalizeSqlServerHost(string value)
    {
        var host = Normalize(value);
        return host?.Length > 2 && host[0] == '[' && host[host.Length - 1] == ']'
            ? Normalize(host.Substring(1, host.Length - 2))
            : host;
    }

    /// <summary>
    /// 解析通用主机端点及其可选端口。
    /// </summary>
    /// <param name="value">原始主机端点。</param>
    /// <param name="configuredPort">连接字符串中单独配置的端口。</param>
    /// <returns>解析出的服务器和端口；未提供端点时服务器为空。</returns>
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

    /// <summary>
    /// 解析 Oracle 的主机、端口和服务端点格式。
    /// </summary>
    /// <param name="value">原始 Oracle 端点。</param>
    /// <returns>解析成功时返回服务器、端口和数据库名称，否则返回 <see langword="null"/>。</returns>
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

    /// <summary>
    /// 判断数据源是否具有 Oracle TNS 描述符格式特征。
    /// </summary>
    /// <param name="value">待判断的数据源文本。</param>
    /// <returns>包含 TNS 描述符结构字符时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private static bool IsOracleTnsDescriptor(string value) => string.IsNullOrWhiteSpace(value) == false &&
        value.IndexOfAny(new[] { '(', ')', '=' }) >= 0;

    /// <summary>
    /// 尝试从受限 Oracle TNS 描述符解析唯一物理数据库身份。
    /// </summary>
    /// <param name="value">待解析的 TNS 描述符文本。</param>
    /// <param name="identity">解析成功时输出的 Oracle 物理数据库身份。</param>
    /// <returns>描述符只包含唯一 TCP 地址及唯一服务名或 SID 时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    /// <remarks>格式不完整、节点重复、包含未知节点或目标存在歧义时拒绝解析，以避免将不确定的目标视为相同数据库。</remarks>
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

    /// <summary>
    /// 从 TNS 节点提取并验证允许的标量字段。
    /// </summary>
    /// <param name="node">要读取子节点的 TNS 节点。</param>
    /// <param name="allowedNames">允许出现的字段名称集合。</param>
    /// <param name="fields">验证成功时输出的字段字典。</param>
    /// <returns>所有子节点均为名称唯一、非空且位于白名单内的标量字段时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
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

    /// <summary>
    /// 判断文本是否为不含端口、实例或描述符语法的简单主机名。
    /// </summary>
    /// <param name="value">待判断的主机文本。</param>
    /// <returns>文本符合简单主机名格式时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private static bool IsSimpleHost(string value) => string.IsNullOrWhiteSpace(value) == false &&
        value.IndexOfAny(new[] { '/', '\\', ':', '(', ')', '=' }) < 0;

    /// <summary>
    /// 解析受限 Oracle TNS 描述符语法的内部解析器。
    /// </summary>
    private sealed class OracleTnsDescriptorParser
    {
        /// <summary>
        /// 待解析的 TNS 描述符文本。
        /// </summary>
        private readonly string _value;

        /// <summary>
        /// 当前解析游标位置。
        /// </summary>
        private int _position;

        /// <summary>
        /// 初始化一个 <see cref="OracleTnsDescriptorParser"/> 类型的实例。
        /// </summary>
        /// <param name="value">待解析的 TNS 描述符文本。</param>
        public OracleTnsDescriptorParser(string value) => _value = value;

        /// <summary>
        /// 解析完整的 TNS 描述符并确认输入已被完全消费。
        /// </summary>
        /// <param name="node">解析成功时输出的根节点。</param>
        /// <returns>输入符合受限 TNS 节点语法且没有剩余字符时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        public bool TryParse(out OracleTnsDescriptorNode node)
        {
            node = null;
            SkipWhitespace();
            if (TryParseNode(out node) == false)
                return false;
            SkipWhitespace();
            return _position == _value.Length;
        }

        /// <summary>
        /// 从当前游标位置解析一个括号包裹的 TNS 节点。
        /// </summary>
        /// <param name="node">解析成功时输出的节点。</param>
        /// <returns>节点满足 <c>名称=标量值</c> 或 <c>名称=子节点列表</c> 的受限语法时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
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

        /// <summary>
        /// 读取并消费当前位置的指定字符。
        /// </summary>
        /// <param name="expected">预期读取的字符。</param>
        /// <returns>当前位置为指定字符并已成功消费时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        private bool Read(char expected)
        {
            if (_position >= _value.Length || _value[_position] != expected)
                return false;
            _position++;
            return true;
        }

        /// <summary>
        /// 跳过当前游标前的空白字符。
        /// </summary>
        private void SkipWhitespace()
        {
            while (_position < _value.Length && char.IsWhiteSpace(_value[_position]))
                _position++;
        }
    }

    /// <summary>
    /// Oracle TNS 描述符的语法树节点。
    /// </summary>
    private sealed class OracleTnsDescriptorNode
    {
        /// <summary>
        /// 初始化一个 <see cref="OracleTnsDescriptorNode"/> 类型的实例。
        /// </summary>
        /// <param name="name">节点名称。</param>
        /// <param name="value">标量节点值；复合节点为空。</param>
        /// <param name="children">复合节点的子节点列表；标量节点为空。</param>
        public OracleTnsDescriptorNode(string name, string value, List<OracleTnsDescriptorNode> children)
        {
            Name = name;
            Value = value;
            Children = children;
        }

        /// <summary>
        /// 节点名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 标量节点值；复合节点为空。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 复合节点的子节点列表；标量节点为空。
        /// </summary>
        public List<OracleTnsDescriptorNode> Children { get; }
    }

    /// <summary>
    /// 从 SQLite 文件 URI 查询字符串读取指定选项值。
    /// </summary>
    /// <param name="dataSource">SQLite 数据源 URI。</param>
    /// <param name="name">要查找的选项名称。</param>
    /// <returns>解码后的选项值；数据源不是 URI、没有该选项或选项值为空时返回 <c>null</c>。</returns>
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

    /// <summary>
    /// 获取 SQLite 内存数据库 URI 中用于共享内存识别的名称。
    /// </summary>
    /// <param name="dataSource">SQLite 数据源文本。</param>
    /// <returns>解码后的内存数据库名称；匿名 <c>:memory:</c> 数据源或空名称返回 <c>null</c>。</returns>
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

    /// <summary>
    /// 从 SQLite 文件 URI 获取解码后的文件路径。
    /// </summary>
    /// <param name="dataSource">以 <c>file:</c> 开头的 SQLite 数据源 URI。</param>
    /// <returns>解码后的文件路径；URI 未包含路径时返回 <c>null</c>。</returns>
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

    /// <summary>
    /// 验证数据库身份比较所需的连接字符串字段。
    /// </summary>
    /// <param name="value">待验证的字段值。</param>
    /// <param name="fieldName">字段的中文名称。</param>
    private static void EnsureRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"数据库连接字符串缺少{fieldName}，无法安全比较物理数据库身份。");
    }

    /// <summary>
    /// 按优先级读取连接字符串中的第一个非空值。
    /// </summary>
    /// <param name="builder">已解析的连接字符串。</param>
    /// <param name="keys">按优先级排列的连接字符串键。</param>
    /// <returns>第一个非空键值；没有可用值时返回 <see langword="null"/>。</returns>
    private static string GetValue(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value) && string.IsNullOrWhiteSpace(value?.ToString()) == false)
                return value.ToString();
        }
        return null;
    }

    /// <summary>
    /// 解析有效的 TCP 端口号。
    /// </summary>
    /// <param name="value">待解析的端口文本。</param>
    /// <returns>范围为 1 到 65535 的端口号；文本无效时返回 <see langword="null"/>。</returns>
    private static int? ParsePort(string value) => int.TryParse(value, out var port) && port > 0 && port <= 65535 ? port : null;

    /// <summary>
    /// 清理连接字符串字段的空白内容。
    /// </summary>
    /// <param name="value">待规范化的文本。</param>
    /// <returns>去除首尾空白后的文本；空白文本返回 <see langword="null"/>。</returns>
    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}