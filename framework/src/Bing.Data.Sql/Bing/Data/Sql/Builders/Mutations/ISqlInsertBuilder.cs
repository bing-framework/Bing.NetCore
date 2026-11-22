using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 用于构建 Insert SQL 的接口。
/// </summary>
public interface ISqlInsertBuilder : ISqlContent, IInsert, IInsertClauseAccessor, IReturningClauseAccessor,
    ISqlMutationContextAccessor
{
    /// <summary>
    /// 使用查询结果作为 Insert 数据源。
    /// </summary>
    /// <param name="query">使用相同 Provider 创建的来源查询。</param>
    /// <returns>当前 Insert Builder。</returns>
    ISqlInsertBuilder InsertSelect(ISqlBuilder query);

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

    /// <summary>
    /// 生成当前 Insert 的可执行命令快照。
    /// </summary>
    /// <returns>包含 SQL 与参数元数据的命令快照。</returns>
    SqlWriteCommand BuildCommand();
}
