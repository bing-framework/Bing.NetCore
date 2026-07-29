using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Delete SQL Builder。
/// </summary>
public interface ISqlDeleteBuilder : ISqlContent, IDelete, IDeleteClauseAccessor, ISqlMutationContextAccessor,
    IAllowAllRowsMutationBuilder
{
    /// <summary>
    /// 是否显式允许全表删除。
    /// </summary>
    bool AllowAllRows { get; }

    /// <summary>
    /// 创建同配置的空 Delete Builder。
    /// </summary>
    /// <returns>不包含子句和参数状态的 Delete Builder。</returns>
    ISqlDeleteBuilder New();

    /// <summary>
    /// 创建当前 Delete Builder 的独立副本。
    /// </summary>
    /// <returns>包含独立子句和参数状态的 Delete Builder。</returns>
    ISqlDeleteBuilder Clone();

    /// <summary>
    /// 清空当前 Delete Builder 的子句和参数状态。
    /// </summary>
    void Clear();

    /// <summary>
    /// 生成当前 Delete SQL。
    /// </summary>
    /// <returns>当前 Delete SQL 文本。</returns>
    string ToSql();
}