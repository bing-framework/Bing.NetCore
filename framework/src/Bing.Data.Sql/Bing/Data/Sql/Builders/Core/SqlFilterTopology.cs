using Bing.Data.Queries;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 过滤器来源在查询图中的角色。
/// </summary>
public enum SqlFilterSourceKind
{
    /// <summary>
    /// 根 From 来源。
    /// </summary>
    Root,

    /// <summary>
    /// Join 引入的来源。
    /// </summary>
    Join
}

/// <summary>
/// 过滤器拓扑使用的 Join 类型。
/// </summary>
public enum SqlFilterJoinKind
{
    /// <summary>
    /// 内连接。
    /// </summary>
    Inner,

    /// <summary>
    /// 左外连接。
    /// </summary>
    Left,

    /// <summary>
    /// 右外连接。
    /// </summary>
    Right,

    /// <summary>
    /// 全外连接。
    /// </summary>
    Full,

    /// <summary>
    /// 交叉连接。
    /// </summary>
    Cross
}

/// <summary>
/// 过滤器可识别的结构化查询来源。
/// </summary>
public sealed class SqlFilterSource
{
    /// <summary>
    /// 初始化一个 <see cref="SqlFilterSource"/> 类型的实例。
    /// </summary>
    /// <param name="sourceId">查询图内稳定来源标识。</param>
    /// <param name="entityType">来源实体类型。</param>
    /// <param name="alias">来源别名。</param>
    /// <param name="kind">来源角色。</param>
    internal SqlFilterSource(string sourceId, Type entityType, string alias, SqlFilterSourceKind kind)
    {
        SourceId = sourceId;
        EntityType = entityType;
        Alias = alias;
        Kind = kind;
    }

    /// <summary>
    /// 获取查询图内稳定来源标识。
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// 获取关联实体类型。
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// 获取最终 SQL 中使用的表别名。
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// 获取来源在查询图中的角色。
    /// </summary>
    public SqlFilterSourceKind Kind { get; }
}

/// <summary>
/// 过滤器谓词放置时使用的 Join 边。
/// </summary>
public sealed class SqlFilterJoin
{
    /// <summary>
    /// 初始化一个 <see cref="SqlFilterJoin"/> 类型的实例。
    /// </summary>
    /// <param name="kind">Join 类型。</param>
    /// <param name="leftSourceIds">Join 左侧输入中所有结构化来源标识。</param>
    /// <param name="rightSourceId">Join 右侧结构化来源标识；原始来源时为 null。</param>
    internal SqlFilterJoin(SqlFilterJoinKind kind, IReadOnlyList<string> leftSourceIds, string rightSourceId)
    {
        Kind = kind;
        LeftSourceIds = leftSourceIds ?? Array.Empty<string>();
        RightSourceId = rightSourceId;
    }

    /// <summary>
    /// 获取Join 类型。
    /// </summary>
    public SqlFilterJoinKind Kind { get; }

    /// <summary>
    /// 获取Join 左侧输入中所有结构化来源标识。
    /// </summary>
    public IReadOnlyList<string> LeftSourceIds { get; }

    /// <summary>
    /// 获取Join 右侧结构化来源标识。
    /// </summary>
    /// <remarks>
    /// 原始来源时为 null。
    /// </remarks>
    public string RightSourceId { get; }
}

/// <summary>
/// 由过滤器提交、尚未绑定到具体 SQL Clause 的来源级谓词。
/// </summary>
internal sealed class SqlFilterPredicate
{
    /// <summary>
    /// 初始化一个 <see cref="SqlFilterPredicate"/> 类型的实例。
    /// </summary>
    /// <param name="sourceId">谓词归属来源标识。</param>
    /// <param name="column">已转义的完整列引用。</param>
    /// <param name="value">参数值。</param>
    /// <param name="operator">比较运算符。</param>
    public SqlFilterPredicate(string sourceId, string column, object value, Operator @operator)
    {
        SourceId = sourceId;
        Column = column;
        Value = value;
        Operator = @operator;
    }

    /// <summary>
    /// 获取谓词归属来源标识。
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// 获取已转义的完整列引用。
    /// </summary>
    public string Column { get; }

    /// <summary>
    /// 获取参数值。
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// 获取比较运算符。
    /// </summary>
    public Operator Operator { get; }
}

/// <summary>
/// 过滤器谓词的最终 SQL 放置位置。
/// </summary>
internal sealed class SqlFilterPlacement
{
    /// <summary>
    /// 初始化一个 <see cref="SqlFilterPlacement"/> 类型的实例。
    /// </summary>
    /// <param name="predicate">待放置谓词。</param>
    /// <param name="joinSourceId">目标 Join 右侧来源；null 表示最终 Where。</param>
    public SqlFilterPlacement(SqlFilterPredicate predicate, string joinSourceId = null)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        JoinSourceId = joinSourceId;
    }

    /// <summary>
    /// 获取待放置谓词。
    /// </summary>
    public SqlFilterPredicate Predicate { get; }

    /// <summary>
    /// 获取目标 Join 右侧来源。
    /// </summary>
    /// <remarks>
    /// null 表示最终 Where。
    /// </remarks>
    public string JoinSourceId { get; }
}
