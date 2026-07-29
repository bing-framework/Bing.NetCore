using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Accessors;
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
}