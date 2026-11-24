using System.Collections.ObjectModel;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 实体 Mutation 操作类型。
/// </summary>
internal enum SqlMutationOperation
{
    /// <summary>
    /// 基于实体映射生成 Insert 命令。
    /// </summary>
    Insert,

    /// <summary>
    /// 基于实体映射生成 Update 命令。
    /// </summary>
    Update,

    /// <summary>
    /// 基于实体映射生成 Delete 命令。
    /// </summary>
    Delete
}

/// <summary>
/// 可跨实体实例复用的 Mutation 映射计划。
/// </summary>
internal sealed class SqlMutationPlan
{
    /// <summary>
    /// 初始化一个 <see cref="SqlMutationPlan"/> 类型的实例。
    /// </summary>
    /// <param name="mapping">实体表和列映射。</param>
    /// <param name="writeColumns">当前操作应写入的列。</param>
    /// <param name="keys">主键列。</param>
    /// <param name="concurrencyColumns">并发令牌列。</param>
    /// <param name="operation">计划对应的写入操作。</param>
    private SqlMutationPlan(EntityMappingMetadata mapping, IReadOnlyList<ColumnMappingMetadata> writeColumns,
        IReadOnlyList<ColumnMappingMetadata> keys, IReadOnlyList<ColumnMappingMetadata> concurrencyColumns,
        SqlMutationOperation operation)
    {
        Mapping = mapping;
        WriteColumns = writeColumns;
        Keys = keys;
        ConcurrencyColumns = concurrencyColumns;
        _canUseGeneratedParameterBinding = HasCompleteGeneratedGetters(mapping, writeColumns, keys, concurrencyColumns);
        _generatedValueReader = HasCompleteGeneratedAccessors(mapping, writeColumns, keys, concurrencyColumns)
            ? mapping.Model.GeneratedValueReader
            : null;
        _generatedGetters = _generatedValueReader == null
            ? CreateGeneratedGetters(mapping, writeColumns, keys, concurrencyColumns)
            : EmptyGeneratedGetters;
        _generatedOperation = operation switch
        {
            SqlMutationOperation.Insert => SqlOperationKind.InsertValues,
            SqlMutationOperation.Update => SqlOperationKind.Update,
            _ => SqlOperationKind.Delete
        };
        _canUseGeneratedSql = _canUseGeneratedParameterBinding && HasDefaultSqlShape(mapping, operation, writeColumns);
        _canUseGeneratedShapeSql = _canUseGeneratedParameterBinding &&
            mapping.Model?.GeneratedMutationShapeSql != null &&
            (_canUseGeneratedSql || writeColumns.Count == 1 &&
                HasDefaultSqlShape(mapping, operation, writeColumns, checkWriteColumns: false));
        if (_canUseGeneratedParameterBinding && operation != SqlMutationOperation.Delete &&
            mapping.Model?.GeneratedMutationColumnsSql != null &&
            HasDefaultSqlShape(mapping, operation, writeColumns, checkWriteColumns: false))
            _generatedWriteProperties = Array.AsReadOnly(writeColumns.Select(column => column.PropertyName).ToArray());
    }

