using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// Mutation Clause 的 Fluent API 扩展。
/// </summary>
public static class MutationClauseExtensions
{
    /// <summary>
    /// 设置 Insert 目标实体表。
    /// </summary>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">Insert Builder。</param>
    /// <returns>传入的同一个 Insert Builder。</returns>
    public static ISqlInsertBuilder InsertInto<TEntity>(this ISqlInsertBuilder source) where TEntity : class =>
        InsertInto<ISqlInsertBuilder, TEntity>(source);

    /// <summary>
    /// 设置 Insert 目标实体表。
    /// </summary>
    /// <typeparam name="T">支持 Insert 子句访问的 Builder 类型。</typeparam>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">Insert Builder。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T InsertInto<T, TEntity>(this T source)
        where T : IInsert, IInsertClauseAccessor, ISqlMutationContextAccessor
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.InsertClause.Into(ResolveTable<TEntity>(source));
        return source;
    }

    /// <summary>
    /// 设置 Insert 目标表。
    /// </summary>
    /// <param name="source">Insert Builder。</param>
    /// <param name="table">结构化目标表引用。</param>
    /// <returns>传入的同一个 Insert Builder。</returns>
    public static ISqlInsertBuilder InsertInto(this ISqlInsertBuilder source, SqlTableReference table) =>
        InsertInto<ISqlInsertBuilder>(source, table);

    /// <summary>
    /// 设置 Insert 目标表。
    /// </summary>
    /// <typeparam name="T">支持 Insert 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Insert Builder。</param>
    /// <param name="table">结构化目标表引用。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T InsertInto<T>(this T source, SqlTableReference table)
        where T : IInsert, IInsertClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.InsertClause.Into(table);
        return source;
    }

    /// <summary>
    /// 添加 Insert 目标列。
    /// </summary>
    /// <typeparam name="T">支持 Insert 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Insert Builder。</param>
    /// <param name="columns">目标列名。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T Columns<T>(this T source, params string[] columns)
        where T : IInsert, IInsertClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.InsertColumnsClause.AddRange(columns);
        return source;
    }

    /// <summary>
    /// 添加 Insert 目标列。
    /// </summary>
    /// <param name="source">Insert Builder。</param>
    /// <param name="columns">目标列名。</param>
    /// <returns>传入的同一个 Insert Builder。</returns>
    public static ISqlInsertBuilder Columns(this ISqlInsertBuilder source, params string[] columns) =>
        Columns<ISqlInsertBuilder>(source, columns);

    /// <summary>
    /// 依据实体映射添加可写 Insert 列。
    /// </summary>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">Insert Builder。</param>
    /// <param name="columns">由直接实体属性访问组成的列集合表达式。</param>
    /// <returns>传入的同一个 Insert Builder。</returns>
    public static ISqlInsertBuilder Columns<TEntity>(this ISqlInsertBuilder source,
        Expression<Func<TEntity, object[]>> columns) where TEntity : class =>
        Columns<ISqlInsertBuilder, TEntity>(source, columns);

    /// <summary>
    /// 依据实体映射添加可写 Insert 列。
    /// </summary>
    /// <typeparam name="T">支持 Insert 和 Mutation 上下文访问的 Builder 类型。</typeparam>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">Insert Builder。</param>
    /// <param name="columns">由直接实体属性访问组成的列集合表达式。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T Columns<T, TEntity>(this T source, Expression<Func<TEntity, object[]>> columns)
        where T : IInsert, IInsertClauseAccessor, ISqlMutationContextAccessor
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        foreach (var propertyName in GetPropertyNames(columns))
        {
            var column = ResolveWritableColumn<TEntity>(source, propertyName, predicate: static column =>
                column.CanInsert && column.IsKey == false && column.IsDatabaseGenerated == false, "插入");
            source.InsertColumnsClause.Add(column.ColumnName);
        }
        return source;
    }

    /// <summary>
    /// 添加一行 Insert Values。
    /// </summary>
    /// <typeparam name="T">支持 Insert 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Insert Builder。</param>
    /// <param name="values">本行参数值。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T Values<T>(this T source, params object[] values)
        where T : IInsert, IInsertClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.ValuesClause.AddRow(values);
        return source;
    }

    /// <summary>
    /// 添加一行 Insert Values。
    /// </summary>
    /// <param name="source">Insert Builder。</param>
    /// <param name="values">本行参数值。</param>
    /// <returns>传入的同一个 Insert Builder。</returns>
    public static ISqlInsertBuilder Values(this ISqlInsertBuilder source, params object[] values) =>
        Values<ISqlInsertBuilder>(source, values);

    /// <summary>
    /// 添加多行 Insert Values。
    /// </summary>
    /// <typeparam name="T">支持 Insert 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Insert Builder。</param>
    /// <param name="rows">待插入值行集合。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T Values<T>(this T source, IEnumerable<IReadOnlyList<object>> rows)
        where T : IInsert, IInsertClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.ValuesClause.AddRows(rows);
        return source;
    }

    /// <summary>
    /// 添加多行 Insert Values。
    /// </summary>
    /// <param name="source">Insert Builder。</param>
    /// <param name="rows">待插入值行集合。</param>
    /// <returns>传入的同一个 Insert Builder。</returns>
    public static ISqlInsertBuilder Values(this ISqlInsertBuilder source, IEnumerable<IReadOnlyList<object>> rows) =>
        Values<ISqlInsertBuilder>(source, rows);

    /// <summary>
    /// 设置 Update 目标实体表。
    /// </summary>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">Update Builder。</param>
    /// <returns>传入的同一个 Update Builder。</returns>
    public static ISqlUpdateBuilder Update<TEntity>(this ISqlUpdateBuilder source) where TEntity : class =>
        Update<ISqlUpdateBuilder, TEntity>(source);

    /// <summary>
    /// 设置 Update 目标实体表。
    /// </summary>
    /// <typeparam name="T">支持 Update 子句访问的 Builder 类型。</typeparam>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">Update Builder。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T Update<T, TEntity>(this T source)
        where T : IUpdate, IUpdateClauseAccessor, ISqlMutationContextAccessor
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.UpdateClause.UpdateTable(ResolveTable<TEntity>(source));
        return source;
    }

    /// <summary>
    /// 设置 Update 目标表。
    /// </summary>
    /// <param name="source">Update Builder。</param>
    /// <param name="table">结构化目标表引用。</param>
    /// <returns>传入的同一个 Update Builder。</returns>
    public static ISqlUpdateBuilder Update(this ISqlUpdateBuilder source, SqlTableReference table) =>
        Update<ISqlUpdateBuilder>(source, table);

    /// <summary>
    /// 设置 Update 目标表。
    /// </summary>
    /// <typeparam name="T">支持 Update 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Update Builder。</param>
    /// <param name="table">结构化目标表引用。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T Update<T>(this T source, SqlTableReference table)
        where T : IUpdate, IUpdateClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.UpdateClause.UpdateTable(table);
        return source;
    }

    /// <summary>
    /// 设置 Update 列值。
    /// </summary>
    /// <typeparam name="T">支持 Update 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Update Builder。</param>
    /// <param name="column">逻辑列名。</param>
    /// <param name="value">参数化值。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T Set<T>(this T source, string column, object value)
        where T : IUpdate, IUpdateClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.SetClause.Set(column, value);
        return source;
    }

    /// <summary>
    /// 设置 Update 列值。
    /// </summary>
    /// <param name="source">Update Builder。</param>
    /// <param name="column">逻辑列名。</param>
    /// <param name="value">参数化值。</param>
    /// <returns>传入的同一个 Update Builder。</returns>
    public static ISqlUpdateBuilder Set(this ISqlUpdateBuilder source, string column, object value) =>
        Set<ISqlUpdateBuilder>(source, column, value);

    /// <summary>
    /// 使用实体映射设置可更新列的参数化值。
    /// </summary>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <typeparam name="TValue">属性值类型。</typeparam>
    /// <param name="source">Update Builder。</param>
    /// <param name="column">直接实体属性访问表达式。</param>
    /// <param name="value">参数化更新值。</param>
    /// <returns>传入的同一个 Update Builder。</returns>
    public static ISqlUpdateBuilder Set<TEntity, TValue>(this ISqlUpdateBuilder source,
        Expression<Func<TEntity, TValue>> column, TValue value) where TEntity : class =>
        Set<ISqlUpdateBuilder, TEntity, TValue>(source, column, value);

    /// <summary>
    /// 使用实体映射设置可更新列的参数化值。
    /// </summary>
    /// <typeparam name="T">支持 Update 和 Mutation 上下文访问的 Builder 类型。</typeparam>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <typeparam name="TValue">属性值类型。</typeparam>
    /// <param name="source">Update Builder。</param>
    /// <param name="column">直接实体属性访问表达式。</param>
    /// <param name="value">参数化更新值。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T Set<T, TEntity, TValue>(this T source, Expression<Func<TEntity, TValue>> column, TValue value)
        where T : IUpdate, IUpdateClauseAccessor, ISqlMutationContextAccessor
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        var mapping = ResolveWritableColumn<TEntity>(source, GetPropertyName(column), static item =>
            item.CanUpdate && item.IsKey == false && item.IsDatabaseGenerated == false, "更新");
        var context = source.MutationContext;
        var parameter = context.Services.ParameterFactory.Create(context.ParameterManager.GenerateName(), value, mapping,
            context.ExecutionContext.DatabaseContext, typeof(TEntity), SqlParameterSource.SqlBuilder);
        source.SetClause.Set(mapping.ColumnName, parameter);
        return source;
    }

    /// <summary>
    /// 设置 Delete 目标实体表。
    /// </summary>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">Delete Builder。</param>
    /// <returns>传入的同一个 Delete Builder。</returns>
    public static ISqlDeleteBuilder DeleteFrom<TEntity>(this ISqlDeleteBuilder source) where TEntity : class =>
        DeleteFrom<ISqlDeleteBuilder, TEntity>(source);

    /// <summary>
    /// 设置 Delete 目标实体表。
    /// </summary>
    /// <typeparam name="T">支持 Delete 子句访问的 Builder 类型。</typeparam>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">Delete Builder。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T DeleteFrom<T, TEntity>(this T source)
        where T : IDelete, IDeleteClauseAccessor, ISqlMutationContextAccessor
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.DeleteClause.From(ResolveTable<TEntity>(source));
        return source;
    }

    /// <summary>
    /// 设置 Delete 目标表。
    /// </summary>
    /// <param name="source">Delete Builder。</param>
    /// <param name="table">结构化目标表引用。</param>
    /// <returns>传入的同一个 Delete Builder。</returns>
    public static ISqlDeleteBuilder DeleteFrom(this ISqlDeleteBuilder source, SqlTableReference table) =>
        DeleteFrom<ISqlDeleteBuilder>(source, table);

    /// <summary>
    /// 设置 Delete 目标表。
    /// </summary>
    /// <typeparam name="T">支持 Delete 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Delete Builder。</param>
    /// <param name="table">结构化目标表引用。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T DeleteFrom<T>(this T source, SqlTableReference table)
        where T : IDelete, IDeleteClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.DeleteClause.From(table);
        return source;
    }

    /// <summary>
    /// 使用 And 追加 Mutation 筛选条件。
    /// </summary>
    /// <typeparam name="T">支持 Mutation Where 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Mutation Builder。</param>
    /// <param name="condition">查询条件。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T Where<T>(this T source, ICondition condition)
        where T : IMutationWhereClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.WhereClause.And(condition);
        return source;
    }

    /// <summary>
    /// 使用 And 追加 Update 筛选条件。
    /// </summary>
    /// <param name="source">Update Builder。</param>
    /// <param name="condition">查询条件。</param>
    /// <returns>传入的同一个 Update Builder。</returns>
    public static ISqlUpdateBuilder Where(this ISqlUpdateBuilder source, ICondition condition) =>
        Where<ISqlUpdateBuilder>(source, condition);

    /// <summary>
    /// 使用 And 追加 Delete 筛选条件。
    /// </summary>
    /// <param name="source">Delete Builder。</param>
    /// <param name="condition">查询条件。</param>
    /// <returns>传入的同一个 Delete Builder。</returns>
    public static ISqlDeleteBuilder Where(this ISqlDeleteBuilder source, ICondition condition) =>
        Where<ISqlDeleteBuilder>(source, condition);

    /// <summary>
    /// 使用实体映射追加参数化 Mutation 条件。
    /// </summary>
    /// <typeparam name="TEntity">条件所属实体类型。</typeparam>
    /// <typeparam name="TValue">条件属性值类型。</typeparam>
    /// <param name="source">Mutation Builder。</param>
    /// <param name="column">直接实体属性访问表达式。</param>
    /// <param name="value">条件比较值。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>传入的同一个 Mutation Builder。</returns>
    public static ISqlUpdateBuilder Where<TEntity, TValue>(this ISqlUpdateBuilder source,
        Expression<Func<TEntity, TValue>> column, TValue value, Operator @operator = Operator.Equal) where TEntity : class =>
        Where<ISqlUpdateBuilder, TEntity, TValue>(source, column, value, @operator);

    /// <summary>
    /// 使用实体映射追加参数化 Mutation 条件。
    /// </summary>
    /// <typeparam name="TEntity">条件所属实体类型。</typeparam>
    /// <typeparam name="TValue">条件属性值类型。</typeparam>
    /// <param name="source">Mutation Builder。</param>
    /// <param name="column">直接实体属性访问表达式。</param>
    /// <param name="value">条件比较值。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>传入的同一个 Mutation Builder。</returns>
    public static ISqlDeleteBuilder Where<TEntity, TValue>(this ISqlDeleteBuilder source,
        Expression<Func<TEntity, TValue>> column, TValue value, Operator @operator = Operator.Equal) where TEntity : class =>
        Where<ISqlDeleteBuilder, TEntity, TValue>(source, column, value, @operator);

    /// <summary>
    /// 使用实体映射追加参数化 Mutation 条件。
    /// </summary>
    /// <typeparam name="T">支持 Mutation Where 和上下文访问的 Builder 类型。</typeparam>
    /// <typeparam name="TEntity">条件所属实体类型。</typeparam>
    /// <typeparam name="TValue">条件属性值类型。</typeparam>
    /// <param name="source">Mutation Builder。</param>
    /// <param name="column">直接实体属性访问表达式。</param>
    /// <param name="value">条件比较值。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T Where<T, TEntity, TValue>(this T source, Expression<Func<TEntity, TValue>> column, TValue value,
        Operator @operator = Operator.Equal)
        where T : IMutationWhereClauseAccessor, ISqlMutationContextAccessor
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        var mapping = ResolveMappedColumn<TEntity>(source, GetPropertyName(column));
        var context = source.MutationContext;
        var left = context.Dialect.SafeName(mapping.ColumnName);
        if (value == null)
        {
            source.WhereClause.And(SqlConditionFactory.Create(left, null, @operator));
            return source;
        }
        var parameter = context.Services.ParameterFactory.Create(context.ParameterManager.GenerateName(), value, mapping,
            context.ExecutionContext.DatabaseContext, typeof(TEntity), SqlParameterSource.SqlBuilder);
        AddParameter(context.ParameterManager, parameter);
        source.WhereClause.And(SqlConditionFactory.Create(left, context.Dialect.GetParamName(parameter.Name), @operator));
        return source;
    }

    /// <summary>
    /// 使用 Or 追加 Mutation 筛选条件。
    /// </summary>
    /// <typeparam name="T">支持 Mutation Where 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Mutation Builder。</param>
    /// <param name="condition">查询条件。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T OrWhere<T>(this T source, ICondition condition)
        where T : IMutationWhereClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.WhereClause.Or(condition);
        return source;
    }

    /// <summary>
    /// 使用 Or 追加 Update 筛选条件。
    /// </summary>
    /// <param name="source">Update Builder。</param>
    /// <param name="condition">查询条件。</param>
    /// <returns>传入的同一个 Update Builder。</returns>
    public static ISqlUpdateBuilder OrWhere(this ISqlUpdateBuilder source, ICondition condition) =>
        OrWhere<ISqlUpdateBuilder>(source, condition);

    /// <summary>
    /// 使用 Or 追加 Delete 筛选条件。
    /// </summary>
    /// <param name="source">Delete Builder。</param>
    /// <param name="condition">查询条件。</param>
    /// <returns>传入的同一个 Delete Builder。</returns>
    public static ISqlDeleteBuilder OrWhere(this ISqlDeleteBuilder source, ICondition condition) =>
        OrWhere<ISqlDeleteBuilder>(source, condition);

    /// <summary>
    /// 设置是否允许全表更新。
    /// </summary>
    /// <typeparam name="T">支持该能力的 Mutation Builder 类型。</typeparam>
    /// <param name="source">Mutation Builder。</param>
    /// <param name="allowAllRows">允许时为 true。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T AllowAllRows<T>(this T source, bool allowAllRows = true)
        where T : IAllowAllRowsMutationBuilder
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.SetAllowAllRows(allowAllRows);
        return source;
    }

    /// <summary>
    /// 设置是否允许全表更新。
    /// </summary>
    /// <param name="source">Update Builder。</param>
    /// <param name="allowAllRows">允许时为 true。</param>
    /// <returns>传入的同一个 Update Builder。</returns>
    public static ISqlUpdateBuilder AllowAllRows(this ISqlUpdateBuilder source, bool allowAllRows = true) =>
        AllowAllRows<ISqlUpdateBuilder>(source, allowAllRows);

    /// <summary>
    /// 设置是否允许全表删除。
    /// </summary>
    /// <param name="source">Delete Builder。</param>
    /// <param name="allowAllRows">允许时为 true。</param>
    /// <returns>传入的同一个 Delete Builder。</returns>
    public static ISqlDeleteBuilder AllowAllRows(this ISqlDeleteBuilder source, bool allowAllRows = true) =>
        AllowAllRows<ISqlDeleteBuilder>(source, allowAllRows);

    /// <summary>
    /// 从当前 Mutation 上下文解析实体映射表。
    /// </summary>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">Mutation 上下文访问器。</param>
    /// <returns>实体映射的结构化表引用。</returns>
    private static SqlTableReference ResolveTable<TEntity>(ISqlMutationContextAccessor source) where TEntity : class
    {
        var mapping = source.MutationContext.Services.EntityMappingResolver.Resolve(typeof(TEntity),
            source.MutationContext.ExecutionContext.DatabaseContext);
        if (mapping?.Table == null)
            throw new InvalidOperationException($"未找到实体 {typeof(TEntity).Name} 的数据库表映射。");
        return mapping.Table;
    }

    /// <summary>
    /// 解析实体中指定的可写列。
    /// </summary>
    private static ColumnMappingMetadata ResolveWritableColumn<TEntity>(ISqlMutationContextAccessor source,
        string propertyName, Func<ColumnMappingMetadata, bool> predicate, string operation) where TEntity : class
    {
        var column = ResolveMappedColumn<TEntity>(source, propertyName);
        if (predicate(column) == false)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 的属性 {propertyName} 不能用于{operation}。");
        return column;
    }

    /// <summary>
    /// 解析实体中指定的映射列。
    /// </summary>
    private static ColumnMappingMetadata ResolveMappedColumn<TEntity>(ISqlMutationContextAccessor source,
        string propertyName) where TEntity : class
    {
        var context = source.MutationContext;
        var mapping = context.Services.EntityMappingResolver.Resolve(typeof(TEntity),
            context.ExecutionContext.DatabaseContext);
        var column = mapping?.Columns?.Values.FirstOrDefault(item =>
            string.Equals(item.PropertyName, propertyName, StringComparison.Ordinal));
        if (column == null)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 的属性 {propertyName} 未映射到数据库列。");
        return column;
    }

    /// <summary>
    /// 获取单个直接实体属性访问的属性名。
    /// </summary>
    private static string GetPropertyName<TEntity, TValue>(Expression<Func<TEntity, TValue>> expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var body = UnwrapConvert(expression.Body);
        if (body is not MemberExpression { Expression: ParameterExpression } member)
            throw new ArgumentException("表达式必须是实体的直接属性访问。", nameof(expression));
        return member.Member.Name;
    }

    /// <summary>
    /// 获取多个直接实体属性访问的属性名。
    /// </summary>
    private static IReadOnlyList<string> GetPropertyNames<TEntity>(Expression<Func<TEntity, object[]>> expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (expression.Body is not NewArrayExpression array || array.Expressions.Count == 0)
            throw new ArgumentException("列集合表达式必须包含至少一个实体直接属性访问。", nameof(expression));
        return array.Expressions.Select(item => GetPropertyName(Expression.Lambda<Func<TEntity, object>>(
            Expression.Convert(UnwrapConvert(item), typeof(object)), expression.Parameters))).ToArray();
    }

    /// <summary>
    /// 移除值类型成员访问生成的转换表达式。
    /// </summary>
    private static Expression UnwrapConvert(Expression expression) => expression is UnaryExpression
        {
            NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
        } unary ? unary.Operand : expression;

    /// <summary>
    /// 保存带元数据参数，供 Provider 自定义数据库参数行为使用。
    /// </summary>
    private static void AddParameter(IParameterManager parameterManager, SqlParam parameter)
    {
        if (parameterManager is IAdvancedParameterManager advancedParameterManager)
            advancedParameterManager.Add(parameter);
        else
            parameterManager.Add(parameter.Name, parameter.Value);
    }
}