using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 供 Mutation Builder 单元测试使用的 SQL Provider。
/// </summary>
internal sealed class TestMutationSqlProvider : ISqlProvider
{
    /// <summary>
    /// 测试 Provider 单例。
    /// </summary>
    public static TestMutationSqlProvider Instance { get; } = new();

    /// <summary>
    /// 初始化一个 <see cref="TestMutationSqlProvider"/> 类型的实例。
    /// </summary>
    private TestMutationSqlProvider()
    {
    }

    /// <inheritdoc />
    public string Key => "test.mutation";

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.SqlServer;

    /// <inheritdoc />
    public IDialect Dialect => TestDialect.Instance;

    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer { get; } = new TestMutationPaginationRenderer();

    /// <inheritdoc />
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

    /// <inheritdoc />
    public IParamLiteralsResolver ParamLiteralsResolver { get; } = new ParamLiteralsResolver();

    /// <summary>
    /// 测试分页渲染器。
    /// </summary>
    private sealed class TestMutationPaginationRenderer : ISqlPaginationRenderer
    {
        /// <inheritdoc />
        public string Render(string offsetParameterName, string limitParameterName) =>
            $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
    }
}