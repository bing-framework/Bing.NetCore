using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 独立、可重复执行的 Mutation 描述。
/// </summary>
/// <remarks>
/// 描述在创建时冻结 SQL 文本、参数和 Mutation 语义，不持有 Builder、连接、事务或根执行器。
/// </remarks>
public sealed class SqlMutationDescription
{
    /// <summary>
    /// 初始化一个 <see cref="SqlMutationDescription"/> 类型的实例。
    /// </summary>
    /// <param name="sql">已渲染的 SQL 文本。</param>
    /// <param name="parameters">已渲染的参数快照。</param>
    /// <param name="provider">生成 SQL 时使用的 Provider。</param>
    /// <param name="operationKind">Mutation 操作类型。</param>
    /// <param name="hasReturning">是否包含 Returning 或 Output 子句。</param>
    internal SqlMutationDescription(string sql, IEnumerable<SqlParam> parameters, ISqlProvider provider,
        SqlOperationKind operationKind, bool hasReturning)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("Mutation SQL 不能为空。", nameof(sql));
        if (provider == null)
            throw new ArgumentException("Mutation Builder 必须提供 SQL Provider。", nameof(provider));
        if (string.IsNullOrWhiteSpace(provider.Key))
            throw new ArgumentException("Mutation Builder 的 SQL Provider Key 不能为空。", nameof(provider));
        if (operationKind is not (SqlOperationKind.InsertValues or SqlOperationKind.InsertSelect or
            SqlOperationKind.Update or SqlOperationKind.Delete))
            throw new ArgumentException("Mutation 描述必须包含 Insert、Update 或 Delete 操作。", nameof(operationKind));
        Sql = sql;
        ProviderKey = provider.Key.Trim();
        ProviderProfile = SqlProviderCapabilityResolver.CreateSnapshot(provider);
        OperationKind = operationKind;
        HasReturning = hasReturning;
        Parameters = parameters?.Where(parameter => parameter != null)
            .Select(SqlMutationParameter.Create)
            .ToArray() ?? Array.Empty<SqlMutationParameter>();
    }

    /// <summary>
    /// 获取冻结后的 SQL 文本。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 获取生成 SQL 时冻结的 Provider 标识。
    /// </summary>
    public string ProviderKey { get; }

    /// <summary>
    /// 获取生成 SQL 时冻结的 Provider 能力档案。
    /// </summary>
    /// <remarks>
    /// 仅供框架内部执行前校验和诊断使用，调用方不得依赖其替代当前数据源的实际能力检查。
    /// </remarks>
    internal SqlProviderProfile ProviderProfile { get; }

    /// <summary>
    /// 获取冻结后的 Mutation 操作类型。
    /// </summary>
    public SqlOperationKind OperationKind { get; }

    /// <summary>
    /// 获取是否包含 Returning 或 Output 子句。
    /// </summary>
    public bool HasReturning { get; }

    /// <summary>
    /// 获取冻结后的参数集合。
    /// </summary>
    public IReadOnlyList<SqlMutationParameter> Parameters { get; }

    /// <summary>
    /// 为一次执行创建独立的增强参数实例。
    /// </summary>
    /// <returns>本次执行专属的参数集合。</returns>
    internal IReadOnlyCollection<SqlParam> CreateParameters() => Parameters.Select(parameter => parameter.CreateSqlParam())
        .ToArray();
}

/// <summary>
/// Mutation 描述的不可变参数快照。
/// </summary>
public sealed class SqlMutationParameter
{
    /// <summary>
    /// 初始化一个 <see cref="SqlMutationParameter"/> 类型的实例。
    /// </summary>
    private SqlMutationParameter(SqlParam parameter)
    {
        Name = parameter.Name;
        Value = SnapshotValue(parameter.Value);
        OriginalValue = SnapshotValue(parameter.OriginalValue);
        Direction = parameter.Direction;
        DbType = parameter.DbType;
        Size = parameter.Size;
        Precision = parameter.Precision;
        Scale = parameter.Scale;
        EntityType = parameter.EntityType;
        PropertyName = parameter.PropertyName;
        ColumnName = parameter.ColumnName;
        DatabaseType = parameter.DatabaseType;
        ProviderTypeName = parameter.ProviderTypeName;
        Source = parameter.Source;
        MetadataLevel = parameter.MetadataLevel;
        StorageKind = parameter.StorageKind;
        ConverterKind = parameter.ConverterKind;
        CustomConverterName = parameter.CustomConverterName;
    }

