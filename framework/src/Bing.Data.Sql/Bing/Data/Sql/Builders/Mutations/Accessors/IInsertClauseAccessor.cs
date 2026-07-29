namespace Bing.Data.Sql.Builders.Mutations.Accessors;

/// <summary>
/// 提供 Insert 子句访问能力。
/// </summary>
public interface IInsertClauseAccessor
{
    /// <summary>
    /// Insert 目标表子句。
    /// </summary>
    IInsertClause InsertClause { get; }

    /// <summary>
    /// Insert 目标列子句。
    /// </summary>
    IInsertColumnsClause InsertColumnsClause { get; }

    /// <summary>
    /// Insert Values 子句。
    /// </summary>
    IValuesClause ValuesClause { get; }
}