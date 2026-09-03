using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using MutationSqlExecutionKind = Bing.Data.Sql.SqlExecutionKind;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// <see cref="SqlProviderProfile"/> 单元测试。
/// </summary>
public sealed class SqlProviderProfileTest
{
    /// <summary>
    /// 测试目的：统一 Profile 应按 Query、Mutation、Execution、Transaction、Procedure 和 Limits 责任域公开不可变能力。
    /// </summary>
    [Fact]
    public void Profile_WhenConfigured_ShouldPreserveAllCapabilityDomains()
    {
        // Arrange
        var profile = new SqlProviderProfile
        {
            Query = new SqlProviderQueryCapabilities
            {
                Cte = SqlQueryCapabilityState.Supported,
                Except = SqlQueryCapabilityState.Unsupported,
                RightJoin = SqlQueryCapabilityState.Supported,
                Pagination = SqlQueryCapabilityState.Supported
            },
            Mutation = new SqlProviderMutationCapabilities
            {
                SupportsMultiRowValues = false,
                MultiRowValuesFailureReason = SqlCapabilityFailureReason.DatabaseUnsupported,
                SupportsUpdateFrom = true,
                SupportsDeleteUsing = true,
                SupportsReturning = true,
                ReturningFailureReason = SqlCapabilityFailureReason.ProviderImplementationGap
            },
            Execution = new SqlProviderExecutionCapabilities
            {
                SupportsMultipleResultSets = true,
                MultipleResultSetsFailureReason = SqlCapabilityFailureReason.DatabaseUnsupported,
                SupportsStreaming = false,
                StreamingFailureReason = SqlCapabilityFailureReason.ProviderImplementationGap,
                SupportsCancellation = false,
                CancellationFailureReason = SqlCapabilityFailureReason.DatabaseUnsupported
            },
            Transaction = new SqlProviderTransactionCapabilities
            {
                SupportsTransactions = false,
                TransactionsFailureReason = SqlCapabilityFailureReason.DatabaseUnsupported,
                SupportsNativeAsyncBegin = true,
                SupportsNativeAsyncCommit = true,
                SupportsNativeAsyncRollback = false
            },
            Procedure = new SqlProviderProcedureCapabilities
            {
                SupportsStoredProcedures = false,
                StoredProceduresFailureReason = SqlCapabilityFailureReason.DatabaseUnsupported,
                SupportsOutputParameters = false,
                OutputParametersFailureReason = SqlCapabilityFailureReason.ProviderImplementationGap
            },
            Limits = new SqlProviderLimits { MaxParameterCount = 42 }
        };

        // Act
        // Assert
        Assert.True(profile.Execution.SupportsMultipleResultSets);
        Assert.False(profile.Execution.SupportsStreaming);
        Assert.Equal(SqlCapabilityFailureReason.DatabaseUnsupported,
            profile.Execution.MultipleResultSetsFailureReason);
        Assert.Equal(SqlCapabilityFailureReason.ProviderImplementationGap,
            profile.Execution.StreamingFailureReason);
        Assert.Equal(SqlCapabilityFailureReason.DatabaseUnsupported,
            profile.Execution.CancellationFailureReason);
        Assert.False(profile.Execution.SupportsCancellation);
        Assert.False(profile.Transaction.SupportsTransactions);
        Assert.Equal(SqlCapabilityFailureReason.DatabaseUnsupported, profile.Transaction.TransactionsFailureReason);
        Assert.True(profile.Transaction.SupportsNativeAsyncBegin);
        Assert.True(profile.Transaction.SupportsNativeAsyncCommit);
        Assert.False(profile.Transaction.SupportsNativeAsyncRollback);
        Assert.False(profile.Procedure.SupportsStoredProcedures);
        Assert.Equal(SqlCapabilityFailureReason.DatabaseUnsupported, profile.Procedure.StoredProceduresFailureReason);
        Assert.False(profile.Procedure.SupportsOutputParameters);
        Assert.Equal(SqlCapabilityFailureReason.ProviderImplementationGap,
            profile.Procedure.OutputParametersFailureReason);
        Assert.Equal(42, profile.Limits.MaxParameterCount);
        Assert.False(profile.Mutation.SupportsMultiRowValues);
        Assert.Equal(SqlCapabilityFailureReason.DatabaseUnsupported, profile.Mutation.MultiRowValuesFailureReason);
        Assert.True(profile.Mutation.SupportsUpdateFrom);
        Assert.True(profile.Mutation.SupportsDeleteUsing);
        Assert.True(profile.Mutation.SupportsReturning);
        Assert.Equal(SqlCapabilityFailureReason.ProviderImplementationGap, profile.Mutation.ReturningFailureReason);
        Assert.Equal(SqlQueryCapabilityState.Supported, profile.Query.Cte);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, profile.Query.Except);
        Assert.Equal(SqlQueryCapabilityState.Supported, profile.Query.RightJoin);
        Assert.Equal(SqlQueryCapabilityState.Supported, profile.Query.Pagination);
    }

    /// <summary>
    /// 测试目的：Provider Capability 快照必须复制原生异步事务标志，避免后续修改原 Profile 影响已绑定查询。
    /// </summary>
    [Fact]
    public void CreateSnapshot_WhenTransactionCapabilitiesAreConfigured_ShouldDeepCopyNativeAsyncFlags()
    {
        // Arrange
        var provider = ProfileSqlProvider.Instance;

        // Act
        var snapshot = SqlProviderCapabilityResolver.CreateSnapshot(provider);

        // Assert
        Assert.True(snapshot.Transaction.SupportsNativeAsyncBegin);
        Assert.False(snapshot.Transaction.SupportsNativeAsyncCommit);
        Assert.True(snapshot.Transaction.SupportsNativeAsyncRollback);
        Assert.Equal(SqlCapabilityFailureReason.DatabaseUnsupported, snapshot.Mutation.ReturningFailureReason);
        Assert.Equal(SqlCapabilityFailureReason.ProviderImplementationGap,
            snapshot.Execution.MultipleResultSetsFailureReason);
        Assert.Equal(SqlCapabilityFailureReason.DatabaseUnsupported,
            snapshot.Procedure.OutputParametersFailureReason);
        Assert.NotSame(provider.Profile.Transaction, snapshot.Transaction);
        Assert.NotSame(provider.Profile.Query, snapshot.Query);
        Assert.NotSame(provider.Profile.Mutation, snapshot.Mutation);
        Assert.NotSame(provider.Profile.Execution, snapshot.Execution);
        Assert.NotSame(provider.Profile.Procedure, snapshot.Procedure);
        Assert.NotSame(provider.Profile.Limits, snapshot.Limits);
    }

    /// <summary>
    /// 测试目的：核心查询校验应仅使用统一 Profile。
    /// </summary>
    [Fact]
    public void ToSql_WhenProfileDeclaresUnsupported_ShouldReject()
    {
        // Arrange
        var builder = new ProfileSqlBuilder();
        builder.Select("Id").From("LeftSource").Union(builder.New().Select("Id").From("RightSource"));

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider profile.test 的当前查询能力配置不支持 Union。", exception.Message);
    }

    /// <summary>
    /// 测试目的：未声明 Profile 的第三方 Provider 必须默认关闭可能创建外部资源或生成方言 SQL 的能力。
    /// </summary>
    [Fact]
    public void GetProfile_WhenProviderDoesNotImplementProfileContract_ShouldFailClosed()
    {
        // Arrange
        var provider = new NoProfileSqlProvider();

        // Act
        var profile = SqlProviderCapabilityResolver.GetProfile(provider);

        // Assert
        Assert.False(profile.Mutation.SupportsMultiRowValues);
        Assert.False(profile.Execution.SupportsStreaming);
        Assert.False(profile.Execution.SupportsCancellation);
        Assert.False(profile.Transaction.SupportsTransactions);
        Assert.False(profile.Procedure.SupportsStoredProcedures);
        Assert.False(profile.Procedure.SupportsOutputParameters);
    }

    /// <summary>
    /// 测试目的：能力拒绝异常应同时保留既有消息和可编程读取的结构化原因。
    /// </summary>
    [Theory]
    [InlineData(SqlCapabilityFailureReason.DatabaseUnsupported)]
    [InlineData(SqlCapabilityFailureReason.ProviderImplementationGap)]
    [InlineData(SqlCapabilityFailureReason.ProviderProfileMissing)]
    [InlineData(SqlCapabilityFailureReason.ProviderProfileMismatch)]
    public void CapabilityFailure_WhenCreated_ShouldPreserveReasonAndMessage(SqlCapabilityFailureReason reason)
    {
        // Arrange
        const string message = "能力拒绝消息。";

        // Act
        var exception = SqlCapabilityFailure.Create(reason, "TestCapability", "test.provider", message);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.True(SqlCapabilityFailure.TryGetReason(exception, out var actualReason));
        Assert.Equal(reason, actualReason);
    }

    /// <summary>
    /// 测试目的：未附加结构化数据或附加未知值时，原因读取器必须返回 false，避免误分类异常。
    /// </summary>
    [Fact]
    public void TryGetReason_WhenExceptionDoesNotContainValidReason_ShouldReturnFalse()
    {
        // Arrange
        var missing = new NotSupportedException("missing");
        var invalid = new NotSupportedException("invalid");
        invalid.Data["Bing.Data.Sql.CapabilityFailureReason"] = "Unknown";

        // Act
        var missingResult = SqlCapabilityFailure.TryGetReason(missing, out _);
        var invalidResult = SqlCapabilityFailure.TryGetReason(invalid, out _);

        // Assert
        Assert.False(missingResult);
        Assert.False(invalidResult);
    }

    /// <summary>
    /// 测试目的：未声明 Profile 的 Mutation 能力校验应标记 ProfileMissing，而不是误报 Provider 实现缺口。
    /// </summary>
    [Fact]
    public void ValuesClause_WhenProviderProfileIsMissing_ShouldClassifyProfileMissing()
    {
        // Arrange
        var context = new SqlMutationContext(NoProfileSqlProvider.Instance,
            new ParameterManager(NoProfileSqlProvider.Instance.Dialect), new SqlBuilderServices(),
            new SqlBuilderExecutionContext(null));
        var values = new ValuesClause(context);
        values.AddRow(new object[] { "Bing" });
        values.AddRow(new object[] { "Framework" });
        var validationContext = new SqlValidationContext(NoProfileSqlProvider.Instance,
            context.ParameterManager.Count, false, MutationSqlExecutionKind.Insert);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
        {
            values.Validate(validationContext);
        });

        // Assert
        Assert.True(SqlCapabilityFailure.TryGetReason(exception, out var reason));
        Assert.Equal(SqlCapabilityFailureReason.ProviderProfileMissing, reason);
    }

    /// <summary>
    /// 测试目的：Profile 声明但能力域不完整时，多行 Values 应标记 ProfileMismatch，避免继续进入渲染路径。
    /// </summary>
    [Fact]
    public void ValuesClause_WhenProviderProfileIsIncomplete_ShouldClassifyProfileMismatch()
    {
        // Arrange
        var provider = new IncompleteProfileSqlProvider();
        var context = new SqlMutationContext(provider, new ParameterManager(provider.Dialect), new SqlBuilderServices(),
            new SqlBuilderExecutionContext(null));
        var values = new ValuesClause(context);
        values.AddRow(new object[] { "Bing" });
        values.AddRow(new object[] { "Framework" });
        var validationContext = new SqlValidationContext(provider, context.ParameterManager.Count, false,
            MutationSqlExecutionKind.Insert);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => values.Validate(validationContext));

        // Assert
        Assert.True(SqlCapabilityFailure.TryGetReason(exception, out var reason));
        Assert.Equal(SqlCapabilityFailureReason.ProviderProfileMismatch, reason);
    }

    /// <summary>
    /// 测试目的：Profile 不完整且查询能力被拒绝时，应返回 ProfileMismatch 而不是 ProviderImplementationGap。
    /// </summary>
    [Fact]
    public void ToSql_WhenProviderProfileIsIncomplete_ShouldClassifyProfileMismatch()
    {
        // Arrange
        var builder = new IncompleteProfileSqlBuilder();
        builder.Select("Id").Union(builder.New().Select("Id"));

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.True(SqlCapabilityFailure.TryGetReason(exception, out var reason));
        Assert.Equal(SqlCapabilityFailureReason.ProviderProfileMismatch, reason);
    }

    /// <summary>
    /// 测试用 Profile SQL Builder。
    /// </summary>
    private sealed class ProfileSqlBuilder : SqlBuilderBase
    {
        /// <summary>
        /// 初始化测试 Builder。
        /// </summary>
        public ProfileSqlBuilder() : this(null) { }

        /// <summary>
        /// 初始化携带指定参数管理器的测试 Builder。
        /// </summary>
        /// <param name="parameterManager">当前 Builder 使用的参数管理器。</param>
        private ProfileSqlBuilder(IParameterManager parameterManager)
            : base(ProfileSqlProvider.Instance, new SqlBuilderServices(), parameterManager)
        {
        }

        /// <inheritdoc />
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new ProfileSqlBuilder(parameterManager);
    }

    /// <summary>
    /// 仅暴露统一能力档案的测试 Provider。
    /// </summary>
    private sealed class ProfileSqlProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>
        /// 可共享的测试 Provider 单例。
        /// </summary>
        public static ProfileSqlProvider Instance { get; } = new();

        /// <inheritdoc />
        public string Key => "profile.test";

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect => TestDialect.Instance;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new ProfilePaginationRenderer();

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => new ParamLiteralsResolver();

        /// <inheritdoc />
        public SqlProviderProfile Profile { get; } = new()
        {
            Query = new SqlProviderQueryCapabilities { Union = SqlQueryCapabilityState.Unsupported },
            Mutation = new SqlProviderMutationCapabilities
            {
                ReturningFailureReason = SqlCapabilityFailureReason.DatabaseUnsupported
            },
            Execution = new SqlProviderExecutionCapabilities
            {
                MultipleResultSetsFailureReason = SqlCapabilityFailureReason.ProviderImplementationGap
            },
            Transaction = new SqlProviderTransactionCapabilities
            {
                SupportsNativeAsyncBegin = true,
                SupportsNativeAsyncCommit = false,
                SupportsNativeAsyncRollback = true
            },
            Procedure = new SqlProviderProcedureCapabilities
            {
                OutputParametersFailureReason = SqlCapabilityFailureReason.DatabaseUnsupported
            }
        };

    }

    /// <summary>
    /// 未实现 Profile 契约的第三方 Provider 测试替身。
    /// </summary>
    private sealed class NoProfileSqlProvider : ISqlProvider
    {
        /// <summary>
        /// 未声明 Profile 的测试 Provider 单例。
        /// </summary>
        public static NoProfileSqlProvider Instance { get; } = new();

        /// <inheritdoc />
        public string Key => "profile.no-contract";

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect => TestDialect.Instance;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new ProfilePaginationRenderer();

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => new ParamLiteralsResolver();
    }

    /// <summary>
    /// 声明但缺失能力域的测试 Provider。
    /// </summary>
    private sealed class IncompleteProfileSqlProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <inheritdoc />
        public string Key => "profile.incomplete";

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect => TestDialect.Instance;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new ProfilePaginationRenderer();

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => new ParamLiteralsResolver();

        /// <inheritdoc />
        public SqlProviderProfile Profile { get; } = new()
        {
            Query = null
        };
    }

    /// <summary>
    /// 使用不完整 Profile 的查询 Builder 测试替身。
    /// </summary>
    private sealed class IncompleteProfileSqlBuilder : SqlBuilderBase
    {
        /// <summary>
        /// 初始化测试 Builder。
        /// </summary>
        public IncompleteProfileSqlBuilder() : base(new IncompleteProfileSqlProvider(), new SqlBuilderServices())
        {
        }

        /// <inheritdoc />
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new IncompleteProfileSqlBuilder();
    }

    /// <summary>
    /// 渲染测试 Provider 使用的分页 SQL。
    /// </summary>
    private sealed class ProfilePaginationRenderer : ISqlPaginationRenderer
    {
        /// <inheritdoc />
        public string Render(string offsetParameterName, string limitParameterName) =>
            $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
    }
}