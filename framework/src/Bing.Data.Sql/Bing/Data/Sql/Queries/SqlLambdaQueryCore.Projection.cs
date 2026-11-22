using System.Linq.Expressions;
using System.Reflection;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

internal sealed partial class SqlLambdaQueryCore
{
    /// <summary>
    /// 获取查询使用的投影子句。
    /// </summary>
    private SelectClause ProjectionClause => ((ISqlQueryClauseAccessor)GetBuilder()).SelectClause as SelectClause ??
        throw new NotSupportedException("当前 Provider 不支持结构化 Lambda 投影。");

    /// <summary>
    /// 解析 Lambda 投影并应用投影列。
    /// </summary>
    /// <param name="expression">描述投影内容的 Lambda 表达式。</param>
    /// <param name="sources">表达式参数对应的查询数据源。</param>
    /// <param name="replace">是否替换已有投影列。</param>
    /// <param name="legacyAppend">兼容旧投影时是否追加列。</param>
    /// <param name="functions">是否允许函数投影。</param>
    /// <param name="propertyAsAlias">是否将属性名作为默认结果别名。</param>
    internal void ApplyProjection(LambdaExpression expression, IReadOnlyList<TableSource> sources,
        bool replace = false, bool legacyAppend = false, bool functions = false, bool propertyAsAlias = false)
    {
        var columns = SqlFunctionProjectionParser.Parse(expression, sources, functions);
        if (propertyAsAlias)
            columns = columns.Select(column => column with { ResultAlias = column.ResultAlias ?? column.MemberName }).ToList();
        ApplyProjectionColumns(columns, replace, legacyAppend, functions);
    }

    /// <summary>
    /// 将投影列应用到当前查询构建器。
    /// </summary>
    /// <param name="columns">要应用的投影列。</param>
    /// <param name="replace">是否替换已有投影列。</param>
    /// <param name="legacyAppend">兼容旧投影时是否追加列。</param>
    /// <param name="functions">投影列是否包含函数。</param>
    /// <remarks>
    /// 外部 Provider 不支持函数投影或缺少投影上下文时会抛出 NotSupportedException。
    /// </remarks>
    private void ApplyProjectionColumns(IReadOnlyList<SqlProjectionColumn> columns, bool replace, bool legacyAppend, bool functions)
    {
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        if (accessor.SelectClause is SelectClause clause)
            clause.ApplyProjection(columns, replace, legacyAppend);
        else
        {
            if (functions || columns.Any(column => column.Function.HasValue))
                throw new NotSupportedException("当前外部 Provider 未提供聚合投影能力。");
            var context = (GetBuilder() as SqlBuilderBase)?.QueryClauseContext ??
                throw new NotSupportedException("当前外部 Provider 缺少投影映射上下文。");
            var rendered = columns.Select(column => column.ToSql(context)).ToArray();
            var existing = replace || _query.LegacyProjectionColumns == null
                ? new HashSet<string>() : new HashSet<string>(_query.LegacyProjectionColumns);
            var incoming = rendered.Where(column => existing.Add(column)).ToArray();
            var clear = replace || _query.LegacyProjectionColumns == null && !legacyAppend;
            if (clear)
            {
                var distinct = accessor.SelectClause.IsDistinct;
                accessor.SelectClause.Clear();
                if (distinct) accessor.SelectClause.Distinct();
            }
            if (incoming.Length != 0) GetSelectClause(accessor).AppendBoundColumns(string.Join(",", incoming));
            _query.LegacyProjectionColumns = existing;
        }
        Touch();
    }

    /// <summary>
    /// 为指定数据源应用实体属性投影。
    /// </summary>
    /// <param name="source">要投影的实体数据源。</param>
    /// <param name="propertyAsAlias">是否将属性名作为结果别名。</param>
    /// <param name="replace">是否替换已有投影列。</param>
    internal void ApplyEntityProjection(TableSource source, bool propertyAsAlias, bool replace = false)
    {
        var clause = ((ISqlQueryClauseAccessor)GetBuilder()).SelectClause as SelectClause;
        if (clause == null)
        {
            var context = (GetBuilder() as SqlBuilderBase)?.QueryClauseContext ??
                throw new NotSupportedException("当前外部 Provider 缺少投影映射上下文。");
            clause = new SelectClause(context);
        }
        ApplyProjectionColumns(clause.EntityColumns(source, propertyAsAlias), replace, false, false);
    }

    /// <summary>
    /// 根据终结方法提供的结果原型生成单来源 DTO 投影。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="prototype">仅用于确定结果类型和可写成员；不会读取实例属性值。</param>
    /// <remarks>
    /// 该路径与显式 Lambda 投影共用 <see cref="SqlProjectionColumn" />、映射校验和执行计划。
    /// 多来源查询必须使用显式 Lambda，避免按成员名猜测来源。
    /// </remarks>
    internal void ApplyPrototypeProjection<TResult>(TResult prototype) where TResult : class
    {
        if (prototype == null)
            throw new ArgumentNullException(nameof(prototype));

        ApplyPrototypeProjection(typeof(TResult), prototype);
    }

