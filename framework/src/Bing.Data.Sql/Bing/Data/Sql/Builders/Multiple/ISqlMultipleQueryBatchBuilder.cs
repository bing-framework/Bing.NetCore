using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Multiple;

/// <summary>
/// 组合多个查询语句为单次数据库往返命令的 Builder。
/// </summary>
public interface ISqlMultipleQueryBatchBuilder
{
    /// <summary>
    /// 追加一个结果集查询语句。
    /// </summary>
    /// <param name="sql">不含批处理分隔符的 SQL 语句。</param>
    /// <param name="parameters">该语句使用的参数；参数名不得与已追加语句重复。</param>
    /// <returns>当前 Builder。</returns>
    ISqlMultipleQueryBatchBuilder Append(string sql, IEnumerable<SqlParam> parameters = null);

    /// <summary>
    /// 生成不可变的多结果集命令快照。
    /// </summary>
    /// <returns>组合后的可执行命令。</returns>
    SqlMultipleQueryCommand Build();
}