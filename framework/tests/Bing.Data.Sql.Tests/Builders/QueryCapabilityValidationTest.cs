using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// 查询语法能力校验测试。
/// </summary>
public class QueryCapabilityValidationTest
{
    /// <summary>
    /// 测试 - Provider 未确认 CTE 能力时，应在渲染前拒绝，不建立连接。
    /// </summary>
    [Fact]
    public void ToSql_WhenCteCapabilityIsInherited_ShouldRejectBeforeOpeningConnection()
    {
        // Arrange
        var builder = new CapabilitySqlBuilder(new SqlQueryCapabilities());
        var cte = builder.New().Select("Id").From("Source");
        builder.Select("Id").From("Active").With("Active", cte);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider capability.test 的当前查询能力配置不支持 CTE。", exception.Message);
    }

    /// <summary>
    /// 测试 - 数据源可显式确认 Provider 未知的 CTE 能力，且 Builder 应在构造后冻结该设置。
    /// </summary>
    [Fact]
    public void ToSql_WhenDataSourceConfirmsInheritedCte_ShouldRenderUsingFrozenCapabilities()
    {
        // Arrange
        var dataSourceCapabilities = new SqlQueryCapabilities { Cte = SqlQueryCapabilityState.Supported };
        var options = new SqlOptions().SetDatabaseContext(new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor { QueryCapabilities = dataSourceCapabilities }
        });
        var builder = new CapabilitySqlBuilder(new SqlQueryCapabilities(), options);
        var cte = builder.New().Select("Id").From("Source");
        builder.Select("Id").From("Active").With("Active", cte);
        dataSourceCapabilities.Cte = SqlQueryCapabilityState.Unsupported;

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("With [Active] \r\nAs (Select [Id] \r\nFrom [Source])\r\nSelect [Id] \r\nFrom [Active]", sql);
    }

    /// <summary>
    /// 测试 - SqlOptions 应优先于数据源能力配置，并允许收紧已支持的集合操作。
    /// </summary>
    [Fact]
    public void ToSql_WhenOptionsDisablesSupportedUnion_ShouldReject()
    {
        // Arrange
        var options = new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { Union = SqlQueryCapabilityState.Unsupported }
        }.SetDatabaseContext(new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor
            {
                QueryCapabilities = new SqlQueryCapabilities { Union = SqlQueryCapabilityState.Supported }
            }
        });
        var builder = new CapabilitySqlBuilder(new SqlQueryCapabilities { Union = SqlQueryCapabilityState.Supported }, options);
        builder.Select("Id").From("LeftSource").Union(builder.New().Select("Id").From("RightSource"));

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider capability.test 的当前查询能力配置不支持 Union。", exception.Message);
    }

    /// <summary>
    /// 测试 - Provider 明确不支持 Except 时，后续配置不得重新启用该语法。
    /// </summary>
    [Fact]
    public void ToSql_WhenProviderDisablesExcept_ShouldNotAllowOptionsToEnableIt()
    {
        // Arrange
        var options = new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { Except = SqlQueryCapabilityState.Supported }
        };
        var builder = new CapabilitySqlBuilder(new SqlQueryCapabilities { Except = SqlQueryCapabilityState.Unsupported }, options);
        builder.Select("Id").From("LeftSource").Except(builder.New().Select("Id").From("RightSource"));

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider capability.test 的当前查询能力配置不支持 Except。", exception.Message);
    }

    /// <summary>
    /// 测试 - 分页能力未确认时，应在 SQL 渲染前拒绝。
    /// </summary>
    [Fact]
    public void ToSql_WhenPaginationCapabilityIsInherited_ShouldReject()
    {
        // Arrange
        var builder = new CapabilitySqlBuilder(new SqlQueryCapabilities());
        builder.Select("Id").From("Source").OrderBy("Id").Page(new Pager(1, 10, "Id"));

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider capability.test 的当前查询能力配置不支持 分页。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Provider 明确不支持 Right Join 时，数据源和选项均不得重新启用该语法。
    /// </summary>
    [Fact]
    public void ToSql_WhenProviderDisablesRightJoin_ShouldNotAllowOverridesToEnableIt()
    {
        // Arrange
        var options = new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { RightJoin = SqlQueryCapabilityState.Supported }
        }.SetDatabaseContext(new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor
            {
                QueryCapabilities = new SqlQueryCapabilities { RightJoin = SqlQueryCapabilityState.Supported }
            }
        });
        var builder = new CapabilitySqlBuilder(
            new SqlQueryCapabilities { RightJoin = SqlQueryCapabilityState.Unsupported }, options);
        builder.Select("right_source.Id").From("left_source")
            .RightJoin("right_source").AppendOn("left_source.Id=right_source.Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider capability.test 的当前查询能力配置不支持 Right Join。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Provider 明确支持 Full Join 时，应按结构化表和 On 条件生成完整 SQL。
    /// </summary>
    [Fact]
    public void ToSql_WhenProviderSupportsFullJoin_ShouldRenderCompleteSql()
    {
        // Arrange
        var builder = new CapabilitySqlBuilder(new SqlQueryCapabilities
        {
            FullJoin = SqlQueryCapabilityState.Supported
        });
        builder.Select("s.Id").From("Samples", "s")
            .FullJoin("Reviews", "r").AppendOn("s.Id=r.SampleId");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Select [s].[Id] \r\nFrom [Samples] As [s] \r\nFull Join [Reviews] As [r] On s.Id=r.SampleId", sql);
    }

    /// <summary>
    /// 测试目的：Provider 明确不支持 Full Join 时，数据源和选项均不得重新启用该语法。
    /// </summary>
    [Fact]
    public void ToSql_WhenProviderDisablesFullJoin_ShouldNotAllowOverridesToEnableIt()
    {
        // Arrange
        var options = new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { FullJoin = SqlQueryCapabilityState.Supported }
        }.SetDatabaseContext(new DatabaseContext
        {
            DataSource = new SqlDataSourceDescriptor
            {
                QueryCapabilities = new SqlQueryCapabilities { FullJoin = SqlQueryCapabilityState.Supported }
            }
        });
        var builder = new CapabilitySqlBuilder(
            new SqlQueryCapabilities { FullJoin = SqlQueryCapabilityState.Unsupported }, options);
        builder.Select("right_source.Id").From("left_source")
            .FullJoin("right_source").AppendOn("left_source.Id=right_source.Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider capability.test 的当前查询能力配置不支持 Full Join。", exception.Message);
    }

    /// <summary>
    /// 测试用 Provider 能力生成器。
    /// </summary>
    private sealed class CapabilitySqlBuilder : SqlBuilderBase
    {
        private readonly SqlQueryCapabilities _providerCapabilities;

        /// <summary>
        /// 初始化测试 Builder。
        /// </summary>
        /// <param name="providerCapabilities">Provider 能力基线。</param>
        /// <param name="options">SQL 选项。</param>
        /// <param name="parameterManager">参数管理器。</param>
        public CapabilitySqlBuilder(SqlQueryCapabilities providerCapabilities, SqlOptions options = null,
            IParameterManager parameterManager = null)
            : base(new CapabilitySqlProvider(providerCapabilities),
                new SqlBuilderServices(options: options), parameterManager)
        {
            _providerCapabilities = providerCapabilities;
        }

        /// <inheritdoc />
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new CapabilitySqlBuilder(_providerCapabilities, Services.Options, parameterManager);
    }

    /// <summary>
    /// 测试用查询能力 Provider。
    /// </summary>
    private sealed class CapabilitySqlProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>
        /// 初始化测试 Provider。
        /// </summary>
        /// <param name="queryCapabilities">查询能力基线。</param>
        public CapabilitySqlProvider(SqlQueryCapabilities queryCapabilities) => Profile = new SqlProviderProfile
        {
            Query = new SqlProviderQueryCapabilities
            {
                Cte = queryCapabilities.Cte,
                Union = queryCapabilities.Union,
                UnionAll = queryCapabilities.UnionAll,
                Intersect = queryCapabilities.Intersect,
                Except = queryCapabilities.Except,
                RightJoin = queryCapabilities.RightJoin,
                FullJoin = queryCapabilities.FullJoin,
                Pagination = queryCapabilities.Pagination
            }
        };

        /// <inheritdoc />
        public string Key => "capability.test";

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect => TestDialect.Instance;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new CapabilityPaginationRenderer();

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => new ParamLiteralsResolver();

        /// <inheritdoc />
        public SqlProviderProfile Profile { get; }
    }

    /// <summary>
    /// 测试用分页渲染器。
    /// </summary>
    private sealed class CapabilityPaginationRenderer : ISqlPaginationRenderer
    {
        /// <inheritdoc />
        public string Render(string offsetParameterName, string limitParameterName) =>
            $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
    }
}
