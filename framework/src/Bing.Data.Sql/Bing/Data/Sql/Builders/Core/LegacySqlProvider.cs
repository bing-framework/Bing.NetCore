using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 仅用于过渡旧 Clause 构造器的 SQL 提供程序适配器。
/// </summary>
/// <remarks>
/// 该类型将在旧分散依赖构造器删除后移除。
/// </remarks>
internal sealed class LegacySqlProvider : ISqlProvider
{
    /// <summary>
    /// 初始化一个 <see cref="LegacySqlProvider"/> 类型的实例。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    public LegacySqlProvider(IDialect dialect) => Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.SqlServer;

    /// <inheritdoc />
    public IDialect Dialect { get; }

    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory => new DefaultSqlClauseFactory();

    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer => throw new NotSupportedException();

    /// <inheritdoc />
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

    /// <inheritdoc />
    public IParamLiteralsResolver ParamLiteralsResolver => new ParamLiteralsResolver();
}