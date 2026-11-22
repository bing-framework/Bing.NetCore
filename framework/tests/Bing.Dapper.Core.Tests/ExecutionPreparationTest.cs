using Bing.Data.Sql.Builders.Mutations;
using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Mutations;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// SQL 执行准备的延迟工作测试。
/// </summary>
public class ExecutionPreparationTest
{
    /// <summary>
    /// 测试目的：同步 Direct Builder 执行在 Trace 和诊断均关闭时不得生成调试 SQL 或参数元数据。
    /// </summary>
    [Fact]
    public void ExecuteMutation_WhenTraceAndDiagnosticsAreDisabled_ShouldNotRenderDebugSqlOrCreateDiagnostics()
    {
        // Arrange
        var builder = CreateBuilder();
        var executor = CreateExecutor();

        // Act
        var result = executor.ExecuteWrite(builder.Object.ToSqlWriteCommand());

        // Assert
        Assert.Equal(1, result);
        builder.Verify(item => item.BuildCommand(), Times.Once);
        Assert.Equal(0, executor.BuilderDiagnosticRequests);
    }

    /// <summary>
    /// 测试目的：异步 Direct Builder 执行在 Trace 和诊断均关闭时不得生成调试 SQL 或参数元数据。
    /// </summary>
    [Fact]
    public async Task ExecuteMutationAsync_WhenTraceAndDiagnosticsAreDisabled_ShouldNotRenderDebugSqlOrCreateDiagnostics()
    {
        // Arrange
        var builder = CreateBuilder();
        var executor = CreateExecutor();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteWriteAsync(builder.Object.ToSqlWriteCommand()));

