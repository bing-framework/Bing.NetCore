using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Insert SQL Builder。
/// </summary>
public interface ISqlInsertBuilder : ISqlContent, IInsert, IInsertClauseAccessor, ISqlMutationContextAccessor
{
    /// <summary>
    /// 创建同配置的空 Insert Builder。
    /// </summary>
    /// <returns>不包含子句和参数状态的 Insert Builder。</returns>
    ISqlInsertBuilder New();

    /// <summary>
    /// 创建当前 Insert Builder 的独立副本。
    /// </summary>
    /// <returns>包含独立子句和参数状态的 Insert Builder。</returns>
    ISqlInsertBuilder Clone();

    /// <summary>
    /// 清空当前 Insert Builder 的子句和参数状态。
    /// </summary>
    void Clear();

    /// <summary>
    /// 生成当前 Insert SQL。
    /// </summary>
    /// <returns>当前 Insert SQL 文本。</returns>
    string ToSql();
}