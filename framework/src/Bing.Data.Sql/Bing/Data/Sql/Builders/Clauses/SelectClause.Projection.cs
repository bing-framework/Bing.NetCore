using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// 标识 Select 投影采用的组合模式。
/// </summary>
/// <remarks>
/// 区分根实体默认投影、自动补全投影和显式指定的投影。
/// </remarks>
internal enum SqlProjectionMode
{
    /// <summary>
    /// 使用根实体的默认列投影。
    /// </summary>
    DefaultRoot,
    /// <summary>
    /// 自动补全根实体的全部映射列。
    /// </summary>
    AutoAll,
    /// <summary>
    /// 使用调用方显式指定的投影列。
    /// </summary>
    Explicit
}

/// <summary>
/// 待排除投影列的来源与成员描述。
/// </summary>
/// <param name="EntityType">投影来源的实体类型。</param>
/// <param name="SourceId">查询来源的稳定标识。</param>
/// <param name="Alias">查询来源的别名。</param>
/// <param name="ParameterName">用于绑定查询来源的 Lambda 参数名称。</param>
/// <param name="MemberName">CLR 成员名称。</param>
/// <param name="ColumnText">延迟解析的列文本。</param>
internal sealed record SqlProjectionExclusion(Type EntityType, string SourceId, string Alias,
    string ParameterName, string MemberName, string ColumnText = null);

public partial class SelectClause
{
    /// <summary>
    /// 获取查询采用的投影模式。
    /// </summary>
    internal SqlProjectionMode ProjectionMode { get; private set; }
    /// <summary>
    /// 保存选择子句的投影列集合。
    /// </summary>
    private List<SqlProjectionColumn> _projection = new();
    /// <summary>
    /// 保存选择子句的投影排除规则。
    /// </summary>
    private List<SqlProjectionExclusion> _exclusions = new();

    /// <summary>
    /// 将查询投影切换为选择所有数据源。
    /// </summary>
    internal void SelectAllSources()
    {
        if (ProjectionMode == SqlProjectionMode.AutoAll) return;
        _context.UseOperation(SqlOperationAction.Select);
        _columns.Clear();
        _projection.Clear();
        _exclusions.Clear();
        ProjectionMode = SqlProjectionMode.AutoAll;
    }

