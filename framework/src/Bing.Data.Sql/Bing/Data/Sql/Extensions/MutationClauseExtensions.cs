using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
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
    /// 设置统一 Builder 的 Insert 目标实体表。
    /// </summary>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">统一 SQL Builder。</param>
    /// <returns>传入的同一个统一 SQL Builder。</returns>
    public static ISqlBuilder InsertInto<TEntity>(this ISqlBuilder source) where TEntity : class =>
        InsertInto<ISqlBuilder, TEntity>(source);

    /// <summary>
    /// 设置统一 Builder 的 Insert 目标实体表和目标列。
    /// </summary>
    /// <typeparam name="TEntity">目标实体类型。</typeparam>
    /// <param name="source">统一 SQL Builder。</param>
    /// <param name="columns">单个属性或匿名对象属性投影。</param>
    /// <returns>传入的同一个统一 SQL Builder。</returns>
    public static ISqlBuilder InsertInto<TEntity>(this ISqlBuilder source,
        Expression<Func<TEntity, object>> columns) where TEntity : class
    {
        InsertInto<ISqlBuilder, TEntity>(source);
        foreach (var propertyName in GetPropertyNames(columns))
        {
            var column = ResolveWritableColumn<TEntity>(source, propertyName, static item =>
                item.CanInsert && item.IsKey == false && item.IsDatabaseGenerated == false, "插入");
            source.InsertColumnsClause.Add(column.ColumnName);
        }
        return source;
    }

    /// <summary>
    /// 设置统一 Builder 的 Insert 目标表。
    /// </summary>
    /// <param name="source">统一 SQL Builder。</param>
    /// <param name="table">可由当前 Provider 解析的目标表文本。</param>
    /// <returns>传入的同一个统一 SQL Builder。</returns>
    public static ISqlBuilder InsertInto(this ISqlBuilder source, string table)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        var parsed = source.MutationContext.Provider.TableReferenceParser.Parse(table);
        source.InsertClause.Into(new SqlTableReference
        {
            TableName = parsed.TableName,
            Schema = parsed.Schema,
            Alias = parsed.Alias
        });
        return source;
    }

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
    /// 设置 Update From 的结构化来源表。
    /// </summary>
    /// <typeparam name="T">支持 Update From 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Update Builder。</param>
    /// <param name="table">必须指定别名的结构化来源表引用。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T UpdateFrom<T>(this T source, SqlTableReference table)
        where T : IUpdateFromClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.UpdateFromClause.From(table);
        return source;
    }

    /// <summary>
    /// 将目标列设置为 Update From 来源表的列。
    /// </summary>
    /// <typeparam name="T">支持 Update 和 Update From 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Update Builder。</param>
    /// <param name="targetColumn">单段目标列标识符。</param>
    /// <param name="sourceColumn">单段来源列标识符。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T SetFrom<T>(this T source, string targetColumn, string sourceColumn)
        where T : IUpdateClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        var sourceTable = source.UpdateFromClause.Table ??
            throw new InvalidOperationException("调用 SetFrom 前必须先配置 UpdateFrom 来源表。");
        if (source.SetClause is not IColumnSetClause setClause)
            throw new NotSupportedException("当前 Mutation Set Clause 不支持来源列赋值。");
        setClause.SetFrom(ValidateSingleIdentifier(targetColumn, nameof(targetColumn)), sourceTable.Alias,
            ValidateSingleIdentifier(sourceColumn, nameof(sourceColumn)));
        return source;
    }

    /// <summary>
    /// 使用目标表列和 Update From 来源表列追加结构化筛选条件。
    /// </summary>
    /// <typeparam name="T">支持 Update From 和 Mutation 上下文访问的 Builder 类型。</typeparam>
    /// <param name="source">Update Builder。</param>
    /// <param name="targetColumn">单段目标列标识符。</param>
    /// <param name="sourceColumn">单段来源列标识符。</param>
    /// <param name="operator">目标列与来源列间的比较运算符。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T WhereFrom<T>(this T source, string targetColumn, string sourceColumn,
        Operator @operator = Operator.Equal)
        where T : IUpdateClauseAccessor, ISqlMutationContextAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        var targetTable = source.UpdateClause.Table ??
            throw new InvalidOperationException("调用 WhereFrom 前必须先配置 Update 目标表。");
        var sourceTable = source.UpdateFromClause.Table ??
            throw new InvalidOperationException("调用 WhereFrom 前必须先配置 UpdateFrom 来源表。");
        if (string.IsNullOrWhiteSpace(targetTable.Alias))
            throw new InvalidOperationException("WhereFrom 要求 Update 目标表指定别名。");
        if (string.IsNullOrWhiteSpace(sourceTable.Alias))
            throw new InvalidOperationException("WhereFrom 要求 UpdateFrom 来源表指定别名。");
        var dialect = source.MutationContext.Dialect;
        var left = $"{dialect.SafeName(targetTable.Alias)}.{dialect.SafeName(ValidateSingleIdentifier(targetColumn, nameof(targetColumn)))}";
        var right = $"{dialect.SafeName(sourceTable.Alias)}.{dialect.SafeName(ValidateSingleIdentifier(sourceColumn, nameof(sourceColumn)))}";
        source.WhereClause.And(SqlConditionFactory.Create(left, right, @operator));
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
    /// 设置 Delete Using 的结构化来源表。
    /// </summary>
    /// <typeparam name="T">支持 Delete Using 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Delete Builder。</param>
    /// <param name="table">必须指定别名的结构化来源表引用。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T DeleteUsing<T>(this T source, SqlTableReference table)
        where T : IDeleteUsingClauseAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.DeleteUsingClause.Using(table);
        return source;
    }

    /// <summary>
    /// 使用 Delete 目标表列和 Using 来源表列追加结构化筛选条件。
    /// </summary>
    /// <typeparam name="T">支持 Delete Using 和 Mutation 上下文访问的 Builder 类型。</typeparam>
    /// <param name="source">Delete Builder。</param>
    /// <param name="targetColumn">单段目标列标识符。</param>
    /// <param name="sourceColumn">单段来源列标识符。</param>
    /// <param name="operator">目标列与来源列间的比较运算符。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static T WhereUsing<T>(this T source, string targetColumn, string sourceColumn,
        Operator @operator = Operator.Equal)
        where T : IDeleteClauseAccessor, IDeleteUsingClauseAccessor, ISqlMutationContextAccessor
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        var targetTable = source.DeleteClause.Table ??
            throw new InvalidOperationException("调用 WhereUsing 前必须先配置 Delete 目标表。");
        var sourceTable = source.DeleteUsingClause.Table ??
            throw new InvalidOperationException("调用 WhereUsing 前必须先配置 DeleteUsing 来源表。");
        if (string.IsNullOrWhiteSpace(targetTable.Alias))
            throw new InvalidOperationException("WhereUsing 要求 Delete 目标表指定别名。");
        if (string.IsNullOrWhiteSpace(sourceTable.Alias))
            throw new InvalidOperationException("WhereUsing 要求 DeleteUsing 来源表指定别名。");
        var dialect = source.MutationContext.Dialect;
        var left = $"{dialect.SafeName(targetTable.Alias)}.{dialect.SafeName(ValidateSingleIdentifier(targetColumn, nameof(targetColumn)))}";
        var right = $"{dialect.SafeName(sourceTable.Alias)}.{dialect.SafeName(ValidateSingleIdentifier(sourceColumn, nameof(sourceColumn)))}";
        source.WhereClause.And(SqlConditionFactory.Create(left, right, @operator));
        return source;
    }

    /// <summary>
    /// 使用 And 追加 Mutation 筛选条件。
    /// </summary>
    /// <typeparam name="T">支持 Mutation Where 子句访问的 Builder 类型。</typeparam>
    /// <param name="source">Mutation Builder。</param>
    /// <param name="condition">查询条件。</param>
    /// <returns>传入的同一个 Builder。</returns>
    private static T WhereMutation<T>(T source, ICondition condition)
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
        WhereMutation(source, condition);

    /// <summary>
    /// 使用 And 追加 Delete 筛选条件。
    /// </summary>
    /// <param name="source">Delete Builder。</param>
    /// <param name="condition">查询条件。</param>
    /// <returns>传入的同一个 Delete Builder。</returns>
    public static ISqlDeleteBuilder Where(this ISqlDeleteBuilder source, ICondition condition) =>
        WhereMutation(source, condition);

    /// <summary>
    /// 根据统一 Builder 当前操作状态追加查询或 Mutation 条件。
    /// </summary>
    /// <param name="source">统一 SQL Builder。</param>
    /// <param name="condition">要追加的筛选条件。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static ISqlBuilder Where(this ISqlBuilder source, ICondition condition)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source.OperationKind is SqlOperationKind.Update or SqlOperationKind.Delete)
            ((IMutationWhereClauseAccessor)source).WhereClause.And(condition);
        else
            ((ISqlQueryClauseAccessor)source).WhereClause.And(condition);
        return source;
    }

    /// <summary>
    /// 根据统一 Builder 当前操作状态追加参数化查询或 Mutation 条件。
    /// </summary>
    /// <param name="source">统一 SQL Builder。</param>
    /// <param name="column">参与比较的列名。</param>
    /// <param name="value">参数化比较值。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static ISqlBuilder Where(this ISqlBuilder source, string column, object value,
        Operator @operator = Operator.Equal)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source.OperationKind is not (SqlOperationKind.Update or SqlOperationKind.Delete))
        {
            ((ISqlQueryClauseAccessor)source).WhereClause.Where(column, value, @operator);
            return source;
        }
        var context = source.MutationContext;
        var left = context.Dialect.SafeName(column);
        if (value == null)
        {
            ((IMutationWhereClauseAccessor)source).WhereClause.And(
                SqlConditionFactory.Create(left, null, @operator));
            return source;
        }
        var parameterName = context.ParameterManager.GenerateName();
        context.ParameterManager.Add(parameterName, value);
        ((IMutationWhereClauseAccessor)source).WhereClause.And(
            SqlConditionFactory.Create(left, context.Dialect.GetParamName(parameterName), @operator));
        return source;
    }

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
    /// 使用实体映射为统一 Builder 追加参数化 Mutation 条件。
    /// </summary>
    /// <typeparam name="TEntity">条件所属实体类型。</typeparam>
    /// <typeparam name="TValue">条件属性值类型。</typeparam>
    /// <param name="source">统一 SQL Builder。</param>
    /// <param name="column">直接实体属性访问表达式。</param>
    /// <param name="value">条件比较值。</param>
    /// <param name="operator">比较运算符。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static ISqlBuilder Where<TEntity, TValue>(this ISqlBuilder source,
        Expression<Func<TEntity, TValue>> column, TValue value, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        ((ISqlOperationStateManager)source).UseOperation(SqlOperationAction.MutationWhere);
        return Where<ISqlBuilder, TEntity, TValue>(source, column, value, @operator);
    }

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
    private static T OrWhereMutation<T>(T source, ICondition condition)
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
        OrWhereMutation(source, condition);

    /// <summary>
    /// 使用 Or 追加 Delete 筛选条件。
    /// </summary>
    /// <param name="source">Delete Builder。</param>
    /// <param name="condition">查询条件。</param>
    /// <returns>传入的同一个 Delete Builder。</returns>
    public static ISqlDeleteBuilder OrWhere(this ISqlDeleteBuilder source, ICondition condition) =>
        OrWhereMutation(source, condition);

    /// <summary>
    /// 根据统一 Builder 当前操作状态追加查询或 Mutation Or 条件。
    /// </summary>
    /// <param name="source">统一 SQL Builder。</param>
    /// <param name="condition">要以 Or 方式追加的筛选条件。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static ISqlBuilder OrWhere(this ISqlBuilder source, ICondition condition)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source.OperationKind is SqlOperationKind.Update or SqlOperationKind.Delete)
            ((IMutationWhereClauseAccessor)source).WhereClause.Or(condition);
        else
            ((ISqlQueryClauseAccessor)source).WhereClause.Or(condition);
        return source;
    }

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
    /// 为统一 Mutation Builder 添加结构化返回列。
    /// </summary>
    /// <param name="source">统一 SQL Builder。</param>
    /// <param name="columns">单段物理列名集合。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static ISqlBuilder Returning(this ISqlBuilder source, params string[] columns)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (columns == null || columns.Length == 0)
            throw new ArgumentException("Returning 必须包含至少一个返回列。", nameof(columns));
        var accessor = source as IReturningClauseAccessor ??
            throw new NotSupportedException("当前 SQL Builder 不支持 Returning 子句。");
        var qualifier = GetReturningQualifier(source);
        var result = columns.Select(column => new SqlReturningColumn(
            ValidateSingleIdentifier(column, nameof(columns)), qualifier)).ToArray();
        accessor.ReturningClause.AddRange(result);
        return source;
    }

    /// <summary>
    /// 根据实体映射为统一 Mutation Builder 添加结构化返回投影。
    /// </summary>
    /// <typeparam name="TEntity">返回实体类型。</typeparam>
    /// <param name="source">统一 SQL Builder。</param>
    /// <param name="columns">单个属性或匿名对象属性投影。</param>
    /// <returns>传入的同一个 Builder。</returns>
    public static ISqlBuilder Returning<TEntity>(this ISqlBuilder source,
        Expression<Func<TEntity, object>> columns) where TEntity : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        var accessor = source as IReturningClauseAccessor ??
            throw new NotSupportedException("当前 SQL Builder 不支持 Returning 子句。");
        var qualifier = GetReturningQualifier(source);
        var result = GetPropertyNames(columns).Select(propertyName =>
        {
            var mapping = ResolveMappedColumn<TEntity>(source, propertyName);
            return new SqlReturningColumn(mapping.ColumnName, qualifier, mapping.PropertyName);
        }).ToArray();
        accessor.ReturningClause.AddRange(result);
        return source;
    }

    /// <summary>
    /// 获取 Returning 投影的默认表别名。
    /// </summary>
    /// <param name="source">统一 SQL Builder。</param>
    /// <returns>Update 或 Delete 目标表别名；Insert 及无目标表时返回 <see langword="null"/>。</returns>
    private static string GetReturningQualifier(ISqlBuilder source) => source.OperationKind switch
    {
        SqlOperationKind.Update => source.UpdateClause.Table?.Alias,
        SqlOperationKind.Delete => source.DeleteClause.Table?.Alias,
        _ => null
    };

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
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="source">Mutation 上下文访问器。</param>
    /// <param name="propertyName">实体属性名称。</param>
    /// <param name="predicate">判断映射列能否参与当前操作的谓词。</param>
    /// <param name="operation">用于异常消息的操作名称。</param>
    /// <returns>通过可写性验证的列映射。</returns>
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
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="source">Mutation 上下文访问器。</param>
    /// <param name="propertyName">实体属性名称。</param>
    /// <returns>与实体属性精确匹配的列映射。</returns>
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
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <typeparam name="TValue">属性值类型。</typeparam>
    /// <param name="expression">必须直接访问实体属性的表达式。</param>
    /// <returns>表达式引用的实体属性名称。</returns>
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
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="expression">包含至少一个直接属性访问的数组表达式。</param>
    /// <returns>数组表达式中属性名称的有序集合。</returns>
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
    /// 获取匿名对象或单个直接属性投影中的属性名。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="expression">单个属性或匿名对象属性投影表达式。</param>
    /// <returns>投影中的实体属性名称集合。</returns>
    private static IReadOnlyList<string> GetPropertyNames<TEntity>(Expression<Func<TEntity, object>> expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var body = UnwrapConvert(expression.Body);
        if (body is NewExpression creation && creation.Arguments.Count > 0)
        {
            return creation.Arguments.Select(item => GetPropertyName(Expression.Lambda<Func<TEntity, object>>(
                Expression.Convert(UnwrapConvert(item), typeof(object)), expression.Parameters))).ToArray();
        }
        return new[] { GetPropertyName(expression) };
    }

    /// <summary>
    /// 移除值类型成员访问生成的转换表达式。
    /// </summary>
    /// <param name="expression">可能包含装箱或显式转换的表达式。</param>
    /// <returns>移除最外层转换后的表达式。</returns>
    private static Expression UnwrapConvert(Expression expression) => expression is UnaryExpression
        {
            NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
        } unary ? unary.Operand : expression;

    /// <summary>
    /// 保存带元数据参数，供 Provider 自定义数据库参数行为使用。
    /// </summary>
    /// <param name="parameterManager">当前 Mutation 参数管理器。</param>
    /// <param name="parameter">待保存的带元数据 SQL 参数。</param>
    private static void AddParameter(IParameterManager parameterManager, SqlParam parameter)
    {
        if (parameterManager is IAdvancedParameterManager advancedParameterManager)
            advancedParameterManager.Add(parameter);
        else
            parameterManager.Add(parameter.Name, parameter.Value);
    }

    /// <summary>
    /// 验证列名是未带数据库或架构前缀的单段标识符。
    /// </summary>
    /// <param name="value">待验证的列标识符。</param>
    /// <param name="parameterName">用于异常的参数名称。</param>
    /// <returns>通过验证的单段列名。</returns>
    private static string ValidateSingleIdentifier(string value, string parameterName)
    {
        if (SqlIdentifierPathParser.TryParse(value, out var path) == false || path.Prefix != null ||
            path.DatabaseName != null)
            throw new ArgumentException("列名必须是单段结构化标识符。", parameterName);
        return path.Name;
    }
}