    /// <summary>
    /// 按结果类型生成终结 DTO 投影；供生成式调用桥接使用。
    /// </summary>
    /// <param name="resultType">结果 DTO 类型。</param>
    /// <param name="prototype">可选的结果原型；仅用于空值校验。</param>
    internal void ApplyPrototypeProjection(Type resultType, object prototype)
        => ApplyPrototypeProjection(resultType, prototype, null);

    /// <summary>
    /// 按结果类型和可选的编译期成员描述生成终结 DTO 投影。
    /// </summary>
    /// <param name="resultType">结果 DTO 类型。</param>
    /// <param name="prototype">可选的结果原型；仅用于空值校验。</param>
    /// <param name="projectionMembers">生成器提供的可读取标量成员名称；为 null 时仅在允许反射回退的兼容模式下解析。</param>
    internal void ApplyPrototypeProjection(Type resultType, object prototype, IReadOnlyList<string> projectionMembers)
    {
        if (resultType == null)
            throw new ArgumentNullException(nameof(resultType));
        if (prototype == null)
            throw new ArgumentNullException(nameof(prototype));
        if (projectionMembers != null && projectionMembers.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("生成投影成员名称不能为空。", nameof(projectionMembers));

        AtomicChange(() =>
        {
            var accessor = (ISqlQueryClauseAccessor)GetBuilder();
            if (accessor.SelectClause is not SelectClause select)
                throw new NotSupportedException("当前 Provider 不支持终结 DTO 投影。");

            // 已有显式 Select 时保留调用方的列定义，只执行统一的结果校验。
            if (select.ProjectionMode == SqlProjectionMode.Explicit && select.ProjectionCount.GetValueOrDefault() > 0)
            {
                ValidateProjectionResult(resultType);
                return 0;
            }

            var sources = SqlLambdaSourceResolver.Sources(accessor);
            if (sources.Count != 1)
                throw new InvalidOperationException("终结 DTO 投影只支持单个实体来源，请使用 Select Lambda 明确绑定多来源列。");
            var source = sources[0];
            if (source.EntityType == null)
                throw new NotSupportedException("终结 DTO 投影需要可解析的实体来源。");

            if (source.EntityType == resultType && source.ProjectedMembers == null)
            {
                ApplyEntityProjection(source, propertyAsAlias: true, replace: true);
                return 0;
            }

            IReadOnlyList<PropertyInfo> reflectedProperties = null;
            if (projectionMembers == null)
            {
                if (!SqlRuntimeFeatures.ReflectionFallbackEnabled ||
                    select.ProjectionContext.Services.MetadataOptions.ExecutionMode == SqlExecutionMode.GeneratedOnly)
                    throw new NotSupportedException($"结果类型 {resultType.FullName} 的 DTO 投影缺少生成成员信息，请使用受支持的生成式 To/ToList 入口。");
                reflectedProperties = GetWritableScalarProperties(resultType);
            }
            var members = projectionMembers ?? reflectedProperties.Select(property => property.Name).ToArray();
            var reflectedTypes = reflectedProperties?.ToDictionary(property => property.Name,
                property => property.PropertyType, StringComparer.OrdinalIgnoreCase);

            if (source.ProjectedMembers != null)
            {
                var projected = members
                    .Where(member => source.ProjectedMembers.Contains(member, StringComparer.OrdinalIgnoreCase))
                    .Select(member => new SqlProjectionColumn(source.SourceId, member, member, typeof(object)))
                    .ToList();
                if (projected.Count == 0)
                    throw new InvalidOperationException($"派生来源没有可映射到 {resultType.Name} 的 DTO 成员。");
                select.ApplyProjection(projected, replace: true);
                Touch();
                return 0;
            }

            var mapping = ((EntityResolver)select.ProjectionContext.EntityResolver).GetMapping(source.EntityType);
            var columns = new List<SqlProjectionColumn>();
            foreach (var memberName in members)
            {
                if (!mapping.Columns.TryGetValue(memberName, out var column))
                    continue;
                if (mapping.Model.Properties.TryGetValue(memberName, out var metadata) &&
                    (metadata.IsIgnored || !metadata.CanRead))
                    continue;
                columns.Add(new SqlProjectionColumn(source.SourceId, memberName, memberName,
                    reflectedTypes == null ? column.ClrType : reflectedTypes[memberName]));
            }
            if (columns.Count == 0)
                throw new InvalidOperationException($"结果类型 {resultType.Name} 没有与实体 {source.EntityType.Name} 匹配的可读列。");
            select.ApplyProjection(columns, replace: true);
            Touch();
            return 0;
        });
    }

    /// <summary>
    /// 获取结果类型中可由 SQL 直接读取的公开可写标量属性。
    /// </summary>
    /// <param name="resultType">结果类型。</param>
    /// <returns>按声明顺序排列的可写标量属性。</returns>
    private static IReadOnlyList<PropertyInfo> GetWritableScalarProperties(Type resultType) =>
        resultType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0 && property.SetMethod?.IsPublic == true &&
                               SqlFunctionProjectionParser.IsScalar(property.PropertyType))
            .ToArray();

