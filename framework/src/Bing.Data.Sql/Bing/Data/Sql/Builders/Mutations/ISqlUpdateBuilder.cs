using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Update SQL Builder。
/// </summary>
public interface ISqlUpdateBuilder : ISqlContent, IUpdate, IUpdateClauseAccessor, ISqlMutationContextAccessor,
    IAllowAllRowsMutationBuilder
{
    /// <summary>
    /// 是否显式允许全表更新。
    /// </summary>
    bool AllowAllRows { get; }

    /// <summary>
    /// 创建同配置的空 Update Builder。
    /// </summary>
    /// <returns>不包含子句和参数状态的 Update Builder。</returns>
    ISqlUpdateBuilder New();

    /// <summary>
    /// 创建当前 Update Builder 的独立副本。
    /// </summary>
    /// <returns>包含独立子句和参数状态的 Update Builder。</returns>
    ISqlUpdateBuilder Clone();

    /// <summary>
    /// 清空当前 Update Builder 的子句和参数状态。
    /// </summary>
    void Clear();

    /// <summary>
    /// 生成当前 Update SQL。
    /// </summary>
    /// <returns>当前 Update SQL 文本。</returns>
    string ToSql();

    /// <summary>
    /// 生成当前 Update 的可执行命令快照。
    /// </summary>
    /// <returns>包含 SQL 与参数元数据的命令快照。</returns>
    SqlWriteCommand BuildCommand();
}