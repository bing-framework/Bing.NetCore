using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

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
                SupportsUpdateFrom = true,
                SupportsDeleteUsing = true,
                SupportsReturning = true
            },
            Execution = new SqlProviderExecutionCapabilities
            {
                SupportsMultipleResultSets = true,
                SupportsStreaming = false,
                SupportsCancellation = false
            },
            Transaction = new SqlProviderTransactionCapabilities { SupportsTransactions = false },
            Procedure = new SqlProviderProcedureCapabilities
            {
                SupportsStoredProcedures = false,
                SupportsOutputParameters = false
            },
            Limits = new SqlProviderLimits { MaxParameterCount = 42 }
        };

        // Act
        // Assert
        Assert.True(profile.Execution.SupportsMultipleResultSets);
        Assert.False(profile.Execution.SupportsStreaming);
        Assert.False(profile.Execution.SupportsCancellation);
        Assert.False(profile.Transaction.SupportsTransactions);
        Assert.False(profile.Procedure.SupportsStoredProcedures);
        Assert.False(profile.Procedure.SupportsOutputParameters);
        Assert.Equal(42, profile.Limits.MaxParameterCount);
        Assert.False(profile.Mutation.SupportsMultiRowValues);
        Assert.True(profile.Mutation.SupportsUpdateFrom);
        Assert.True(profile.Mutation.SupportsDeleteUsing);
        Assert.True(profile.Mutation.SupportsReturning);
        Assert.Equal(SqlQueryCapabilityState.Supported, profile.Query.Cte);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, profile.Query.Except);
        Assert.Equal(SqlQueryCapabilityState.Supported, profile.Query.RightJoin);
        Assert.Equal(SqlQueryCapabilityState.Supported, profile.Query.Pagination);
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
            Query = new SqlProviderQueryCapabilities { Union = SqlQueryCapabilityState.Unsupported }
        };

    }

    /// <summary>
    /// 未实现 Profile 契约的第三方 Provider 测试替身。
    /// </summary>
    private sealed class NoProfileSqlProvider : ISqlProvider
    {
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
    /// 渲染测试 Provider 使用的分页 SQL。
    /// </summary>
    private sealed class ProfilePaginationRenderer : ISqlPaginationRenderer
    {
        /// <inheritdoc />
        public string Render(string offsetParameterName, string limitParameterName) =>
            $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
    }
}