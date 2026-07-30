using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 创建 Fluent Mutation SQL Builder。
/// </summary>
public interface ISqlFluentMutationBuilderFactory
{
    /// <summary>
    /// 创建 Insert SQL Builder。
    /// </summary>
    ISqlInsertBuilder CreateInsert(ISqlProvider provider, SqlBuilderServices services);

    /// <summary>
    /// 创建 Update SQL Builder。
    /// </summary>
    ISqlUpdateBuilder CreateUpdate(ISqlProvider provider, SqlBuilderServices services);

    /// <summary>
    /// 创建 Delete SQL Builder。
    /// </summary>
    ISqlDeleteBuilder CreateDelete(ISqlProvider provider, SqlBuilderServices services);
}
