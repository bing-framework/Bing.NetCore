using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// SqlServer 路由与执行测试
/// </summary>
public class SqlServerRoutingAndExecutionTest
{
    /// <summary>
    /// 测试 - 查询工厂应使用解析后的连接字符串创建查询对象。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_ShouldUseResolvedConnectionString()
    {
        // Arrange
        var metadataOptions = new SqlMetadataOptions();
        metadataOptions.Databases[SqlMetadataOptions.GetDatabaseDescriptorKey("reporting", DatabaseType.SqlServer,
            DatabaseRole.Reporting)] = new DatabaseDescriptor
        {
            DbKey = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            Role = DatabaseRole.Reporting,
            ConnectionString = "Server=reporting;Database=test;"
        };
        var services = CreateServices(metadataOptions);
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=default;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = factory.Create<InspectableSqlServerQuery>("reporting", DatabaseType.SqlServer,
            DatabaseRole.Reporting);

        // Assert
        query.CurrentOptions.ConnectionString.ShouldBe("Server=reporting;Database=test;");
        query.CurrentOptions.DatabaseType.ShouldBe(DatabaseType.SqlServer);
        query.CurrentOptions.Connection.ShouldBeNull();
    }

    /// <summary>
    /// 测试 - 查询工厂创建接口查询对象时不应为了获取实现类型而提前创建实例。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_CreateInterface_ShouldNotInstantiateServiceWhenResolvingImplementationType()
    {
        // Arrange
        CountedSqlServerQuery.CreatedCount = 0;
        var metadataOptions = new SqlMetadataOptions();
        metadataOptions.Databases[SqlMetadataOptions.GetDatabaseDescriptorKey("reporting", DatabaseType.SqlServer,
            DatabaseRole.Reporting)] = new DatabaseDescriptor
        {
            DbKey = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            Role = DatabaseRole.Reporting,
            ConnectionString = "Server=reporting;Database=test;"
        };
        var services = CreateServices(metadataOptions);
        services.AddSqlServerSqlQuery<ICountedSqlServerQuery, CountedSqlServerQuery>(options =>
            options.ConnectionString("Server=default;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = factory.Create<ICountedSqlServerQuery>("reporting", DatabaseType.SqlServer,
            DatabaseRole.Reporting);

        // Assert
        query.ShouldBeOfType<CountedSqlServerQuery>();
        CountedSqlServerQuery.CreatedCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 工厂创建的查询对象应在连接上下文与实体映射上下文之间保持一致。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_ShouldUseSameContextForConnectionAndEntityMapping()
    {
        // Arrange
        var metadataOptions = CreateRoutingMetadataOptions();
        var services = CreateServices(metadataOptions);
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=default;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();
        var accessor = provider.GetRequiredService<IDatabaseContextAccessor>();

        // Act
        var query = factory.Create<InspectableSqlServerQuery>("reporting", DatabaseType.SqlServer,
            DatabaseRole.Reporting);
        accessor.Current = new DatabaseContext
        {
            DbKey = "default",
            DatabaseType = DatabaseType.SqlServer,
            Role = DatabaseRole.Default
        };
        query.From<MappedSample>("u").Where<MappedSample>(t => t.Name, "abc");

        // Assert
        query.CurrentOptions.ConnectionString.ShouldBe("Server=reporting;Database=test;");
        query.CurrentSql.ShouldContain("[Users_Reporting]");
        query.CurrentSql.ShouldContain("[reporting_name]");
    }

    /// <summary>
    /// 测试 - 原生 SQL 参数映射应生成带完整元数据的数据库参数。
    /// </summary>
    [Fact]
    public void RawSql_WithParameterMap_ShouldCreateFullMetadataParams()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);

        // Act
        var result = executor.ExecuteSql<MappedSample>(
            "Update [Users] Set [Name]=@name Where [Id]=@id",
            new { name = "abc", id = 1 },
            map => map.Map("name", t => t.Name).Map("id", t => t.Id));

        // Assert
        result.ShouldBe(1);
        connection.LastCreatedParameters.Count.ShouldBe(2);
        var name = connection.LastCreatedParameters.Single(t => t.ParameterName == "name");
        name.Value.ShouldBe("abc");
        name.DbType.ShouldBe(DbType.String);
        name.Size.ShouldBe(20);
        var id = connection.LastCreatedParameters.Single(t => t.ParameterName == "id");
        id.Value.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 原生 SQL 参数映射显式传入 null 时应绑定 DBNull 而不是从源对象回退取值。
    /// </summary>
    [Fact]
    public void RawSql_WithParameterMapExplicitNull_ShouldBindDbNull()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);

