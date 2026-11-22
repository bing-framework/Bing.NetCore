using System.ComponentModel;
using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Extensions;
using Bing.Data.Sql.Builders.Internal;

namespace Bing.Data.Sql;

/// <summary>
/// 复用当前查询契约的生成调用点桥接入口。
/// </summary>
/// <remarks>
/// 生成调用点的受控入口；所有操作仍使用当前查询的来源、映射和生命周期。
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlGeneratedQueryBridge
{
    /// <summary>
    /// 根据生成调用点提供的结果原型应用 DTO 投影。
    /// </summary>
    /// <param name="query">待修改的 Lambda 查询。</param>
    /// <param name="resultType">生成调用点的结果类型。</param>
    /// <param name="prototype">结果原型；仅用于空值校验。</param>
    /// <returns>应用投影后的原查询对象。</returns>
    public static SqlLambdaQuery Prototype(SqlLambdaQuery query, Type resultType, object prototype)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        query.GeneratedCore.ApplyPrototypeProjection(resultType, prototype);
        return query;
    }

    /// <summary>
    /// 根据生成器提供的静态成员名称应用结果 DTO 投影。
    /// </summary>
    /// <param name="query">待修改的 Lambda 查询。</param>
    /// <param name="resultType">生成调用点的结果类型。</param>
    /// <param name="prototype">结果原型；仅用于空值校验。</param>
    /// <param name="projectionMembers">编译期确定的可读取标量成员名称，包含构造函数绑定成员。</param>
    /// <returns>应用投影后的原查询对象。</returns>
    /// <remarks>
    /// 生成路径使用该入口避免执行时反射枚举 DTO 属性；运行时兼容路径继续使用无成员描述的重载。
    /// </remarks>
    public static SqlLambdaQuery Prototype(SqlLambdaQuery query, Type resultType, object prototype,
        string[] projectionMembers)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        query.GeneratedCore.ApplyPrototypeProjection(resultType, prototype, projectionMembers);
        return query;
    }

    /// <summary>
    /// 将生成列描述绑定到查询数据源。
    /// </summary>
    /// <param name="item">生成的列描述。</param>
    /// <param name="sources">Lambda 参数对应的查询数据源。</param>
    /// <returns>包含实际数据源标识的 SQL 投影列。</returns>
    private static SqlProjectionColumn Bind(SqlGeneratedColumn item, IReadOnlyList<TableSource> sources) =>
        new(sources[item.SourceIndex].SourceId, item.MemberName, item.ResultAlias, item.ResultType, item.Function, item.Distinct);

    /// <summary>
    /// 按生成操作修改查询投影、分组、排序或排除列。
    /// </summary>
    /// <param name="query">要修改的 Lambda 查询。</param>
    /// <param name="operation">投影操作名称。</param>
    /// <param name="types">Lambda 参数对应的实体类型。</param>
    /// <param name="names">Lambda 参数名称。</param>
    /// <param name="aliases">可选的数据源别名。</param>
    /// <param name="columns">生成的列描述。</param>
    /// <param name="descending">排序时是否降序。</param>
    /// <returns>应用指定操作后的原查询对象。</returns>
    /// <remarks>
    /// 支持 Select、ReplaceSelect、AppendSelect、GroupBy、OrderBy 和 ExcludeSelect；ExcludeSelect 不支持聚合列。
    /// </remarks>
    public static SqlLambdaQuery Project(SqlLambdaQuery query, string operation, Type[] types, string[] names,
        string[] aliases, SqlGeneratedColumn[] columns, bool descending = false)
    {
        var core = query.GeneratedCore;
        return core.AtomicChange(() =>
        {
            var accessor = (ISqlQueryClauseAccessor)core.GetBuilder();
            var select = (SelectClause)accessor.SelectClause;
            if (operation == "ExcludeSelect" && columns.Any(column => column.Function.HasValue))
                throw new NotSupportedException("ExcludeSelect 不能包含聚合。");
            if (operation == "ExcludeSelect" && !SqlLambdaSourceResolver.Sources(accessor).Any(source => source.EntityType == types[0]))
            {
                foreach (var column in columns)
                    select.Exclude(new SqlProjectionExclusion(types[0], null, aliases?[0], names[0], column.MemberName));
            }
            else
            {
                var sources = SqlLambdaSourceResolver.Resolve(accessor, types, names, aliases);
                var logical = columns.Select(column => Bind(column, sources)).ToArray();
                switch (operation)
                {
                    case "Select": select.ApplyProjection(logical); break;
                    case "ReplaceSelect": select.ApplyProjection(logical, replace: true); break;
                    case "AppendSelect": select.ApplyProjection(logical, legacyAppend: true); break;
                    case "GroupBy": ((GroupByClause)accessor.GroupByClause).AddLogicalColumns(logical); break;
                    case "OrderBy": ((OrderByClause)accessor.OrderByClause).AddLogicalColumns(logical, descending); break;
                    case "ExcludeSelect":
                        foreach (var column in logical)
                        {
                            var source = sources.Single(item => item.SourceId == column.SourceId);
                            if (!select.EntityColumns(source, true, true).Any(item => item.MemberName == column.MemberName))
                                throw new ArgumentException("排除成员没有可读取的映射。");
                            select.Exclude(new SqlProjectionExclusion(source.EntityType, source.SourceId, null, null, column.MemberName));
                        }
                        break;
                    default: throw new NotSupportedException("未知的生成投影操作。");
                }
            }
            core.Touch();
            return query;
        });
    }

    /// <summary>
    /// 将生成的列比较应用到 Where 或 Having 条件。
    /// </summary>
    /// <param name="query">要修改的 Lambda 查询。</param>
    /// <param name="having">是否将条件添加到 Having 子句。</param>
    /// <param name="types">Lambda 参数对应的实体类型。</param>
    /// <param name="names">Lambda 参数名称。</param>
    /// <param name="aliases">可选的数据源别名。</param>
    /// <param name="left">比较左侧的生成列。</param>
    /// <param name="right">可选的比较右侧生成列。</param>
    /// <param name="comparison">比较运算符。</param>
    /// <param name="value">右侧列缺省时使用的比较值。</param>
    /// <returns>应用比较条件后的原查询对象。</returns>
    /// <remarks>
    /// Where 条件不能包含聚合列；Having 条件使用函数谓词，普通条件绑定列及参数。
    /// </remarks>
    public static SqlLambdaQuery Compare(SqlLambdaQuery query, bool having, Type[] types, string[] names,
        string[] aliases, SqlGeneratedColumn left, SqlGeneratedColumn right, Operator comparison, object value)
    {
        var core = query.GeneratedCore;
        return core.AtomicChange(() =>
        {
            var accessor = (ISqlQueryClauseAccessor)core.GetBuilder();
            if (!having && (left.Function.HasValue || right?.Function != null))
                throw new NotSupportedException("Where 不能包含聚合，请使用 Having。");
            var sources = SqlLambdaSourceResolver.Resolve(accessor, types, names, aliases);
            var context = ((SelectClause)accessor.SelectClause).ProjectionContext;
            var leftColumn = Bind(left, sources);
            var rightColumn = right == null ? null : Bind(right, sources);
            leftColumn.ToColumn(context);
            rightColumn?.ToColumn(context);
            if (!having && right == null)
            {
                var source = sources[left.SourceIndex];
                var operand = new SqlBoundOperand(leftColumn, source, null);
                accessor.WhereClause.Where(new Helper(context).CreateCondition(left.MemberName, operand,
                    source.EntityType, value, comparison));
            }
            else
            {
                string parameter = null;
                if (right == null && value != null)
                {
                    parameter = context.ParameterManager.GenerateName();
                    context.ParameterManager.Add(parameter, value);
                }
                if (having)
                    ((GroupByClause)accessor.GroupByClause).SetFunctionHaving(new SqlFunctionPredicate(leftColumn, null, rightColumn, parameter, comparison));
                else
                    accessor.WhereClause.Where(new SqlBoundCondition(context, new SqlBoundOperand(leftColumn, sources[left.SourceIndex], null),
                        new SqlBoundOperand(rightColumn, sources[right.SourceIndex], parameter), comparison));
            }
            core.Touch();
            return query;
        });
    }

    /// <summary>
    /// 按运行时条件选择是否应用生成的列比较。
    /// </summary>
    /// <param name="query">要修改的 Lambda 查询。</param>
    /// <param name="condition">为 true 时追加比较条件。</param>
    /// <param name="having">是否将条件添加到 Having 子句。</param>
    /// <param name="types">Lambda 参数对应的实体类型。</param>
    /// <param name="names">Lambda 参数名称。</param>
    /// <param name="aliases">可选的数据源别名。</param>
    /// <param name="left">比较左侧的生成列。</param>
    /// <param name="right">可选的比较右侧生成列。</param>
    /// <param name="comparison">比较运算符。</param>
    /// <param name="value">右侧列缺省时使用的比较值。</param>
    /// <returns>条件为 false 时保持原查询，条件为 true 时返回追加条件后的查询。</returns>
    /// <remarks>
    /// 该入口只在生成路径中读取列描述；条件为 false 时不解析表达式、不创建参数，也不触碰 Builder 状态。
    /// </remarks>
    public static SqlLambdaQuery OptionalCompare(SqlLambdaQuery query, bool condition, bool having, Type[] types,
        string[] names, string[] aliases, SqlGeneratedColumn left, SqlGeneratedColumn right, Operator comparison,
        object value) => condition
        ? Compare(query, having, types, names, aliases, left, right, comparison, value)
        : query;

    /// <summary>将生成的复合条件作为一个操作绑定到当前查询。</summary>
    /// <param name="query">要修改的 Lambda 查询。</param>
    /// <param name="types">Lambda 参数对应的实体类型。</param>
    /// <param name="names">Lambda 参数名称。</param>
    /// <param name="aliases">可选的数据源别名。</param>
    /// <param name="predicate">不包含请求值的编译期条件结构。</param>
    /// <returns>应用条件后的原查询对象。</returns>
    public static SqlLambdaQuery WherePredicate(SqlLambdaQuery query, Type[] types, string[] names,
        string[] aliases, SqlGeneratedPredicate predicate) =>
        WherePredicate(query, types, names, aliases, predicate, null);

    /// <summary>将生成的复合条件与本次查询显式值一起绑定。</summary>
    /// <param name="query">要修改的 Lambda 查询。</param>
    /// <param name="types">Lambda 参数对应的实体类型。</param>
    /// <param name="names">Lambda 参数名称。</param>
    /// <param name="aliases">可选的数据源别名。</param>
    /// <param name="predicate">不包含请求值的编译期条件结构。</param>
    /// <param name="value">本次查询要绑定的显式值。</param>
    /// <returns>应用条件后的原查询对象。</returns>
    public static SqlLambdaQuery WherePredicate(SqlLambdaQuery query, Type[] types, string[] names,
        string[] aliases, SqlGeneratedPredicate predicate, object value)
    {
        var core = query.GeneratedCore;
        return core.AtomicChange(() =>
        {
            var accessor = (ISqlQueryClauseAccessor)core.GetBuilder();
            var sources = SqlLambdaSourceResolver.Resolve(accessor, types, names, aliases);
            var context = ((SelectClause)accessor.SelectClause).ProjectionContext;
            accessor.WhereClause.Where(BindPredicate(predicate, sources, context, value));
            core.Touch();
            return query;
        });
    }

    private static ICondition BindPredicate(SqlGeneratedPredicate predicate, IReadOnlyList<TableSource> sources,
        SqlClauseContext context, object value)
    {
        if (predicate.LeftColumn == null)
        {
            var left = BindPredicate(predicate.Left, sources, context, value);
            if (predicate.IsNot)
                return new SqlNegatedCondition(left);
            var right = BindPredicate(predicate.Right, sources, context, value);
            return predicate.IsOr ? new OrCondition(left, right) : new AndCondition(left, right);
        }

        var leftDescription = predicate.LeftColumn;
        var rightDescription = predicate.RightColumn;
        if (leftDescription.Function.HasValue || rightDescription?.Function != null)
            throw new NotSupportedException("Where 不能包含聚合，请使用 Having。");
        var leftColumn = Bind(leftDescription, sources);
        var rightColumn = rightDescription == null ? null : Bind(rightDescription, sources);
        leftColumn.ToColumn(context);
        rightColumn?.ToColumn(context);
        var leftOperand = new SqlBoundOperand(leftColumn, sources[leftDescription.SourceIndex], null);
        var parameterValue = predicate.UsesParameter
            ? predicate.ParameterIndex < 0 ? value : ((object[])value)[predicate.ParameterIndex]
            : predicate.Value;
        return rightDescription == null
            ? new Helper(context).CreateCondition(leftDescription.MemberName, leftOperand,
                sources[leftDescription.SourceIndex].EntityType, parameterValue, predicate.Comparison)
            : new SqlBoundCondition(context, leftOperand,
                new SqlBoundOperand(rightColumn, sources[rightDescription.SourceIndex], null), predicate.Comparison);
    }

    /// <summary>
    /// 向查询添加指定类型和连接选项的 Join。
    /// </summary>
    /// <param name="query">要添加 Join 的 Lambda 查询。</param>
    /// <param name="leftJoin">是否添加左连接。</param>
    /// <param name="leftType">左侧实体类型。</param>
    /// <param name="rightType">右侧实体类型。</param>
    /// <param name="leftName">左侧 Lambda 参数名称。</param>
    /// <param name="rightName">右侧 Lambda 参数名称。</param>
    /// <param name="leftMember">左侧关联属性名称。</param>
    /// <param name="rightMember">右侧关联属性名称。</param>
    /// <param name="alias">右侧数据源别名。</param>
    /// <param name="schema">右侧数据源架构名称。</param>
    /// <returns>添加 Join 后的原查询对象。</returns>
    public static SqlLambdaQuery Join(SqlLambdaQuery query, bool leftJoin, Type leftType, Type rightType,
        string leftName, string rightName, string leftMember, string rightMember, string alias, string schema) =>
        Join(query, leftJoin, leftType, rightType, leftName, rightName, leftMember, rightMember,
            new SqlJoinOptions { RightAlias = alias, Schema = schema });

    /// <summary>
    /// 向查询添加指定类型和连接选项的 Join。
    /// </summary>
    /// <param name="query">要添加 Join 的 Lambda 查询。</param>
    /// <param name="leftJoin">是否添加左连接。</param>
    /// <param name="leftType">左侧实体类型。</param>
    /// <param name="rightType">右侧实体类型。</param>
    /// <param name="leftName">左侧 Lambda 参数名称。</param>
    /// <param name="rightName">右侧 Lambda 参数名称。</param>
    /// <param name="leftMember">左侧关联属性名称。</param>
    /// <param name="rightMember">右侧关联属性名称。</param>
    /// <param name="options">右侧数据源别名和架构等 Join 选项。</param>
    /// <returns>添加 Join 后的原查询对象。</returns>
    /// <remarks>
    /// 根据两个实体的指定属性创建等值 Join 条件。
    /// </remarks>
    public static SqlLambdaQuery Join(SqlLambdaQuery query, bool leftJoin, Type leftType, Type rightType,
        string leftName, string rightName, string leftMember, string rightMember, SqlJoinOptions options)
    {
        var core = query.GeneratedCore;
        return core.AtomicChange(() =>
        {
            var accessor = (ISqlQueryClauseAccessor)core.GetBuilder();
            var context = ((SelectClause)accessor.SelectClause).ProjectionContext;
            var left = SqlLambdaSourceResolver.Resolve(accessor, new[] { leftType }, new[] { leftName }, new[] { options.LeftAlias })[0];
            var reference = context.EntityResolver.GetTableReference(rightType) with
                { Alias = SqlLambdaSourceResolver.JoinAlias(accessor, options.RightAlias, rightName), EntityType = rightType };
            if (!string.IsNullOrWhiteSpace(options.Schema)) reference = reference with { Schema = options.Schema };
            ((JoinClause)accessor.JoinClause).JoinGenerated(reference, leftJoin);
            var right = ((ISqlMultiSourceJoinClause)accessor.JoinClause).TypedSources.Last();
            var l = new SqlProjectionColumn(left.SourceId, leftMember, null, typeof(object));
            var r = new SqlProjectionColumn(right.SourceId, rightMember, null, typeof(object));
            l.ToColumn(context); r.ToColumn(context);
            ((JoinClause)accessor.JoinClause).SetBoundOn(new SqlBoundCondition(context,
                new SqlBoundOperand(l, left, null), new SqlBoundOperand(r, right, null), Operator.Equal));
            core.Touch();
            return query;
        });
    }

    /// <summary>
    /// 读取比较表达式右侧的常量或闭包字段值。
    /// </summary>
    /// <param name="expression">表达式主体为二元比较的 Lambda 表达式。</param>
    /// <returns>比较表达式右侧读取到的值。</returns>
    /// <remarks>
    /// 表达式主体不是二元表达式或右侧不是支持的常量、闭包字段或转换时会抛出异常。
    /// </remarks>
    public static object ComparisonValue(LambdaExpression expression)
    {
        if (expression?.Body is not BinaryExpression binary) throw new NotSupportedException("生成条件要求直接比较。");
        return SqlFunctionPredicate.ReadValue(binary.Right);
    }
}