    /// <summary>
    /// 将当前查询投影模式标记为显式投影。
    /// </summary>
    internal void MarkExplicitProjection()
    {
        if (((ISqlQueryClauseAccessor)GetBuilder()).SelectClause is SelectClause clause) clause.MarkExplicit();
        else _query.LegacyProjectionColumns = new HashSet<string>();
    }

    /// <summary>
    /// 为当前查询选择所有数据源。
    /// </summary>
    internal void SelectAllCore()
    {
        ProjectionClause.SelectAllSources();
        Touch();
    }

    /// <summary>
    /// 从 SelectAll 投影中排除指定列。
    /// </summary>
    /// <param name="expression">包含要排除属性的实体属性表达式。</param>
    /// <param name="alias">实体数据源的别名。</param>
    /// <remarks>
    /// 表达式必须包含一个或多个直接实体属性；调用前必须启用 SelectAll 投影模式。
    /// </remarks>
    internal void ExcludeCore(LambdaExpression expression, string alias)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        if (ProjectionClause.ProjectionMode != SqlProjectionMode.AutoAll)
            throw new InvalidOperationException("ExcludeSelect 需要先调用 SelectAll。");
        var parameter = expression.Parameters[0];
        var available = SqlLambdaSourceResolver.Sources((ISqlQueryClauseAccessor)GetBuilder());
        TableSource source = null;
        if (available.Any(item => item.EntityType == parameter.Type))
            source = SqlLambdaSourceResolver.Resolve((ISqlQueryClauseAccessor)GetBuilder(), expression, new[] { alias })[0];
        var body = SqlFunctionProjectionParser.Unbox(expression.Body);
        IEnumerable<Expression> items = body is NewExpression create ? create.Arguments :
            body is NewArrayExpression array ? array.Expressions : new[] { body };
        var members = items.Select(item => SqlFunctionProjectionParser.Unbox(item)).ToList();
        if (members.Count == 0) throw new ArgumentException("排除表达式不能为空。", nameof(expression));
        foreach (var item in members)
        {
            if (item is not MemberExpression member || member.Expression != parameter ||
                member.Member is not System.Reflection.PropertyInfo)
                throw new NotSupportedException("排除表达式只支持实体参数的直接属性。");
            if (source != null && !ProjectionClause.EntityColumns(source, true, true).Any(column => column.MemberName == member.Member.Name))
                throw new ArgumentException($"成员 {member.Member.Name} 不属于可读取列。");
            ProjectionClause.Exclude(new SqlProjectionExclusion(parameter.Type, source?.SourceId, alias,
                parameter.Name, member.Member.Name));
        }
        Touch();
    }

    /// <summary>
    /// 从 SelectAll 投影中排除指定列。
    /// </summary>
    /// <param name="column">要排除的列名，可包含一个数据源别名。</param>
    /// <remarks>
    /// 列名最多包含列名和别名两部分，且各部分必须是有效标识符。
    /// </remarks>
    internal void ExcludeCore(string column)
    {
        if (string.IsNullOrWhiteSpace(column)) throw new ArgumentException("排除列不能为空。", nameof(column));
        var parts = column.Split('.');
        if (parts.Length > 2) throw new ArgumentException("排除列只能是属性名或 alias.属性名。", nameof(column));
        foreach (var part in parts)
            if (SqlLambdaSourceResolver.AliasHint(part) == null)
                throw new ArgumentException("排除列必须使用单个标识符。", nameof(column));
        ProjectionClause.Exclude(new SqlProjectionExclusion(null, null, null, null, null, column));
        Touch();
    }

    /// <summary>
    /// 验证查询投影是否可映射到结果类型。
    /// </summary>
    /// <param name="mappedTypes">可选的映射类型数组。</param>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    internal void ValidateProjectionResult<TResult>(Type[] mappedTypes = null) =>
        (((ISqlQueryClauseAccessor)_query.ReadBuilder).SelectClause as SelectClause)?.ValidateResult(
            typeof(TResult), mappedTypes, _query.SplitOnValue);

    /// <summary>
    /// 验证运行时已确定的结果类型投影。
    /// </summary>
    /// <param name="resultType">结果类型。</param>
    internal void ValidateProjectionResult(Type resultType) =>
        (((ISqlQueryClauseAccessor)_query.ReadBuilder).SelectClause as SelectClause)?.ValidateResult(
            resultType, null, _query.SplitOnValue);
}
