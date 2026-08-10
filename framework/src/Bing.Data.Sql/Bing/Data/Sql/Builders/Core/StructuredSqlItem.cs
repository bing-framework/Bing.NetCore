using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Builders.Extensions;
using Bing.Data.Enums;

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
    /// 执行数据库上下文。
    /// </summary>
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Builder 固定的数据库类型。
    /// </summary>
    private readonly DatabaseType? _providerDatabaseType;

    /// <summary>
    /// SQL 表引用验证器。
    /// </summary>
    private readonly ISqlTableReferenceValidator _tableReferenceValidator;

    /// <summary>
    /// 跨数据库查询校验器。
    /// </summary>
    private readonly ISqlCrossDatabaseQueryValidator _crossDatabaseQueryValidator;

    /// <summary>
    /// 结构化 Join 的源表引用。
    /// </summary>
    private readonly SqlTableReference _sourceReference;

    /// <summary>
    /// 是否已完成跨数据库关系校验。
    /// </summary>
    private bool _isCrossDatabaseValidated;

    /// <summary>
    /// 初始化一个<see cref="StructuredSqlItem"/>类型的实例。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    /// <param name="objectNameFormatter">SQL 对象名称格式化器。</param>
    /// <param name="databaseContext">执行数据库上下文。</param>
    /// <param name="databaseType">Builder 固定的数据库类型。</param>
    /// <param name="tableReferenceValidator">SQL 表引用验证器。</param>
    /// <param name="crossDatabaseQueryValidator">跨数据库查询校验器。</param>
    /// <param name="sourceReference">结构化 Join 的源表引用。</param>
    public StructuredSqlItem(SqlTableReference reference, ISqlObjectNameFormatter objectNameFormatter,
            DatabaseContext databaseContext = null, DatabaseType? databaseType = null,
        ISqlTableReferenceValidator tableReferenceValidator = null,
        ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null,
        SqlTableReference sourceReference = null)
        : base(reference?.TableName)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        _objectNameFormatter = objectNameFormatter ?? new DefaultSqlObjectNameFormatter();
        _databaseContext = databaseContext;
        _providerDatabaseType = databaseType;
        _tableReferenceValidator = tableReferenceValidator ?? new DefaultSqlTableReferenceValidator();
        _crossDatabaseQueryValidator = crossDatabaseQueryValidator;
        _sourceReference = sourceReference;
    }

    /// <inheritdoc />
    public override string ToSql(IDialect dialect = null)
    {
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));
        var databaseType = _databaseContext?.DataSource?.DatabaseType ?? _providerDatabaseType;
        if (databaseType == null)
            throw new InvalidOperationException("无法确定结构化表引用的数据库类型。");
        _tableReferenceValidator.Validate(Reference, databaseType.Value);
        ValidateCrossDatabaseReference();
        var table = _objectNameFormatter.Format(Reference, dialect, databaseType);
        return string.IsNullOrWhiteSpace(Reference.Alias) ? table :
            dialect.GetColumn(table, dialect.SafeName(Reference.Alias));
    }

    /// <summary>
    /// 验证结构化 Join 的跨数据库关系。
    /// </summary>
    private void ValidateCrossDatabaseReference()
    {
        if (_isCrossDatabaseValidated || _crossDatabaseQueryValidator == null)
            return;
        _crossDatabaseQueryValidator.Validate(_databaseContext, _sourceReference, Reference);
        _isCrossDatabaseValidated = true;
    }

    /// <inheritdoc />
    public override SqlItem Clone() => new StructuredSqlItem(Reference, _objectNameFormatter, _databaseContext,
        _providerDatabaseType, _tableReferenceValidator, _crossDatabaseQueryValidator, _sourceReference);
}