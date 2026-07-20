using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Builders.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 延迟渲染的结构化 SQL 表项。
/// </summary>
/// <remarks>
/// 仅由类型化 Builder 路径使用，原始字符串 API 仍使用 <see cref="SqlItem"/>，以保持既有点号和引用语义。
/// </remarks>
public sealed class StructuredSqlItem : SqlItem
{
    /// <summary>
    /// 结构化表引用。
    /// </summary>
    public SqlTableReference Reference { get; }

    /// <summary>
    /// SQL 对象名称格式化器。
    /// </summary>
    private readonly ISqlObjectNameFormatter _objectNameFormatter;

    /// <summary>
    /// 跨数据库查询校验器。
    /// </summary>
    private readonly ISqlCrossDatabaseQueryValidator _crossDatabaseQueryValidator;

    /// <summary>
    /// 源表引用。
    /// </summary>
    private readonly SqlTableReference _sourceReference;

    /// <summary>
    /// 执行数据库上下文。
    /// </summary>
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// 初始化一个<see cref="StructuredSqlItem"/>类型的实例。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    /// <param name="objectNameFormatter">SQL 对象名称格式化器。</param>
    /// <param name="databaseContext">执行数据库上下文。</param>
    /// <param name="crossDatabaseQueryValidator">跨数据库查询校验器。</param>
    /// <param name="sourceReference">源表引用。</param>
    public StructuredSqlItem(SqlTableReference reference, ISqlObjectNameFormatter objectNameFormatter,
        DatabaseContext databaseContext = null, ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null,
        SqlTableReference sourceReference = null)
        : base(reference?.ResolvedTableName)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        _objectNameFormatter = objectNameFormatter ?? new DefaultSqlObjectNameFormatter();
        _databaseContext = databaseContext;
        _crossDatabaseQueryValidator = crossDatabaseQueryValidator;
        _sourceReference = sourceReference;
    }

    /// <inheritdoc />
    public override string ToSql(IDialect dialect = null, ITableDatabase tableDatabase = null)
    {
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));
        _crossDatabaseQueryValidator?.Validate(_sourceReference, Reference, _databaseContext);
        var table = _objectNameFormatter.Format(Reference, dialect,
            _databaseContext?.DataSource?.DatabaseType ?? Reference.DatabaseType);
        return string.IsNullOrWhiteSpace(Reference.Alias) ? table :
            dialect.GetColumn(table, dialect.SafeName(Reference.Alias));
    }
}