    /// <summary>
    /// 参数名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 参数值。
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Provider 转换前的原始参数值。
    /// </summary>
    public object OriginalValue { get; }

    /// <summary>
    /// 参数方向。
    /// </summary>
    public ParameterDirection? Direction { get; }

    /// <summary>
    /// 数据库类型。
    /// </summary>
    public DbType? DbType { get; }

    /// <summary>
    /// 参数长度。
    /// </summary>
    public int? Size { get; }

    /// <summary>
    /// 数值有效位数。
    /// </summary>
    public byte? Precision { get; }

    /// <summary>
    /// 数值小数位数。
    /// </summary>
    public byte? Scale { get; }

    /// <summary>
    /// 实体类型。
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// 属性名。
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// 列名。
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// 数据库类型标识。
    /// </summary>
    public DatabaseType? DatabaseType { get; }

    /// <summary>
    /// Provider 数据类型名称。
    /// </summary>
    public string ProviderTypeName { get; }

    /// <summary>
    /// 参数来源。
    /// </summary>
    public SqlParameterSource Source { get; }

    /// <summary>
    /// 参数元数据等级。
    /// </summary>
    public SqlParameterMetadataLevel MetadataLevel { get; }

    /// <summary>
    /// 字段存储方式。
    /// </summary>
    public ColumnStorageKind StorageKind { get; }

    /// <summary>
    /// 字段值转换器类型。
    /// </summary>
    public FieldValueConverterKind ConverterKind { get; }

    /// <summary>
    /// 自定义转换器名称。
    /// </summary>
    public string CustomConverterName { get; }

    /// <summary>
    /// 从可变参数创建不可变快照。
    /// </summary>
    /// <param name="parameter">待复制的参数。</param>
    /// <returns>不可变参数快照。</returns>
    internal static SqlMutationParameter Create(SqlParam parameter) => new(parameter ?? throw new ArgumentNullException(nameof(parameter)));

    /// <summary>
    /// 为一次执行重建可变参数对象。
    /// </summary>
    /// <returns>执行专属的增强参数。</returns>
    internal SqlParam CreateSqlParam() => new(Name, SnapshotValue(Value), DbType, Direction, Size, Precision, Scale)
    {
        OriginalValue = SnapshotValue(OriginalValue),
        EntityType = EntityType,
        PropertyName = PropertyName,
        ColumnName = ColumnName,
        DatabaseType = DatabaseType,
        ProviderTypeName = ProviderTypeName,
        Source = Source,
        MetadataLevel = MetadataLevel,
        StorageKind = StorageKind,
        ConverterKind = ConverterKind,
        CustomConverterName = CustomConverterName
    };

    /// <summary>
    /// 为常见数组值创建独立副本；其他引用类型保留调用时的对象引用。
    /// </summary>
    /// <param name="value">参数值。</param>
    /// <returns>参数值快照。</returns>
    private static object SnapshotValue(object value) => value is Array array ? array.Clone() : value;
}

/// <summary>
/// Mutation 描述创建扩展。
/// </summary>
public static class SqlMutationDescriptionExtensions
{
    /// <summary>
    /// 将当前可变 Builder 冻结为独立 Mutation 描述。
    /// </summary>
    /// <param name="builder">已完成 Mutation 构建的 SQL Builder。</param>
    /// <returns>不再依赖该 Builder 状态的 Mutation 描述。</returns>
    public static SqlMutationDescription ToMutationDescription(this ISqlBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var provider = builder.Provider ?? throw new InvalidOperationException("Mutation Builder 必须提供 SQL Provider。");
        if (string.IsNullOrWhiteSpace(provider.Key))
            throw new InvalidOperationException("Mutation Builder 的 SQL Provider Key 不能为空。");
        var hasReturning = builder is IReturningClauseAccessor { ReturningClause.IsEmpty: false };
        var sql = builder.ToSql();
        var parameters = builder.GetSqlParams();
        return new SqlMutationDescription(sql, parameters?.Values, provider, builder.OperationKind, hasReturning);
    }
}