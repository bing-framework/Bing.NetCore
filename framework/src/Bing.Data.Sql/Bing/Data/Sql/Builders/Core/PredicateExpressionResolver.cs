using System.Linq.Expressions;
using Bing.Data;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Internal;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 谓词表达式解析器
/// </summary>
public class PredicateExpressionResolver
{
    /// <summary>
    /// 辅助操作
    /// </summary>
    private readonly Helper _helper;

    /// <summary>
    /// 使用子句运行上下文初始化谓词表达式解析器。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    public PredicateExpressionResolver(SqlClauseContext context) : this(context, null) { }

    /// <summary>
    /// 使用子句运行上下文和已创建的辅助操作初始化谓词表达式解析器。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="helper">可复用的辅助操作。</param>
    internal PredicateExpressionResolver(SqlClauseContext context, Helper helper) =>
        _helper = helper ?? new Helper(context);

    /// <summary>
    /// 解析谓词表达式
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">谓词表达式</param>
    /// <returns>解析得到的查询条件；表达式为空时返回空条件实例。</returns>
    public ICondition Resolve<TEntity>(Expression<Func<TEntity, bool>> expression)
    {
        if (expression == null)
            return NullCondition.Instance;
        return ResolveExpression(expression, typeof(TEntity));
    }

    /// <summary>
    /// 解析谓词表达式
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="type">实体类型</param>
    /// <returns>表达式对应的查询条件。</returns>
    private ICondition ResolveExpression(Expression expression, Type type)
    {
        switch (expression.NodeType)
        {
            case ExpressionType.Lambda:
                return ResolveExpression(((LambdaExpression)expression).Body, type);

            case ExpressionType.OrElse:
                return ResolveOrExpression((BinaryExpression)expression, type);

            case ExpressionType.AndAlso:
                return ResolveAndExpression((BinaryExpression)expression, type);

            default:
                return _helper.CreateCondition(expression, type);
        }
    }

    /// <summary>
    /// 解析Or表达式
    /// </summary>
    /// <param name="expression">二元表达式</param>
    /// <param name="type">实体类型</param>
    /// <returns>由左右表达式使用 <c>Or</c> 连接形成的查询条件。</returns>
    private ICondition ResolveOrExpression(BinaryExpression expression, Type type)
    {
        var left = ResolveExpression(expression.Left, type);
        var right = ResolveExpression(expression.Right, type);
        return new OrCondition(left, right);
    }

    /// <summary>
    /// 解析And表达式
    /// </summary>
    /// <param name="expression">二元表达式</param>
    /// <param name="type">实体类型</param>
    /// <returns>由左右表达式使用 <c>And</c> 连接形成的查询条件。</returns>
    private ICondition ResolveAndExpression(BinaryExpression expression, Type type)
    {
        var left = ResolveExpression(expression.Left, type);
        var right = ResolveExpression(expression.Right, type);
        return new AndCondition(left, right);
    }
}