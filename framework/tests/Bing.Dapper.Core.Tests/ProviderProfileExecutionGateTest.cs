using System.Collections;
using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Mutations;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// Provider Profile 执行能力 Gate 测试。
/// </summary>
public sealed class ProviderProfileExecutionGateTest
{
    /// <summary>
    /// 测试目的：冻结描述未声明 Returning 能力时，同步入口必须在连接打开前拒绝，即使当前 Provider 支持其他 Mutation。
    /// </summary>
    [Fact]
    public void ExecuteReturning_WhenDescriptionProfileDisablesReturning_ShouldRejectBeforeConnectionOpen()
    {
        // Arrange
        var connection = CreateConnection();
        var profile = new SqlProviderProfile
        {
            Mutation = new SqlProviderMutationCapabilities { SupportsReturning = false }
        };
        using var serviceProvider = CreateServiceProvider(profile);
        using var executor = new ProfileGateExecutor(serviceProvider, CreateOptions(connection.Object));
        var description = CreateReturningDescription(profile);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => executor.ExecuteReturning<int>(description));

        // Assert
        Assert.Equal("写入命令 Provider test.profile-gate 未声明 Returning 能力，不能执行。", exception.Message);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
    }

    /// <summary>
    /// 测试目的：冻结描述未声明 Returning 能力时，异步入口必须与同步入口在连接访问前使用相同 Gate。
    /// </summary>
    [Fact]
    public async Task ExecuteReturningAsync_WhenDescriptionProfileDisablesReturning_ShouldRejectBeforeConnectionOpen()
    {
        // Arrange
        var connection = CreateConnection();
        var profile = new SqlProviderProfile
        {
            Mutation = new SqlProviderMutationCapabilities { SupportsReturning = false }
        };
        using var serviceProvider = CreateServiceProvider(profile);
        using var executor = new ProfileGateExecutor(serviceProvider, CreateOptions(connection.Object));
        var description = CreateReturningDescription(profile);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteReturningAsync<int>(description));

        // Assert
        Assert.Equal("写入命令 Provider test.profile-gate 未声明 Returning 能力，不能执行。", exception.Message);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
    }

    /// <summary>
    /// 测试目的：预取消应先于普通 Mutation 描述的参数与 Provider 校验执行。
    /// </summary>
    [Fact]
    public async Task ExecuteMutationAsync_WhenCancellationRequested_ShouldCancelBeforeDescriptionValidation()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile());
        using var executor = new ProfileGateExecutor(serviceProvider, CreateOptions(CreateConnection().Object));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteMutationAsync(null,
            cancellationToken: cancellationTokenSource.Token));
        Assert.Equal(0, executor.ExecuteBeforeCount);
    }

    /// <summary>
    /// 测试目的：预取消应先于 Returning Mutation 描述的参数与能力校验执行。
    /// </summary>
    [Fact]
    public async Task ExecuteReturningAsync_WhenCancellationRequested_ShouldCancelBeforeDescriptionValidation()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile());
        using var executor = new ProfileGateExecutor(serviceProvider, CreateOptions(CreateConnection().Object));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteReturningAsync<int>(null,
            cancellationToken: cancellationTokenSource.Token));
        Assert.Equal(0, executor.ExecuteBeforeCount);
    }

    /// <summary>
    /// 测试目的：Provider 禁用事务时，事务作用域应在打开连接前拒绝。
    /// </summary>
    [Fact]
    public void BeginTransactionScope_WhenProviderDisablesTransactions_ShouldRejectBeforeConnectionOpen()
    {
        // Arrange
        var connection = CreateConnection();
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile
        {
            Transaction = new SqlProviderTransactionCapabilities { SupportsTransactions = false }
        });
        using var query = new ProfileGateQuery(serviceProvider, CreateOptions(connection.Object));
        var queryFactory = new Mock<ISqlQueryFactory>();
        queryFactory.Setup(factory => factory.Create(null)).Returns(query);
        var executorFactory = new Mock<ISqlExecutorFactory>();
        var factory = new SqlTransactionScopeFactory(queryFactory.Object, executorFactory.Object);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => factory.Begin());

        // Assert
        Assert.Contains("不支持本地事务", exception.Message);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.BeginTransaction(It.IsAny<IsolationLevel>()), Times.Never);
    }

    /// <summary>
    /// 测试目的：Provider 禁用存储过程时，执行型过程入口应在创建命令前拒绝。
    /// </summary>
    [Fact]
    public void ExecuteProcedure_WhenProviderDisablesStoredProcedures_ShouldRejectBeforeCommandCreation()
    {
        // Arrange
        var connection = CreateConnection();
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile
        {
            Procedure = new SqlProviderProcedureCapabilities { SupportsStoredProcedures = false }
        });
        using var executor = new ProfileGateExecutor(serviceProvider, CreateOptions(connection.Object));

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => executor.ExecuteProcedure("UpdateReport"));

        // Assert
        Assert.Contains("test.profile-gate", exception.Message);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
    }

    /// <summary>
    /// 测试目的：Provider 仅禁用输出参数时，过程输出参数应在创建命令前拒绝。
    /// </summary>
    [Fact]
    public void ExecuteProcedure_WhenProviderDisablesOutputParameters_ShouldRejectBeforeCommandCreation()
    {
        // Arrange
        var connection = CreateConnection();
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile
        {
            Procedure = new SqlProviderProcedureCapabilities
            {
                SupportsStoredProcedures = true,
                SupportsOutputParameters = false
            }
        });
        using var executor = new ProfileGateExecutor(serviceProvider, CreateOptions(connection.Object));
        var parameters = new[] { new SqlParam("result", null, direction: ParameterDirection.Output) };

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => executor.ExecuteProcedure("GetReport", parameters));

        // Assert
        Assert.Contains("不支持存储过程输出参数", exception.Message);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
    }

    /// <summary>
    /// 测试目的：原生 Dapper 输出参数仅能在命令物化后识别时，Provider 仍应在执行命令前拒绝。
    /// </summary>
    [Theory]
    [InlineData(ParameterDirection.Output)]
    [InlineData(ParameterDirection.InputOutput)]
    [InlineData(ParameterDirection.ReturnValue)]
    public void ExecuteProcedure_WhenDynamicParametersContainUnsupportedOutput_ShouldRejectBeforeCommandExecution(
        ParameterDirection direction)
    {
        // Arrange
        var command = CreateCommand();
        var connection = CreateConnection(command.Object);
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile
        {
            Procedure = new SqlProviderProcedureCapabilities
            {
                SupportsStoredProcedures = true,
                SupportsOutputParameters = false
            }
        });
        using var executor = new ProfileGateExecutor(serviceProvider, CreateOptions(connection.Object));
        var parameters = new global::Dapper.DynamicParameters();
        parameters.Add("result", dbType: DbType.Int32, direction: direction);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => executor.ExecuteProcedure("GetReport", parameters));

        // Assert
        Assert.Contains("不支持存储过程输出参数", exception.Message);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
        command.Verify(item => item.ExecuteNonQuery(), Times.Never);
    }

    /// <summary>
    /// 测试目的：原生 Dapper 纯输入参数不应被输出参数能力门禁误拒绝。
    /// </summary>
    [Fact]
    public void ExecuteProcedure_WhenDynamicParametersContainOnlyInput_ShouldExecute()
    {
        // Arrange
        var command = CreateCommand();
        command.Setup(item => item.ExecuteNonQuery()).Returns(1);
        var connection = CreateConnection(command.Object);
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile
        {
            Procedure = new SqlProviderProcedureCapabilities
            {
                SupportsStoredProcedures = true,
                SupportsOutputParameters = false
            }
        });
        using var executor = new ProfileGateExecutor(serviceProvider, CreateOptions(connection.Object));
        var parameters = new global::Dapper.DynamicParameters();
        parameters.Add("reportId", 1);

        // Act
        var result = executor.ExecuteProcedure("GetReport", parameters);

        // Assert
        Assert.Equal(1, result.Result);
        command.Verify(item => item.ExecuteNonQuery(), Times.Once);
    }

    /// <summary>
    /// 测试目的：过程查询描述应在 Dapper 输出参数物化后、读取命令前应用 Provider 输出能力门禁。
    /// </summary>
    [Fact]
    public void ProcedureDescription_WhenDynamicParametersContainUnsupportedOutput_ShouldRejectBeforeCommandExecution()
    {
        // Arrange
        var command = CreateCommand();
        var connection = CreateConnection(command.Object);
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile
        {
            Procedure = new SqlProviderProcedureCapabilities
            {
                SupportsStoredProcedures = true,
                SupportsOutputParameters = false
            }
        });
        using var query = new ProfileGateQuery(serviceProvider, CreateOptions(connection.Object));
        var parameters = new global::Dapper.DynamicParameters();
        parameters.Add("result", dbType: DbType.Int32, direction: ParameterDirection.Output);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => query.Procedure<int>("GetReport", parameters)
            .ExecuteList());

        // Assert
        Assert.Contains("不支持存储过程输出参数", exception.Message);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
        command.Verify(item => item.ExecuteReader(), Times.Never);
    }

    /// <summary>
    /// 测试目的：Provider 禁用流式读取时，异步流枚举应在打开连接前拒绝。
    /// </summary>
    [Fact]
    public async Task AsAsyncEnumerable_WhenProviderDisablesStreaming_ShouldRejectBeforeConnectionOpen()
    {
        // Arrange
        var connection = CreateConnection();
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile
        {
            Execution = new SqlProviderExecutionCapabilities { SupportsStreaming = false }
        });
        using var query = new ProfileGateQuery(serviceProvider, CreateOptions(connection.Object));

        // Act
        var exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var _ in query.Sql<int>("Select 1").AsAsyncEnumerable())
            {
            }
        });

        // Assert
        Assert.Contains("test.profile-gate", exception.Message);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
    }

    /// <summary>
    /// 测试目的：预取消必须先于流式能力校验执行，避免调用方收到与已取消操作无关的 Provider 配置异常。
    /// </summary>
    [Fact]
    public async Task AsAsyncEnumerable_WhenCancellationRequested_ShouldCancelBeforeStreamingValidation()
    {
        // Arrange
        var connection = CreateConnection();
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile
        {
            Execution = new SqlProviderExecutionCapabilities { SupportsStreaming = false }
        });
        using var query = new ProfileGateQuery(serviceProvider, CreateOptions(connection.Object));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query.Sql<int>("Select 1").AsAsyncEnumerable(
                               cancellationToken: cancellationTokenSource.Token))
            {
            }
        });
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
    }

    /// <summary>
    /// 测试目的：Provider 禁用取消时，携带可取消令牌的异步原生命令应在打开连接前拒绝。
    /// </summary>
    [Fact]
    public async Task ExecuteSqlAsync_WhenProviderDisablesCancellation_ShouldRejectBeforeConnectionOpen()
    {
        // Arrange
        var connection = CreateConnection();
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile
        {
            Execution = new SqlProviderExecutionCapabilities { SupportsCancellation = false }
        });
        using var executor = new ProfileGateExecutor(serviceProvider, CreateOptions(connection.Object));
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => executor.ExecuteSqlAsync("Select 1",
            cancellationToken: cancellationTokenSource.Token));

        // Assert
        Assert.Contains("test.profile-gate", exception.Message);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
    }

    /// <summary>
    /// 测试目的：预取消的直接异步命令应在执行 Hook 前取消，不能被跳过 Hook 吞掉。
    /// </summary>
    [Fact]
    public async Task ExecuteSqlAsync_WhenCancelledBeforeExecution_ShouldThrowBeforeExecuteBefore()
    {
        // Arrange
        var connection = CreateConnection();
        using var serviceProvider = CreateServiceProvider(new SqlProviderProfile());
        using var executor = new ProfileGateExecutor(serviceProvider, CreateOptions(connection.Object))
        {
            SkipBeforeExecution = true
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteSqlAsync("Select 1",
            cancellationToken: cancellationTokenSource.Token));

        // Assert
        Assert.Equal(0, executor.ExecuteBeforeCount);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
    }

    /// <summary>
    /// 创建验证连接前拒绝的 ADO 连接。
    /// </summary>
    private static Mock<IDbConnection> CreateConnection()
    {
        var connection = new Mock<IDbConnection>();
        connection.SetupGet(item => item.State).Returns(ConnectionState.Closed);
        return connection;
    }

    /// <summary>
    /// 创建携带测试命令的 ADO 连接。
    /// </summary>
    /// <param name="command">Dapper 创建的命令。</param>
    private static Mock<IDbConnection> CreateConnection(IDbCommand command)
    {
        var connection = CreateConnection();
        connection.Setup(item => item.CreateCommand()).Returns(command);
        return connection;
    }

    /// <summary>
    /// 创建可由 Dapper 物化参数的测试命令。
    /// </summary>
    private static Mock<IDbCommand> CreateCommand()
    {
        var command = new Mock<IDbCommand>();
        command.SetupGet(item => item.Parameters).Returns(new TestDbParameterCollection());
        command.Setup(item => item.CreateParameter()).Returns(new TestDbParameter());
        return command;
    }

    /// <summary>
    /// 创建固定到测试 Provider 的 SQL 选项。
    /// </summary>
    private static SqlOptions CreateOptions(IDbConnection connection)
    {
        var options = new SqlOptions { Connection = connection, DatabaseType = DatabaseType.SqlServer };
        options.SetDatabaseContext(new DatabaseContext
        {
            DbKey = "profile-gate",
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "profile-gate",
                DatabaseType = DatabaseType.SqlServer,
                ProviderKey = ProfileGateProvider.ProviderKey
            }
        });
        return options;
    }

    /// <summary>
    /// 创建注册指定 Profile Provider 的测试服务容器。
    /// </summary>
    private static ServiceProvider CreateServiceProvider(SqlProviderProfile profile)
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSingleton<ISqlProvider>(new ProfileGateProvider(profile));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建冻结的 Returning 写入命令。
    /// </summary>
    /// <param name="profile">命令创建时使用的 Provider Profile。</param>
    /// <returns>包含 Returning 标记的独立写入命令。</returns>
    private static SqlWriteCommand CreateReturningDescription(SqlProviderProfile profile)
    {
        var builder = new Mock<ISqlBuilder>();
        builder.SetupGet(item => item.Provider).Returns(new ProfileGateProvider(profile));
        builder.SetupGet(item => item.OperationKind).Returns(SqlOperationKind.Update);
        builder.Setup(item => item.ToSql()).Returns("Update reports Set Status = @status Returning Id");
        var returningClause = new Mock<IReturningClause>();
        returningClause.SetupGet(item => item.IsEmpty).Returns(false);
        builder.As<IReturningClauseAccessor>().SetupGet(item => item.ReturningClause).Returns(returningClause.Object);
        builder.Setup(item => item.Clone()).Returns(builder.Object);
        return builder.Object.ToSqlWriteCommand();
    }

    /// <summary>
    /// 支持执行 Gate 验证的最小 Query。
    /// </summary>
    private sealed class ProfileGateQuery : SqlQueryBase
    {
        /// <summary>
        /// 初始化测试 Query。
        /// </summary>
        public ProfileGateQuery(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options)
        {
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 支持执行 Gate 验证的最小 Executor。
    /// </summary>
    private sealed class ProfileGateExecutor : SqlExecutorBase
    {
        /// <summary>
        /// 初始化测试执行器。
        /// </summary>
        public ProfileGateExecutor(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 是否让执行前 Hook 返回跳过。
        /// </summary>
        public bool SkipBeforeExecution { get; set; }

        /// <summary>
        /// 执行前 Hook 调用次数。
        /// </summary>
        public int ExecuteBeforeCount { get; private set; }

        /// <inheritdoc />
        protected override bool ExecuteBefore()
        {
            ExecuteBeforeCount++;
            return SkipBeforeExecution == false;
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 仅公开统一 Profile 的测试 Provider。
    /// </summary>
    private sealed class ProfileGateProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>测试 Provider Key。</summary>
        public const string ProviderKey = "test.profile-gate";

        /// <summary>
        /// 初始化测试 Provider。
        /// </summary>
        public ProfileGateProvider(SqlProviderProfile profile) => Profile = profile;

        /// <inheritdoc />
        public string Key => ProviderKey;

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect { get; } = new Mock<IDialect>().Object;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; } = new Mock<ISqlClauseFactory>().Object;

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser { get; } = new Mock<ISqlTableReferenceParser>().Object;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new Mock<ISqlPaginationRenderer>().Object;

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory { get; } = new Mock<IParameterManagerFactory>().Object;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver { get; } = new Mock<IParamLiteralsResolver>().Object;

        /// <inheritdoc />
        public SqlProviderProfile Profile { get; }
    }

    /// <summary>
    /// 供 Dapper 参数物化使用的可变参数集合。
    /// </summary>
    private sealed class TestDbParameterCollection : IDataParameterCollection
    {
        private readonly List<IDbDataParameter> _items = new();

        public object this[string parameterName]
        {
            get => _items.FirstOrDefault(item => item.ParameterName == parameterName);
            set => throw new NotSupportedException();
        }

        public object this[int index]
        {
            get => _items[index];
            set => throw new NotSupportedException();
        }

        public bool IsFixedSize => false;

        public bool IsReadOnly => false;

        public int Count => _items.Count;

        public bool IsSynchronized => false;

        public object SyncRoot { get; } = new();

        public int Add(object value)
        {
            _items.Add((IDbDataParameter)value);
            return _items.Count - 1;
        }

        public void Clear() => _items.Clear();

        public bool Contains(string parameterName) => _items.Any(item => item.ParameterName == parameterName);

        public bool Contains(object value) => _items.Contains((IDbDataParameter)value);

        public void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);

        public IEnumerator GetEnumerator() => _items.GetEnumerator();

        public int IndexOf(string parameterName) => _items.FindIndex(item => item.ParameterName == parameterName);

        public int IndexOf(object value) => _items.IndexOf((IDbDataParameter)value);

        public void Insert(int index, object value) => _items.Insert(index, (IDbDataParameter)value);

        public void Remove(object value) => _items.Remove((IDbDataParameter)value);

        public void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
                _items.RemoveAt(index);
        }

        public void RemoveAt(int index) => _items.RemoveAt(index);
    }

    /// <summary>
    /// 供 Dapper 物化参数使用的最小数据库参数。
    /// </summary>
    private sealed class TestDbParameter : IDbDataParameter
    {
        public byte Precision { get; set; }

        public byte Scale { get; set; }

        public int Size { get; set; }

        public DbType DbType { get; set; }

        public ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        public bool IsNullable => true;

        public string ParameterName { get; set; }

        public string SourceColumn { get; set; }

        public DataRowVersion SourceVersion { get; set; }

        public object Value { get; set; }
    }
}