        // Assert
        Assert.Contains("Async operations require use of a DbConnection", exception.Message);
        builder.Verify(item => item.BuildCommand(), Times.Once);
        Assert.Equal(0, executor.BuilderDiagnosticRequests);
    }

    /// <summary>
    /// 测试目的：普通同步写入命令的 Provider 与 Executor 不一致时，必须在创建命令前拒绝。
    /// </summary>
    [Fact]
    public void ExecuteMutation_WhenWriteCommandProviderMismatches_ShouldRejectBeforeCommandCreation()
    {
        // Arrange
        var builder = CreateBuilder("bing.sqlite");
        var executor = CreateExecutor();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => executor.ExecuteWrite(builder.Object.ToSqlWriteCommand()));

        // Assert
        Assert.Equal("写入命令 Provider bing.sqlite 与当前 Executor Provider bing.sqlserver 不一致，不能执行。",
            exception.Message);
    }

    /// <summary>
    /// 测试目的：普通异步写入命令的 Provider 与 Executor 不一致时，必须在异步事务或命令创建前拒绝。
    /// </summary>
    [Fact]
    public async Task ExecuteMutationAsync_WhenWriteCommandProviderMismatches_ShouldRejectBeforeCommandCreation()
    {
        // Arrange
        var builder = CreateBuilder("bing.sqlite");
        var executor = CreateExecutor();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteWriteAsync(builder.Object.ToSqlWriteCommand()));

        // Assert
        Assert.Equal("写入命令 Provider bing.sqlite 与当前 Executor Provider bing.sqlserver 不一致，不能执行。",
            exception.Message);
    }

    /// <summary>
    /// 测试目的：专用 Update Builder 生成的命令应携带匹配的 Provider 和操作类型，并可通过写入执行入口执行。
    /// </summary>
    [Fact]
    public void ExecuteWrite_WhenCommandComesFromDedicatedMutationBuilder_ShouldExecute()
    {
        // Arrange
        var provider = CreateMutationProvider();
        var executor = CreateExecutor(provider);
        var builder = new SqlUpdateBuilder(provider, new SqlBuilderServices());
        builder.MutationContext.ParameterManager.Add("@_p_1", 7);
        builder.Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "updated")
            .Where(new EqualCondition("[Id]", "@_p_1"));
        var command = builder.BuildCommand();

        // Act
        var result = executor.ExecuteWrite(command);

        // Assert
        Assert.Equal(1, result);
    }

    /// <summary>
    /// 测试目的：生成式写入命令应直接使用生成绑定器，跳过通用参数绑定器。
    /// </summary>
    [Fact]
    public void PrepareGeneratedMutationCommand_ShouldUseGeneratedParameterSource()
    {
        // Arrange
        var callbackCalled = false;
        var executor = CreateExecutor(parameterBinder: new ThrowingParameterBinder(),
            executionMode: SqlExecutionMode.GeneratedOnly);
        var command = new SqlWriteCommand(
            "Update Users Set Name=@_p_0 Where Id=@_p_1",
            new[]
            {
                new SqlParam("@_p_0", "Ada"),
                new SqlParam("@_p_1", 7)
            })
            .WithGeneratedParameterBinder((_, parameters) =>
            {
                callbackCalled = true;
                Assert.Equal(new[] { "@_p_0", "@_p_1" }, parameters.Select(item => item.Name));
            });

        // Act
        var prepared = executor.PrepareGeneratedCommand(command);
        var staticSource = Assert.IsAssignableFrom<ISqlStaticParameterSource>(prepared.DapperParameters);
        staticSource.BindTo(null);

        // Assert
        Assert.True(callbackCalled);
        Assert.Equal(command.Sql, prepared.Sql);
        Assert.Equal(command.CreateParameters().Select(item => item.Name),
            Assert.IsAssignableFrom<IEnumerable<SqlParam>>(prepared.ParameterSource).Select(item => item.Name));
    }

    /// <summary>
    /// 测试目的：兼容模式仍应把生成命令交给通用参数绑定器，保证 Dapper 动态执行可以消费参数。
    /// </summary>
    [Fact]
    public void PrepareGeneratedMutationCommand_InCompatibleMode_ShouldUseGenericParameterSource()
    {
        // Arrange
        var expected = new object();
        var executor = CreateExecutor(parameterBinder: new FixedParameterBinder(expected),
            executionMode: SqlExecutionMode.Compatible);
        var command = new SqlWriteCommand("Update Users Set Name=@_p_0 Where Id=@_p_1", new[]
        {
            new SqlParam("@_p_0", "Ada"),
            new SqlParam("@_p_1", 7)
        }).WithGeneratedParameterBinder((_, _) => throw new Xunit.Sdk.XunitException("兼容模式不应调用静态绑定器。"));

        // Act
        var prepared = executor.PrepareGeneratedCommand(command);

        // Assert
        Assert.Same(expected, prepared.DapperParameters);
        Assert.False(prepared.DapperParameters is ISqlStaticParameterSource);
    }

    /// <summary>
    /// 测试目的：离线创建的命令在绑定时应从执行选项补齐缺失的数据库类型，保留显式类型和冻结输入。
    /// </summary>
    [Theory]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    [InlineData(SqlExecutionMode.Compatible)]
    public void PrepareWriteCommand_UsesExecutorTypeOnlyWhenParameterTypeIsMissing(SqlExecutionMode mode)
    {
        using var executor = new PreparationTestExecutor(CreateServiceProvider(executionMode: mode),
            new SqlOptions { DatabaseType = DatabaseType.PgSql });
        var command = new SqlWriteCommand("Update sample Set p = @p Where id = @id", new[]
        {
            new SqlParam("@p", "{}") { ProviderTypeName = "jsonb" },
            new SqlParam("@id", 1) { DatabaseType = DatabaseType.MySql }
        }).WithGeneratedParameterBinder(static (_, _) => { });

        var prepared = executor.PrepareGeneratedCommand(command);

        Assert.Collection(Assert.IsAssignableFrom<IEnumerable<SqlParam>>(prepared.ParameterSource),
            parameter =>
            {
                Assert.Equal(DatabaseType.PgSql, parameter.DatabaseType);
                Assert.Equal("jsonb", parameter.ProviderTypeName);
            },
            parameter => Assert.Equal(DatabaseType.MySql, parameter.DatabaseType));
        Assert.Null(command.CreateParameters().First().DatabaseType);
    }

    /// <summary>
    /// 创建用于 Direct Builder 执行的测试 Builder。
    /// </summary>
    /// <param name="providerKey">Builder 使用的 Provider 标识。</param>
    /// <returns>返回配置完成的 SQL Builder 测试替身。</returns>
    private static Mock<ISqlUpdateBuilder> CreateBuilder(string providerKey = "bing.sqlserver")
    {
        var builder = new Mock<ISqlUpdateBuilder>();
        var provider = new Mock<ISqlProvider>();
        provider.SetupGet(item => item.Key).Returns(providerKey);
        builder.Setup(item => item.Clone()).Returns(builder.Object);
        builder.Setup(item => item.ToSql()).Returns("Update samples Set Name = 'updated'");
        builder.Setup(item => item.BuildCommand()).Returns(() => new SqlWriteCommand(builder.Object.ToSql(), Array.Empty<SqlParam>(),
            provider.Object, SqlOperationKind.Update));
        return builder;
    }

    /// <summary>
    /// 创建具有外部连接和空参数绑定器的测试执行器。
    /// </summary>
    /// <param name="provider">执行器使用的 SQL Provider；省略时使用默认测试 Provider。</param>
    /// <returns>返回配置了外部连接的测试执行器。</returns>
    private static PreparationTestExecutor CreateExecutor(ISqlProvider provider = null,
        ISqlParameterBinder parameterBinder = null, SqlExecutionMode? executionMode = null)
    {
        var command = new Mock<IDbCommand>();
        command.SetupGet(item => item.Parameters).Returns(new Mock<IDataParameterCollection>().Object);
        command.Setup(item => item.CreateParameter()).Returns(new Mock<IDbDataParameter>().Object);
        command.Setup(item => item.ExecuteNonQuery()).Returns(1);
        var connection = new Mock<IDbConnection>();
        connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
        connection.Setup(item => item.CreateCommand()).Returns(command.Object);
        return new PreparationTestExecutor(CreateServiceProvider(provider, parameterBinder, executionMode),
            new SqlOptions { Connection = connection.Object });
    }

    /// <summary>
    /// 创建测试服务提供程序。
    /// </summary>
    /// <param name="sqlProvider">要注册的 SQL Provider；省略时注册默认测试 Provider。</param>
    /// <returns>返回包含 SQL Core 服务和指定 Provider 的服务容器。</returns>
    private static IServiceProvider CreateServiceProvider(ISqlProvider sqlProvider = null,
        ISqlParameterBinder parameterBinder = null, SqlExecutionMode? executionMode = null)
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        if (executionMode.HasValue)
            services.AddSingleton(new SqlMetadataOptions { ExecutionMode = executionMode.Value });
        if (parameterBinder != null)
            services.AddSingleton(parameterBinder);
        if (sqlProvider == null)
        {
            var provider = new Mock<ISqlProvider>();
            provider.SetupGet(item => item.Key).Returns("bing.sqlserver");
            sqlProvider = provider.Object;
        }
        services.AddSingleton(sqlProvider);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建足以驱动专用 Mutation Builder 的 SQL Server 风格 Provider。
    /// </summary>
    /// <returns>返回用于 Mutation Builder 测试的 SQL Provider。</returns>
    private static ISqlProvider CreateMutationProvider()
    {
        var dialect = new Mock<IDialect>();
        dialect.SetupGet(item => item.OpeningIdentifier).Returns('[');
        dialect.SetupGet(item => item.ClosingIdentifier).Returns(']');
        dialect.SetupGet(item => item.BatchSeperator).Returns(';');
        dialect.Setup(item => item.SafeName(It.IsAny<string>()))
            .Returns((string name) => $"[{name}]");
        dialect.Setup(item => item.GetPrefix()).Returns("@");
        dialect.Setup(item => item.SupportSelectAs()).Returns(true);
        dialect.Setup(item => item.GenerateName(It.IsAny<int>()))
            .Returns((int index) => $"_p_{index}");
        dialect.Setup(item => item.GetParamName(It.IsAny<string>()))
            .Returns((string name) => name.StartsWith("@", StringComparison.Ordinal) ? name : $"@{name}");
        dialect.Setup(item => item.GetParamValue(It.IsAny<object>()))
            .Returns((object value) => value);

        var provider = new Mock<ISqlProvider>();
        provider.SetupGet(item => item.Key).Returns("bing.sqlserver");
        provider.SetupGet(item => item.Dialect).Returns(dialect.Object);
        provider.SetupGet(item => item.ClauseFactory).Returns(new DefaultSqlClauseFactory());
        provider.SetupGet(item => item.TableReferenceParser).Returns(DefaultSqlTableReferenceParser.Instance);
        provider.SetupGet(item => item.ParameterManagerFactory).Returns(DefaultParameterManagerFactory.Instance);
        provider.SetupGet(item => item.ParamLiteralsResolver).Returns(new ParamLiteralsResolver());
        return provider.Object;
    }

    /// <summary>
    /// 记录诊断元数据请求次数的执行器测试替身。
    /// </summary>
    private sealed class PreparationTestExecutor : SqlExecutorBase
    {
        /// <summary>
        /// 初始化一个 <see cref="PreparationTestExecutor"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public PreparationTestExecutor(IServiceProvider serviceProvider, SqlOptions options)
            : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 获取或设置Builder 参数诊断元数据请求次数。
        /// </summary>
        public int BuilderDiagnosticRequests { get; private set; }

        /// <summary>
        /// 暴露写入命令准备入口，验证生成式参数源不会回退到通用绑定器。
        /// </summary>
        public SqlPreparedCommand PrepareGeneratedCommand(SqlWriteCommand command) => PrepareCommand(command);

        /// <inheritdoc />
        protected override IReadOnlyCollection<SqlParameterDiagnosticInfo> GetSqlParameterDiagnostics(
            ISqlBuilder builder, string sql)
        {
            BuilderDiagnosticRequests++;
            return Array.Empty<SqlParameterDiagnosticInfo>();
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 若生成式参数源未被采用则立即失败的通用绑定器替身。
    /// </summary>
    private sealed class ThrowingParameterBinder : ISqlParameterBinder
    {
        public object Bind(ISqlBuilder builder) => throw new Xunit.Sdk.XunitException("不应调用通用 Builder 参数绑定器。");

        public object Bind(object parameter) => throw new Xunit.Sdk.XunitException("不应调用通用对象参数绑定器。");
    }

    /// <summary>
    /// 返回固定对象的参数绑定器替身，用于区分兼容参数路径和静态参数源。
    /// </summary>
    private sealed class FixedParameterBinder : ISqlParameterBinder
    {
        private readonly object _value;

        public FixedParameterBinder(object value) => _value = value;

        public object Bind(ISqlBuilder builder) => _value;

        public object Bind(object parameter) => _value;
    }

}
