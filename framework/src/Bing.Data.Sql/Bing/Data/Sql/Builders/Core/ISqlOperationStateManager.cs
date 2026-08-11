namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 统一 SQL Builder 的内部操作状态管理器。
/// </summary>
internal interface ISqlOperationStateManager
{
    /// <summary>
    /// 验证当前 Fluent 操作是否可执行，但不修改 Builder 状态。
    /// </summary>
    /// <param name="action">当前 Fluent 操作。</param>
    void ValidateOperation(SqlOperationAction action);

    /// <summary>
    /// 在修改 Clause 前验证并切换操作状态。
    /// </summary>
    /// <param name="action">当前 Fluent 操作。</param>
    void UseOperation(SqlOperationAction action);
}

/// <summary>
/// 会影响统一 Builder 操作状态的 Clause 行为。
/// </summary>
internal enum SqlOperationAction
{
    /// <summary>
    /// 追加 Select 投影。
    /// </summary>
    Select,

    /// <summary>
    /// 追加 From、Join、Where、GroupBy 或 OrderBy 等查询子句。
    /// </summary>
    QueryClause,

    /// <summary>
    /// 设置 Insert 目标表。
    /// </summary>
    InsertInto,

    /// <summary>
    /// 追加 Insert Values 数据行。
    /// </summary>
    Values,

    /// <summary>
    /// 设置 Update 目标表。
    /// </summary>
    Update,

    /// <summary>
    /// 追加 Update From 来源表。
    /// </summary>
    UpdateFrom,

    /// <summary>
    /// 设置 Update 列值。
    /// </summary>
    Set,

    /// <summary>
    /// 设置 Delete 目标表。
    /// </summary>
    DeleteFrom,

    /// <summary>
    /// 追加 Delete Using 来源表。
    /// </summary>
    DeleteUsing,

    /// <summary>
    /// 追加 Update 或 Delete 的筛选条件。
    /// </summary>
    MutationWhere,

    /// <summary>
    /// 配置 Mutation 执行后的返回投影。
    /// </summary>
    Returning,

    /// <summary>
    /// 显式允许无筛选条件的 Update 或 Delete。
    /// </summary>
    AllowAllRows,

    /// <summary>
    /// 配置查询分页参数。
    /// </summary>
    Paging
}