        // Act
        var result = executor.ExecuteSql<MappedSample>(
            "Update [Users] Set [Name]=@name Where [Id]=@id",
            new { name = "source", id = 1 },
            map => map.Add("name", t => t.Name, null).Map("id", t => t.Id));

        // Assert
        result.ShouldBe(1);
        var name = connection.LastCreatedParameters.Single(t => t.ParameterName == "name");
        name.Value.ShouldBe(DBNull.Value);
        var id = connection.LastCreatedParameters.Single(t => t.ParameterName == "id");
        id.Value.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - Dapper 执行诊断应包含增强参数元数据。
    /// </summary>
    [Fact]
    public void ExecuteSql_WithParameterMap_ShouldPublishParameterMetadataDiagnostics()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(t => message = t);
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);

        // Act
        executor.ExecuteSql<MappedSample>(
            "Update [Users] Set [Name]=@name Where [Id]=@id",
            new { id = 1 },
            map => map.Add("name", t => t.Name, null).Map("id", t => t.Id));

        // Assert
        message.ShouldNotBeNull();
        message.RawParameters.ShouldBeOfType<SqlParameterMap<MappedSample>>();
        message.BoundParameters.ShouldNotBeNull();
        message.SqlParametersMetadata.Count.ShouldBe(2);
        var name = message.SqlParametersMetadata.Single(t => t.Name == "name");
        name.Value.ShouldBeNull();
        name.PropertyName.ShouldBe(nameof(MappedSample.Name));
        name.Source.ShouldBe(SqlParameterSource.RawSql);
        name.MetadataLevel.ShouldBe(SqlParameterMetadataLevel.Full);
    }

    /// <summary>
    /// 测试 - 未提供参数映射时应保持原生 SQL 的旧参数行为。
    /// </summary>
    [Fact]
    public void RawSql_WithoutParameterMap_ShouldKeepBackwardCompatibility()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);

        // Act
        var result = executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "abc" });

        // Assert
        result.ShouldBe(1);
        connection.LastCreatedParameters.Count.ShouldBe(1);
        var parameter = connection.LastCreatedParameters.Single();
        parameter.ParameterName.ShouldBe("name");
        parameter.Value.ShouldBe("abc");
    }

    /// <summary>
    /// 测试 - 异步 Count 应使用增强参数而不是旧字典参数。
    /// </summary>
    [Fact]
    public async Task GetCountAsync_ShouldUseMetadataParameters()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var query = CreateQuery(connection);
        query.From<MappedSample>("a").Where<MappedSample>(t => t.Name, "abc");

        // Act
        var result = await query.InvokeCountAsync();

        // Assert
        result.ShouldBe(1);
        connection.LastCreatedParameters.Count.ShouldBe(1);
        var parameter = connection.LastCreatedParameters.Single();
        parameter.DbType.ShouldBe(DbType.String);
        parameter.Size.ShouldBe(20);
    }

    /// <summary>
    /// 测试 - 类型转换器解析器应解析 SqlServer 对应的 Provider 转换器。
    /// </summary>
    [Fact]
    public void TypeConverterResolver_ShouldResolveProviderConverter()
    {
        // Arrange
        var services = CreateServices();
        services.AddSqlServerSqlQuery("Server=default;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var resolver = provider.GetRequiredService<ITypeConverterResolver>();
        var converter = resolver.Resolve(DatabaseType.SqlServer);

        // Assert
        converter.ShouldBeOfType<Bing.Data.Metadata.SqlServerTypeConverter>();
    }

    /// <summary>
    /// 创建服务集合
    /// </summary>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <returns>服务集合</returns>
    private static IServiceCollection CreateServices(SqlMetadataOptions metadataOptions = null)
    {
        var services = new ServiceCollection();
        if (metadataOptions != null)
            services.AddSingleton(metadataOptions);
        services.AddDatabase<TestDatabase>();
        return services;
    }

    /// <summary>
    /// 创建路由元数据配置
    /// </summary>
    /// <returns>Sql 元数据配置</returns>
    private static SqlMetadataOptions CreateRoutingMetadataOptions()
    {
        var options = new SqlMetadataOptions();
        options.Databases[SqlMetadataOptions.GetDatabaseDescriptorKey("reporting", DatabaseType.SqlServer,
            DatabaseRole.Reporting)] = new DatabaseDescriptor
        {
            DbKey = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            Role = DatabaseRole.Reporting,
            ConnectionString = "Server=reporting;Database=test;"
        };
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MappedSample),
            DbKey = "default",
            DatabaseType = DatabaseType.SqlServer,
            Role = DatabaseRole.Default,
            TableName = "Users",
            Columns =
            {
                [nameof(MappedSample.Name)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(MappedSample.Name),
                    ColumnName = "Name"
                }
            }
        });
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MappedSample),
            DbKey = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            Role = DatabaseRole.Reporting,
            TableName = "Users_Reporting",
            Columns =
            {
                [nameof(MappedSample.Name)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(MappedSample.Name),
                    ColumnName = "reporting_name"
                }
            }
        });
        return options;
    }

    /// <summary>
    /// 创建查询对象
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>查询对象</returns>
    private static InspectableSqlServerQuery CreateQuery(CaptureDbConnection connection)
    {
        var services = CreateServices();
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.Connection(connection));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<InspectableSqlServerQuery>();
    }

    /// <summary>
    /// 创建执行器
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>执行器</returns>
    private static InspectableSqlServerExecutor CreateExecutor(CaptureDbConnection connection)
    {
        var services = CreateServices();
        services.AddSqlServerSqlExecutor<InspectableSqlServerExecutor, InspectableSqlServerExecutor>(options =>
            options.Connection(connection));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<InspectableSqlServerExecutor>();
    }

    /// <summary>
    /// 测试数据库
    /// </summary>
    private sealed class TestDatabase : IDatabase
    {
        public IDbConnection GetConnection() => null;
    }

    /// <summary>
    /// 测试查询对象
    /// </summary>
    private sealed class InspectableSqlServerQuery : SqlServerSqlQueryBase
    {
        public InspectableSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<InspectableSqlServerQuery> options, IDatabase database = null)
            : base(serviceProvider, options, database)
        {
        }

        public SqlOptions CurrentOptions => Options;

        public string CurrentSql => GetSql();

        public Task<int> InvokeCountAsync() => GetCountAsync();
    }

    /// <summary>
    /// 计数查询接口
    /// </summary>
    private interface ICountedSqlServerQuery : ISqlQuery
    {
    }

    /// <summary>
    /// 计数查询对象
    /// </summary>
    private sealed class CountedSqlServerQuery : SqlServerSqlQueryBase, ICountedSqlServerQuery
    {
        public static int CreatedCount { get; set; }

        public CountedSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<CountedSqlServerQuery> options, IDatabase database = null)
            : base(serviceProvider, options, database)
        {
            CreatedCount++;
        }
    }

    /// <summary>
    /// 测试执行器
    /// </summary>
    private sealed class InspectableSqlServerExecutor : SqlServerSqlExecutorBase
    {
        public InspectableSqlServerExecutor(IServiceProvider serviceProvider,
            SqlOptions<InspectableSqlServerExecutor> options, IDatabase database = null)
            : base(serviceProvider, options, database)
        {
        }
    }

    /// <summary>
    /// 字符串映射测试样例
    /// </summary>
    private sealed class MappedSample
    {
        /// <summary>
        /// 标识
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [StringLength(20)]
        [Column(TypeName = "nvarchar(20)")]
        public string Name { get; set; }
    }

    /// <summary>
    /// 捕获参数的数据库连接
    /// </summary>
    private sealed class CaptureDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Open;

        public List<CaptureDbParameter> LastCreatedParameters { get; private set; } = new();

        public override string ConnectionString { get; set; }

        public override string Database => "test";

        public override string DataSource => "test";

        public override string ServerVersion => "1.0";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }

        public override void Close() => _state = ConnectionState.Closed;

        public override void Open() => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => null;

        protected override DbCommand CreateDbCommand() => new CaptureDbCommand(this);

        protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel,
            CancellationToken cancellationToken) => ValueTask.FromResult<DbTransaction>(null);

        public override Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void SetParameters(IEnumerable<CaptureDbParameter> parameters) =>
            LastCreatedParameters = parameters.ToList();
    }

    /// <summary>
    /// 捕获参数的数据库命令
    /// </summary>
    private sealed class CaptureDbCommand : DbCommand
    {
        private readonly CaptureDbConnection _connection;
        private readonly CaptureDbParameterCollection _parameters = new();

        public CaptureDbCommand(CaptureDbConnection connection) => _connection = connection;

        public override string CommandText { get; set; }

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection DbConnection
        {
            get => _connection;
            set { }
        }

        protected override DbParameterCollection DbParameterCollection => _parameters;

        protected override DbTransaction DbTransaction { get; set; }

        public override void Cancel() { }

        public override int ExecuteNonQuery()
        {
            _connection.SetParameters(_parameters.Items);
            return 1;
        }

        public override object ExecuteScalar()
        {
            _connection.SetParameters(_parameters.Items);
            return 1;
        }

        public override void Prepare() { }

        protected override DbParameter CreateDbParameter() => new CaptureDbParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            _connection.SetParameters(_parameters.Items);
            return Task.FromResult(1);
        }

        public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            _connection.SetParameters(_parameters.Items);
            return Task.FromResult<object>(1);
        }
    }

    /// <summary>
    /// 捕获参数集合
    /// </summary>
    private sealed class CaptureDbParameterCollection : DbParameterCollection
    {
        public List<CaptureDbParameter> Items { get; } = new();

        public override int Count => Items.Count;

        public override object SyncRoot { get; } = new();

        public override int Add(object value)
        {
            Items.Add((CaptureDbParameter)value);
            return Items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value);
        }

        public override void Clear() => Items.Clear();

        public override bool Contains(object value) => Items.Contains((CaptureDbParameter)value);

        public override bool Contains(string value) => Items.Any(t => t.ParameterName == value);

        public override void CopyTo(Array array, int index) => Items.ToArray().CopyTo(array, index);

        public override IEnumerator GetEnumerator() => Items.GetEnumerator();

        public override int IndexOf(object value) => Items.IndexOf((CaptureDbParameter)value);

        public override int IndexOf(string parameterName) => Items.FindIndex(t => t.ParameterName == parameterName);

        public override void Insert(int index, object value) => Items.Insert(index, (CaptureDbParameter)value);

        public override void Remove(object value) => Items.Remove((CaptureDbParameter)value);

        public override void RemoveAt(int index) => Items.RemoveAt(index);

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
                Items.RemoveAt(index);
        }

        protected override DbParameter GetParameter(int index) => Items[index];

        protected override DbParameter GetParameter(string parameterName) =>
            Items.FirstOrDefault(t => t.ParameterName == parameterName);

        protected override void SetParameter(int index, DbParameter value) => Items[index] = (CaptureDbParameter)value;

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
            {
                Items.Add((CaptureDbParameter)value);
                return;
            }

            Items[index] = (CaptureDbParameter)value;
        }
    }

    /// <summary>
    /// 捕获数据库参数
    /// </summary>
    private sealed class CaptureDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        public override bool IsNullable { get; set; }

        public override string ParameterName { get; set; }

        public override string SourceColumn { get; set; }

        public override object Value { get; set; }

        public override bool SourceColumnNullMapping { get; set; }

        public override int Size { get; set; }

        public override byte Precision { get; set; }

        public override byte Scale { get; set; }

        public override void ResetDbType() { }
    }

    /// <summary>
    /// Sql 诊断观察器
    /// </summary>
    private sealed class SqlDiagnosticObserver : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object>>, IDisposable
    {
        private readonly Action<DiagnosticsMessage> _onMessage;
        private readonly IDisposable _allSubscription;
        private IDisposable _listenerSubscription;

        public SqlDiagnosticObserver(Action<DiagnosticsMessage> onMessage)
        {
            _onMessage = onMessage;
            _allSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name != SqlQueryDiagnosticListenerNames.DiagnosticListenerName)
                return;
            _listenerSubscription = value.Subscribe(this,
                name => name == SqlQueryDiagnosticListenerNames.BeforeExecute);
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value is DiagnosticsMessage message)
                _onMessage(message);
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void Dispose()
        {
            _listenerSubscription?.Dispose();
            _allSubscription.Dispose();
        }
    }
}