    /// <summary>
    /// 应用显式投影列并更新投影模式。
    /// </summary>
    /// <param name="columns">要应用的投影列。</param>
    /// <param name="replace">是否替换已有投影。</param>
    /// <param name="legacyAppend">是否按旧行为追加投影列。</param>
    /// <remarks>
    /// 投影列为空、列无法解析或结果别名冲突时会抛出异常；相同列身份不会重复加入。
    /// </remarks>
    internal void ApplyProjection(IEnumerable<SqlProjectionColumn> columns, bool replace = false,
        bool legacyAppend = false)
    {
        if (legacyAppend && ProjectionMode == SqlProjectionMode.AutoAll)
            throw new InvalidOperationException("自动投影不能使用 AppendSelect；请用 Select 建立显式投影。");
        var incoming = columns.ToList();
        if (incoming.Count == 0) throw new ArgumentException("投影至少需要一个成员。", nameof(columns));
        foreach (var column in incoming) column.ToColumn(_context);
        _context.UseOperation(SqlOperationAction.Select);
        if (replace || ProjectionMode != SqlProjectionMode.Explicit && !legacyAppend)
        {
            _columns.Clear();
            _projection.Clear();
        }
        _exclusions.Clear();
        ProjectionMode = SqlProjectionMode.Explicit;
        foreach (var column in incoming)
        {
            if (_projection.Any(existing => existing.Identity == column.Identity)) continue;
            if (column.ResultAlias != null &&
                _projection.Any(existing => string.Equals(existing.ResultAlias ?? existing.MemberName,
                    column.ResultAlias, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"聚合投影结果名 {column.ResultAlias} 冲突。");
            _projection.Add(column);
        }
    }

    /// <summary>
    /// 将投影列中推断的实体别名更新为指定别名。
    /// </summary>
    /// <param name="entityType">要更新别名的实体类型。</param>
    /// <param name="previousAlias">原推断别名。</param>
    /// <param name="alias">新的实体别名。</param>
    internal void RebindInferredAlias(Type entityType, string previousAlias, string alias)
    {
        for (var index = 0; index < _columns.Count; index++)
        {
            var column = _columns[index];
            if (column.TableType == entityType && string.Equals(column.TableAlias, previousAlias, StringComparison.OrdinalIgnoreCase))
                _columns[index] = column.WithTableAlias(alias);
        }
    }

    /// <summary>
    /// 将投影模式标记为显式选择。
    /// </summary>
    internal void MarkExplicit() => ProjectionMode = SqlProjectionMode.Explicit;

    /// <summary>
    /// 根据实体映射生成可读属性的投影列。
    /// </summary>
    /// <param name="source">要生成投影列的实体数据源。</param>
    /// <param name="propertyAsAlias">是否将属性名用作结果别名。</param>
    /// <param name="automatic">是否按自动投影规则排序并跳过不支持的属性。</param>
    /// <returns>实体映射中可读取的投影列。</returns>
    /// <remarks>
    /// 自动投影按键属性优先、属性名排序，并跳过非标量属性；显式投影遇到不支持自动读取的存储转换时会抛出异常。
    /// </remarks>
    internal IReadOnlyList<SqlProjectionColumn> EntityColumns(TableSource source, bool propertyAsAlias,
        bool automatic = false)
    {
        if (source.EntityType == null || source.ProjectedMembers != null)
            throw new NotSupportedException("自动实体投影仅支持普通实体表源，请显式选择派生表或原始表源的列。");
        var mapping = ((EntityResolver)_resolver).GetMapping(source.EntityType);
        IEnumerable<ColumnMappingMetadata> columns = mapping.Columns.Values;
        if (automatic)
            columns = columns.OrderByDescending(column => column.IsKey)
                .ThenBy(column => column.PropertyName, StringComparer.Ordinal);
        var result = new List<SqlProjectionColumn>();
        foreach (var column in columns)
        {
            if (mapping.Model.Properties.TryGetValue(column.PropertyName, out var property) &&
                !property.CanRead) continue;
            if (!SqlFunctionProjectionParser.IsScalar(column.ClrType))
            {
                if (column.StorageKind != ColumnStorageKind.Default)
                    throw new NotSupportedException($"成员 {column.PropertyName} 的存储转换没有自动读取契约，请显式投影。");
                if (automatic) continue;
            }
            result.Add(new SqlProjectionColumn(source.SourceId, column.PropertyName,
                propertyAsAlias && column.ColumnName != column.PropertyName ? column.PropertyName : null, column.ClrType));
        }
        return result;
    }

    /// <summary>
    /// 在自动全字段投影模式下记录要排除的列。
    /// </summary>
    /// <param name="exclusion">要排除的投影列规则。</param>
    /// <remarks>
    /// 当前投影模式不是 AutoAll 时会抛出异常；相同排除规则不会重复记录。
    /// </remarks>
    internal void Exclude(SqlProjectionExclusion exclusion)
    {
        if (ProjectionMode != SqlProjectionMode.AutoAll)
            throw new InvalidOperationException("ExcludeSelect 需要先调用 SelectAll。");
        if (!_exclusions.Contains(exclusion)) _exclusions.Add(exclusion);
    }

    /// <summary>
    /// 获取投影解析使用的上下文。
    /// </summary>
    internal SqlClauseContext ProjectionContext => _context;

    /// <summary>
    /// 获取是否通过旧投影 API 显式指定列。
    /// </summary>
    internal bool HasLegacyExplicitColumns => ProjectionMode == SqlProjectionMode.Explicit && _columns.Count != 0;

    /// <summary>
    /// 生成应用排除规则后的投影列布局。
    /// </summary>
    /// <returns>当前投影列布局。</returns>
    /// <remarks>
    /// 自动投影不支持分组、原始 Join 数据源或 Union；排除项必须唯一匹配到可读列，且最终至少保留一列。
    /// </remarks>
    internal IReadOnlyList<SqlProjectionColumn> ProjectionLayout()
    {
        if (ProjectionMode != SqlProjectionMode.AutoAll) return _projection;
        var accessor = (ISqlQueryClauseAccessor)_context.Builder;
        if (accessor.GroupByClause.IsGroup)
            throw new NotSupportedException("全来源自动投影不支持分组，请使用显式 Select。");
        var sources = SqlLambdaSourceResolver.Sources(accessor);
        if (accessor.JoinClause is JoinClause joins && joins.HasNonEntitySources)
            throw new NotSupportedException("全来源自动投影不支持原始 Join 来源。");
        if (_context.Builder is SqlBuilderBase { IsUnion: true })
            throw new NotSupportedException("全来源自动投影不支持 Union，请使用显式 Select。");
        var all = sources.SelectMany(source => EntityColumns(source, true, true)).ToList();
        var bySource = sources.ToDictionary(source => source.SourceId);
        var byMember = all.ToDictionary(column => (column.SourceId, column.MemberName));
        var exact = new Dictionary<string, HashSet<SqlProjectionColumn>>(StringComparer.Ordinal);
        var insensitive = new Dictionary<string, HashSet<SqlProjectionColumn>>(StringComparer.OrdinalIgnoreCase);
        void Index(Dictionary<string, HashSet<SqlProjectionColumn>> target, string key, SqlProjectionColumn column)
        {
            if (!target.TryGetValue(key, out var values)) target[key] = values = new HashSet<SqlProjectionColumn>();
            values.Add(column);
        }
        foreach (var column in all)
        {
            var alias = bySource[column.SourceId].Alias;
            foreach (var name in new[] { column.MemberName, column.ToColumn(_context).Name })
            {
                Index(exact, name, column); Index(insensitive, name, column);
                if (!string.IsNullOrWhiteSpace(alias))
                { Index(exact, alias + "." + name, column); Index(insensitive, alias + "." + name, column); }
            }
        }
        var removed = new HashSet<SqlProjectionColumn>();
        foreach (var exclusion in _exclusions)
        {
            SqlProjectionColumn match;
            if (exclusion.ColumnText != null)
            {
                if (!exact.TryGetValue(exclusion.ColumnText, out var candidates))
                    insensitive.TryGetValue(exclusion.ColumnText, out candidates);
                if (candidates == null || candidates.Count != 1)
                    throw new InvalidOperationException($"排除列 {exclusion.ColumnText} 不存在或匹配到多个来源。");
                match = candidates.Single();
            }
            else
            {
                var source = exclusion.SourceId == null
                    ? SqlLambdaSourceResolver.Find(sources, exclusion.EntityType, exclusion.Alias, exclusion.ParameterName)
                    : bySource[exclusion.SourceId];
                if (!byMember.TryGetValue((source.SourceId, exclusion.MemberName), out match))
                    throw new InvalidOperationException($"排除列 {exclusion.MemberName} 不存在或不可读取。");
            }
            removed.Add(match);
        }
        var result = all.Where(column => !removed.Contains(column)).ToList();
        if (result.Count == 0) throw new InvalidOperationException("自动投影的全部列已被排除。");
        return result;
    }

    /// <summary>
    /// 验证投影列与分组及排序结构是否兼容。
    /// </summary>
    /// <remarks>
    /// 分组查询中未参与分组的非聚合投影列会导致异常。
    /// </remarks>
    internal void ValidateStructure()
    {
        var layout = ProjectionLayout();
        foreach (var column in layout) column.ToColumn(_context);
        var group = ((ISqlQueryClauseAccessor)_context.Builder).GroupByClause as GroupByClause;
        var order = ((ISqlQueryClauseAccessor)_context.Builder).OrderByClause as OrderByClause;
        var otherColumns = (group?.HavingColumns ?? Enumerable.Empty<SqlProjectionColumn>())
            .Concat(order?.LogicalColumns ?? Enumerable.Empty<SqlProjectionColumn>());
        if (group?.LogicalColumns.Count > 0 || layout.Concat(otherColumns).Any(column => column.Function.HasValue))
        {
            foreach (var column in layout.Concat(otherColumns).Where(column => !column.Function.HasValue))
                if (group == null || !group.LogicalColumns.Any(key => key.SourceId == column.SourceId && key.MemberName == column.MemberName))
                    throw new InvalidOperationException($"非聚合列 {column.MemberName} 必须出现在 GroupBy 中。");
        }
    }

    /// <summary>
    /// 验证投影列与查询结果映射方式是否兼容。
    /// </summary>
    /// <param name="resultType">查询结果类型。</param>
    /// <param name="mappedTypes">多对象映射使用的实体类型数组。</param>
    /// <param name="splitOn">多对象映射的分段列名称。</param>
    /// <remarks>
    /// 校验自动投影和聚合投影约束、多对象映射的数据源顺序及分段键，并检查聚合结果能否映射到结果类型。
    /// </remarks>
    internal void ValidateResult(Type resultType, Type[] mappedTypes, string splitOn)
    {
        ValidateStructure();
        var layout = ProjectionLayout();
        if (ProjectionMode != SqlProjectionMode.AutoAll && !layout.Any(column => column.Function.HasValue)) return;
        if (mappedTypes != null)
        {
            if (layout.Any(column => column.Function.HasValue))
                throw new NotSupportedException("聚合投影请使用单结果 DTO，不支持导航多映射。");
            var sources = SqlLambdaSourceResolver.Sources((ISqlQueryClauseAccessor)_context.Builder);
            if (sources.Count != mappedTypes.Length || sources.Where((source, index) => source.EntityType != mappedTypes[index]).Any())
                throw new InvalidOperationException("多映射输入类型必须与自动投影的来源顺序一致。");
            var splits = splitOn.Split(',').Select(value => value.Trim()).ToArray();
            for (var i = 0; i < sources.Count; i++)
            {
                var segment = layout.Where(column => column.SourceId == sources[i].SourceId).ToList();
                if (segment.Count == 0) throw new InvalidOperationException("必需的多映射分段被全部排除。");
                if (i == 0) continue;
                var boundary = splits.Length == 1 ? splits[0] : splits.Length == sources.Count - 1 ? splits[i - 1] : null;
                var first = segment[0];
                var item = first.ToColumn(_context);
                var mapping = ((EntityResolver)_resolver).GetMapping(sources[i].EntityType).Columns[first.MemberName];
                if (!string.Equals(item.ColumnAlias ?? item.Name, boundary, StringComparison.OrdinalIgnoreCase) ||
                    !mapping.IsKey || mapping.IsNullable)
                    throw new InvalidOperationException("自动多映射分段必须以未排除的非空主键开始，并与 SplitOn 一致。");
            }
            return;
        }
        var names = layout.Select(column => column.ToColumn(_context)).Select(item => item.ColumnAlias ?? item.Name).ToList();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
            throw new InvalidOperationException("单对象投影存在重复结果列，请使用显式 DTO 投影或多类型映射。");
        if (SqlFunctionProjectionParser.IsScalar(resultType))
        {
            ValidateScalarResult(resultType);
            return;
        }
        foreach (var column in layout.Where(column => column.Function.HasValue))
        {
            var metadata = _context.Services.EntityModelMetadataProvider.GetMetadata(resultType)
                ?? throw new NotSupportedException($"类型 {resultType.FullName} 未注册模型元数据。请注册结果类型元数据；生成模式可使用 SqlModel/SqlResult 上下文的 CreateModels 注册。");
            // 仅约束实际选中的聚合目标；普通导航仍由显式 map 装配。
            if (!metadata.Properties.TryGetValue(column.ResultAlias ?? string.Empty, out var descriptor) ||
                descriptor.IsIgnored || !descriptor.CanWrite ||
                (descriptor.ClrType != typeof(object) && !SqlFunctionProjectionParser.IsScalar(descriptor.ClrType)) ||
                !descriptor.ClrType.IsAssignableFrom(column.ResultType))
                throw new InvalidOperationException($"聚合列 {column.ResultAlias ?? "(未命名)"} 不能安全映射到 {resultType.Name}，结果属性必须公开可写、未被忽略，使用受支持的标量或 object，且类型及可空性兼容。");
        }
    }

    /// <summary>
    /// 验证聚合标量结果的可空性和类型兼容性。
    /// </summary>
    /// <param name="resultType">接收聚合结果的目标类型。</param>
    /// <remarks>
    /// Sum、Avg、Min 和 Max 的结果可能为 null，目标类型必须可空；目标类型还必须能接收聚合表达式结果。
    /// </remarks>
    internal void ValidateScalarResult(Type resultType)
    {
        if (ProjectionMode != SqlProjectionMode.Explicit || _projection.Count == 0)
            return;

        var column = _projection[0];
        if (!column.Function.HasValue)
            return;

        var aggregateCanReturnNull = column.Function is SqlAggregateFunction.Sum or SqlAggregateFunction.Avg or
            SqlAggregateFunction.Min or SqlAggregateFunction.Max;
        var resultCanReturnNull = !resultType.IsValueType || Nullable.GetUnderlyingType(resultType) != null;
        if (aggregateCanReturnNull && !resultCanReturnNull)
            throw new InvalidOperationException("此聚合结果可能为 NULL，标量结果类型必须可空。");

        var expectedType = Nullable.GetUnderlyingType(column.ResultType) ?? column.ResultType;
        var actualType = Nullable.GetUnderlyingType(resultType) ?? resultType;
        if (!actualType.IsAssignableFrom(expectedType))
            throw new InvalidOperationException(
                $"聚合标量结果类型 {resultType.Name} 与表达式结果类型 {column.ResultType.Name} 不兼容。");
    }

    /// <summary>
    /// 渲染当前投影布局及原始列为 SQL。
    /// </summary>
    /// <returns>投影 SQL；没有任何列时为 *。</returns>
    private string RenderProjection()
    {
        ValidateStructure();
        var logical = ProjectionLayout();
        var sql = string.Join(",", logical.Select(column => column.ToSql(_context)));
        if (_columns.Count != 0)
            sql = _columns.ToSql(_dialect, _register) + (sql.Length == 0 ? "" : "," + sql);
        return sql.Length == 0 ? "*" : sql;
    }

    /// <summary>
    /// 从另一个 Select 子句复制投影模式、列和排除规则。
    /// </summary>
    /// <param name="source">提供投影状态的 Select 子句。</param>
    private void CopyProjectionFrom(SelectClause source)
    {
        ProjectionMode = source.ProjectionMode;
        _projection = new List<SqlProjectionColumn>(source._projection);
        _exclusions = new List<SqlProjectionExclusion>(source._exclusions);
    }
}
