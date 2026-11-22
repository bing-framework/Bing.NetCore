using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Conditions;

namespace Bing.Data.Sql.Builders.Clauses;

public partial class GroupByClause
{
    /// <summary>
    /// 保存分组函数 HAVING 谓词。
    /// </summary>
    private SqlFunctionPredicate _functionHaving;

    /// <summary>
    /// 将非聚合投影列加入 GroupBy 逻辑列。
    /// </summary>
    /// <param name="columns">要加入的投影列。</param>
    /// <remarks>
    /// 聚合列会抛出 NotSupportedException；相同数据源和成员的列不会重复加入。
    /// </remarks>
    internal void AddLogicalColumns(IEnumerable<SqlProjectionColumn> columns)
    {
        var items = columns.ToList();
        foreach (var item in items)
        {
            if (item.Function.HasValue) throw new NotSupportedException("GroupBy 不能包含聚合。");
            item.ToColumn(_context);
        }
        _context.UseOperation(SqlOperationAction.QueryClause);
        foreach (var item in items)
            if (!LogicalColumns.Any(column => column.SourceId == item.SourceId && column.MemberName == item.MemberName))
                LogicalColumns.Add(item with { ResultAlias = null });
    }

    /// <summary>
    /// 设置聚合函数的 Having 条件。
    /// </summary>
    /// <param name="expression">描述函数条件的 Lambda 表达式。</param>
    /// <param name="sources">表达式参数对应的查询数据源。</param>
    internal void SetFunctionHaving(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        SetFunctionHaving(SqlFunctionPredicate.Parse(expression, sources, _context));
    }

    /// <summary>
    /// 设置聚合函数的 Having 条件。
    /// </summary>
    /// <param name="condition">解析后的函数谓词。</param>
    /// <remarks>
    /// Having/OrderBy 条件不能引用聚合结果别名；设置后清除其他 Having 条件表示，并根据谓词列更新聚合状态。
    /// </remarks>
    internal void SetFunctionHaving(SqlFunctionPredicate condition)
    {
        // 生成调用与表达式解析共用校验，不能让结果别名进入条件 SQL。
        foreach (var column in condition.Columns)
        {
            if (column.ResultAlias != null) throw new ArgumentException("Having/OrderBy 中不能设置聚合结果别名。");
            column.ToColumn(_context);
        }
        _functionHaving = condition;
        _having = null;
        _boundHaving = null;
        HasFunctionAggregate = condition.Columns.Any(column => column.Function.HasValue);
        _context.UseOperation(SqlOperationAction.QueryClause);
    }

    /// <summary>
    /// 获取聚合筛选涉及的列集合。
    /// </summary>
    internal IEnumerable<SqlProjectionColumn> HavingColumns => _functionHaving?.Columns ?? SqlConditionSnapshot.Columns(_boundHaving);
}