    /// <summary>
    /// 没有生成式值读取器时复用的空索引，避免为每个严格生成计划重复分配字典。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<object, object>> EmptyGeneratedGetters =
        new ReadOnlyDictionary<string, Func<object, object>>(
            new Dictionary<string, Func<object, object>>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// 当前计划可直接使用的生成属性读取器。
    /// </summary>
    private readonly IReadOnlyDictionary<string, Func<object, object>> _generatedGetters;

    /// <summary>
    /// 当前计划可直接使用的生成式按名值读取器。
    /// </summary>
    private readonly Func<object, string, object> _generatedValueReader;

    /// <summary>
    /// 当前计划的全部写入、键和并发列均具有生成 Getter 时，可使用静态参数绑定。
    /// </summary>
    private readonly bool _canUseGeneratedParameterBinding;

    /// <summary>最终映射与编译期 SQL 的有序列形状是否一致；仅在计划创建时检查。</summary>
    private readonly bool _canUseGeneratedSql;

    /// <summary>有限形状仍必须核对完整最终映射；判断只在计划创建时执行。</summary>
    private readonly bool _canUseGeneratedShapeSql;

    /// <summary>筛选后的固定属性布局只随计划保存；各次命令仅提供新的实体值。</summary>
    private readonly IReadOnlyList<string> _generatedWriteProperties;

    /// <summary>生成 SQL 对应的单实体操作。</summary>
    private readonly SqlOperationKind _generatedOperation;

    /// <summary>
    /// 获取实体映射。
    /// </summary>
    public EntityMappingMetadata Mapping { get; }

    /// <summary>
    /// 获取当前操作应写入的列。
    /// </summary>
    public IReadOnlyList<ColumnMappingMetadata> WriteColumns { get; }

    /// <summary>
    /// 获取主键列。
    /// </summary>
    public IReadOnlyList<ColumnMappingMetadata> Keys { get; }

    /// <summary>
    /// 获取并发令牌列。
    /// </summary>
    public IReadOnlyList<ColumnMappingMetadata> ConcurrencyColumns { get; }

    /// <summary>
    /// 获取已登记的生成属性读取器数量。
    /// </summary>
    internal int GeneratedGetterCount => _generatedGetters.Count;

    /// <summary>
    /// 获取当前计划是否可以跳过通用对象参数绑定。
    /// </summary>
    internal bool CanUseGeneratedParameterBinding => _canUseGeneratedParameterBinding;

    /// <summary>整行返回优先复用完整常量，否则由生成片段组成筛选写列的 SQL。</summary>
    internal string GetGeneratedReturningSql(string providerKey) =>
        (_canUseGeneratedSql ? Mapping.Model.GeneratedMutationReturningSql?.Invoke(providerKey, _generatedOperation) : null) ??
        (_generatedWriteProperties != null ? Mapping.Model.GeneratedMutationColumnsSql(providerKey, _generatedOperation,
            _generatedWriteProperties, 1, true) : null);

    /// <summary>优先获取默认或有限形状 SQL，再按筛选写列拼接生成片段；未覆盖的映射或行数返回空。</summary>
    internal string GetGeneratedSql(string providerKey, int rowCount = 1)
    {
        if (rowCount == 1 && _canUseGeneratedSql &&
            Mapping.Model.GeneratedMutationSql?.Invoke(providerKey, _generatedOperation) is { } sql)
            return sql;
        var shapeSql = _canUseGeneratedShapeSql
            ? Mapping.Model.GeneratedMutationShapeSql(providerKey, _generatedOperation,
                _canUseGeneratedSql ? null : WriteColumns[0].PropertyName, rowCount)
            : null;
        return shapeSql ?? (_generatedWriteProperties != null
            ? Mapping.Model.GeneratedMutationColumnsSql(providerKey, _generatedOperation, _generatedWriteProperties, rowCount, false)
            : null);
    }

    /// <summary>获取同一映射形状的数据库身份键插入 SQL；其他操作不使用该合同。</summary>
    internal string GetGeneratedKeyInsertSql(string providerKey) => _canUseGeneratedSql &&
        _generatedOperation == SqlOperationKind.InsertValues ? Mapping.Model.GeneratedKeyInsertSql?.Invoke(providerKey) : null;

    /// <summary>生成文本只适用于已声明的内置方言及默认服务，扩展子句与参数服务仍走原渲染器。</summary>
    internal static bool SupportsGeneratedSql(ISqlProvider provider, SqlBuilderServices services) =>
        (provider.Key, provider.DatabaseType) is ("bing.sqlite", DatabaseType.Sqlite) or
            ("bing.mysql", DatabaseType.MySql) or ("bing.postgresql", DatabaseType.PgSql) or
            ("bing.sqlserver", DatabaseType.SqlServer) or ("bing.oracle", DatabaseType.Oracle) &&
        provider is ISqlProviderProfileProvider { Profile.Mutation.GeneratedSqlDialect: { } generatedDialect } &&
        ReferenceEquals(provider.Dialect, generatedDialect) &&
        provider is not ISqlMutationClauseFactoryProvider &&
        (provider is not ISqlReturningDialect returning || provider.Key == "bing.sqlserver" &&
            returning.Position == SqlReturningClausePosition.BeforeSource &&
            returning.GetKeyword(SqlExecutionKind.Insert) == "Output" &&
             returning.GetQualifier(SqlExecutionKind.Insert, null) == "Inserted" &&
             returning.GetKeyword(SqlExecutionKind.Update) == "Output" &&
             returning.GetQualifier(SqlExecutionKind.Update, null) == "Inserted" &&
             returning.GetKeyword(SqlExecutionKind.Delete) == "Output" &&
             returning.GetQualifier(SqlExecutionKind.Delete, null) == "Deleted") &&
        provider.ParameterManagerFactory is DefaultParameterManagerFactory &&
        services.ObjectNameFormatter is DefaultSqlObjectNameFormatter &&
        services.ParameterFactory.GetType() == typeof(DefaultSqlParameterFactory);

    /// <summary>
    /// 尝试使用当前计划中的生成读取器获取列值。
    /// </summary>
    /// <param name="source">实体实例。</param>
    /// <param name="column">列映射。</param>
    /// <param name="value">读取出的值。</param>
    /// <returns>存在匹配的生成读取器时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// 生成委托接受登记实体类型的实例，包括其派生类型；不兼容的来源类型必须回到兼容读取路径。
    /// </remarks>
    internal bool TryGetGeneratedValue(object source, ColumnMappingMetadata column, out object value)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (column == null)
            throw new ArgumentNullException(nameof(column));
        if (Mapping.EntityType != null && Mapping.EntityType.IsInstanceOfType(source) &&
            _generatedValueReader != null && string.IsNullOrWhiteSpace(column.PropertyName) == false)
        {
            value = _generatedValueReader(source, column.PropertyName);
            return true;
        }
        if (Mapping.EntityType != null && Mapping.EntityType.IsInstanceOfType(source) &&
            _generatedGetters.TryGetValue(column.PropertyName ?? string.Empty, out var getter))
        {
            value = getter(source);
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>按当前映射读取列值；宿主关闭兼容功能后不引用运行时 Getter 查找。</summary>
    /// <param name="source">当前映射类型的实体实例。</param>
    /// <param name="column">需要读取的映射列。</param>
    /// <param name="fallback">仅供兼容模式使用的 Getter 缓存。</param>
    /// <returns>生成读取器或兼容 Getter 返回的当前值。</returns>
    internal object GetValue(object source, ColumnMappingMetadata column, SqlMutationPlanCache fallback)
    {
        if (TryGetGeneratedValue(source, column, out var value))
            return value;
        // 开关必须位于调用方，裁剪器才能移除整个反射 Getter 可达链。
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled)
            throw new NotSupportedException($"类型 {Mapping.EntityType.FullName} 的属性 {column.PropertyName} 没有匹配当前映射的生成读取器。");
        return fallback.GetValue(source, column);
    }

    /// <summary>
    /// 创建指定操作和列筛选配置的计划。
    /// </summary>
    /// <param name="mapping">实体表和列映射。</param>
    /// <param name="operation">需要生成计划的 Mutation 操作。</param>
    /// <param name="includes">显式包含的实体属性集合。</param>
    /// <param name="excludes">显式排除的实体属性集合。</param>
    /// <returns>包含可写列、主键和并发令牌列的可复用 Mutation 计划。</returns>
    public static SqlMutationPlan Create(EntityMappingMetadata mapping, SqlMutationOperation operation,
        IReadOnlyCollection<string> includes, IReadOnlyCollection<string> excludes)
    {
        if (mapping == null)
            throw new ArgumentNullException(nameof(mapping));
        var includeSet = CreatePropertySet(includes);
        var excludeSet = CreatePropertySet(excludes);
        var writeColumns = operation switch
        {
            SqlMutationOperation.Insert => mapping.Columns.Values.Where(column => column.CanInsert),
            SqlMutationOperation.Update => mapping.Columns.Values.Where(column => column.CanUpdate),
            _ => Enumerable.Empty<ColumnMappingMetadata>()
        };
        writeColumns = writeColumns.Where(column =>
            (includeSet == null || includeSet.Contains(column.PropertyName)) &&
            (excludeSet == null || excludeSet.Contains(column.PropertyName) == false));
        return new SqlMutationPlan(mapping, writeColumns.ToArray(),
            mapping.Columns.Values.Where(column => column.IsKey).ToArray(),
            mapping.Columns.Values.Where(column => column.IsConcurrencyToken).ToArray(), operation);
    }

    /// <summary>
    /// 核对最终表名、有序列映射和写列是否仍与生成模型一致，避免静态 SQL 绕过运行时映射配置。
    /// 参数类型、存储转换等仍由最终列元数据及原参数工厂处理，不影响 SQL 文本形状。
    /// </summary>
    private static bool HasDefaultSqlShape(EntityMappingMetadata mapping, SqlMutationOperation operation,
        IReadOnlyList<ColumnMappingMetadata> writeColumns, bool checkWriteColumns = true)
    {
        var model = mapping.Model;
        var table = mapping.Table;
        if (model == null || (model.GeneratedMutationSql == null && model.GeneratedKeyInsertSql == null &&
                             model.GeneratedMutationShapeSql == null && model.GeneratedMutationReturningSql == null &&
                             model.GeneratedMutationColumnsSql == null) ||
            table == null || mapping.EntityType != model.EntityType ||
            !string.Equals(table.TableName, model.TableName, StringComparison.Ordinal) ||
            !string.Equals(table.Schema ?? string.Empty, model.Schema ?? string.Empty, StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(table.Database) || !string.IsNullOrEmpty(table.Alias))
            return false;

        using var columns = mapping.Columns.Values.GetEnumerator();
        var writeIndex = 0;
        foreach (var property in model.Properties.Values)
        {
            if (property.IsIgnored)
                continue;
            if (!columns.MoveNext())
                return false;
            var column = columns.Current;
            if (!string.Equals(property.PropertyName, column.PropertyName, StringComparison.Ordinal) ||
                !string.Equals(property.ColumnName, column.ColumnName, StringComparison.Ordinal) ||
                property.ClrType != column.ClrType || property.CanInsert != column.CanInsert ||
                property.CanUpdate != column.CanUpdate || property.IsKey != column.IsKey ||
                property.IsConcurrencyToken != column.IsConcurrencyToken ||
                property.DatabaseGeneratedOption != column.DatabaseGeneratedOption)
                return false;
            var isWriteColumn = operation == SqlMutationOperation.Insert ? property.CanInsert :
                operation == SqlMutationOperation.Update && property.CanUpdate;
            if (checkWriteColumns && isWriteColumn && (writeIndex >= writeColumns.Count ||
                                  !ReferenceEquals(column, writeColumns[writeIndex++])))
                return false;
        }
        return !columns.MoveNext() && (!checkWriteColumns || writeIndex == writeColumns.Count);
    }

    /// <summary>
    /// 从实体模型元数据构建当前计划涉及列的生成 Getter 索引。
    /// </summary>
    /// <param name="mapping">最终实体映射。</param>
    /// <param name="writeColumns">写入列。</param>
    /// <param name="keys">键列。</param>
    /// <param name="concurrencyColumns">并发列。</param>
    /// <returns>按属性名索引的生成 Getter 集合。</returns>
    private static IReadOnlyDictionary<string, Func<object, object>> CreateGeneratedGetters(
        EntityMappingMetadata mapping, IReadOnlyList<ColumnMappingMetadata> writeColumns,
        IReadOnlyList<ColumnMappingMetadata> keys, IReadOnlyList<ColumnMappingMetadata> concurrencyColumns)
    {
        var modelProperties = mapping.Model?.Properties;
        if (modelProperties == null || modelProperties.Count == 0)
            return new ReadOnlyDictionary<string, Func<object, object>>(
                new Dictionary<string, Func<object, object>>(StringComparer.OrdinalIgnoreCase));

        var columns = writeColumns.Concat(keys).Concat(concurrencyColumns)
            .Where(column => column != null && string.IsNullOrWhiteSpace(column.PropertyName) == false)
            .GroupBy(column => column.PropertyName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
        var getters = new Dictionary<string, Func<object, object>>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            if (modelProperties.TryGetValue(column.PropertyName, out var property) &&
                property?.GeneratedGetter != null)
                getters[column.PropertyName] = property.GeneratedGetter;
        }
        return new ReadOnlyDictionary<string, Func<object, object>>(getters);
    }

    /// <summary>
    /// 判断当前 Mutation 计划涉及的全部列是否都有生成 Getter。
    /// </summary>
    /// <param name="mapping">最终实体映射。</param>
    /// <param name="writeColumns">写入列。</param>
    /// <param name="keys">主键列。</param>
    /// <param name="concurrencyColumns">并发列。</param>
    /// <returns>模型提供生成式值读取器且所有涉及列均有生成 Getter 时返回 <c>true</c>。</returns>
    private static bool HasCompleteGeneratedAccessors(EntityMappingMetadata mapping,
        IReadOnlyList<ColumnMappingMetadata> writeColumns, IReadOnlyList<ColumnMappingMetadata> keys,
        IReadOnlyList<ColumnMappingMetadata> concurrencyColumns)
    {
        var model = mapping?.Model;
        if (model?.GeneratedValueReader == null)
            return false;

        var columns = writeColumns.Concat(keys).Concat(concurrencyColumns)
            .Where(column => column != null && string.IsNullOrWhiteSpace(column.PropertyName) == false)
            .GroupBy(column => column.PropertyName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        return columns.Length != 0 && columns.All(column =>
            model.Properties.TryGetValue(column.PropertyName, out var property) && property?.GeneratedGetter != null &&
            string.Equals(property.PropertyName, column.PropertyName, StringComparison.Ordinal));
    }

    /// <summary>
    /// 判断当前计划涉及的全部列是否都有生成 Getter。
    /// </summary>
    private static bool HasCompleteGeneratedGetters(EntityMappingMetadata mapping,
        IReadOnlyList<ColumnMappingMetadata> writeColumns, IReadOnlyList<ColumnMappingMetadata> keys,
        IReadOnlyList<ColumnMappingMetadata> concurrencyColumns)
    {
        var modelProperties = mapping?.Model?.Properties;
        if (modelProperties == null || modelProperties.Count == 0)
            return false;

        var columns = writeColumns.Concat(keys).Concat(concurrencyColumns)
            .Where(column => column != null && string.IsNullOrWhiteSpace(column.PropertyName) == false)
            .GroupBy(column => column.PropertyName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        return columns.Length != 0 && columns.All(column =>
            modelProperties.TryGetValue(column.PropertyName, out var property) && property?.GeneratedGetter != null &&
            string.Equals(property.PropertyName, column.PropertyName, StringComparison.Ordinal));
    }

    /// <summary>
    /// 创建忽略大小写的属性筛选集合。
    /// </summary>
    /// <param name="properties">调用方提供的属性名称集合。</param>
    /// <returns>去除空白项后的忽略大小写集合；未提供有效属性时返回 <see langword="null"/>。</returns>
    private static HashSet<string> CreatePropertySet(IReadOnlyCollection<string> properties)
    {
        if (properties == null || properties.Count == 0)
            return null;
        return new HashSet<string>(properties.Where(property => string.IsNullOrWhiteSpace(property) == false),
            StringComparer.OrdinalIgnoreCase);
    }
}
