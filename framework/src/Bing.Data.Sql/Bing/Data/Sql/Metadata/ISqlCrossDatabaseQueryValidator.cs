namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 类型化跨数据库查询校验器
/// </summary>
public interface ISqlCrossDatabaseQueryValidator
{
    /// <summary>
    /// 验证两个结构化表引用是否可在当前执行上下文中直接连接。
    /// </summary>
    /// <param name="source">源表引用。</param>
    /// <param name="target">目标表引用。</param>
    /// <param name="executionContext">执行数据库上下文。</param>
    void Validate(SqlTableReference source, SqlTableReference target, DatabaseContext executionContext);

    /// <summary>
    /// 校验连接表引用
    /// </summary>
    /// <param name="executionDbKey">执行数据源标识</param>
    /// <param name="reference">连接表引用</param>
    void ValidateJoin(string executionDbKey, SqlTableReference reference);
}