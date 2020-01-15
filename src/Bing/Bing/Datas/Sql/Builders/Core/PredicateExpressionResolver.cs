using System;
using System.Linq.Expressions;
using Bing.Datas.Sql.Builders.Conditions;
using Bing.Datas.Sql.Builders.Internal;

namespace Bing.Datas.Sql.Builders.Core
{
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
        /// 初始化一个<see cref="PredicateExpressionResolver"/>类型的实例
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="resolver">实体解析器</param>
        /// <param name="register">实体别名注册器</param>
        /// <param name="parameterManager">参数管理器</param>
        public PredicateExpressionResolver(IDialect dialect, IEntityResolver resolver, IEntityAliasRegister register,
            IParameterManager parameterManager) =>
            _helper = new Helper(dialect, resolver, register, parameterManager);

        /// <summary>
        /// 解析谓词表达式
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">谓词表达式</param>
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
        private ICondition ResolveExpression(Expression expression, Type type)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Lambda:
                    return ResolveExpression(((LambdaExpression) expression).Body, type);
                case ExpressionType.OrElse:
                    return ResolveOrExpression((BinaryExpression) expression, type);
                case ExpressionType.AndAlso:
                    return ResolveAndExpression((BinaryExpression) expression, type);
                default:
                    return _helper.CreateCondition(expression, type);
            }
        }

        /// <summary>
        /// 解析Or表达式
        /// </summary>
        /// <param name="expression">二元表达式</param>
        /// <param name="type">实体类型</param>
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
        private ICondition ResolveAndExpression(BinaryExpression expression, Type type)
        {
            var left = ResolveExpression(expression.Left, type);
            var right = ResolveExpression(expression.Right, type);
            return new AndCondition(left, right);
        }
    }
}
