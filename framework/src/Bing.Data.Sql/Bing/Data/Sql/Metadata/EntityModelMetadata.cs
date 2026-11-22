using System.Collections.ObjectModel;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 描述 CLR 实体类型的表、架构和属性元数据。
/// </summary>
public sealed class EntityModelMetadata
{
    /// <summary>
    /// 初始化一个 <see cref="EntityModelMetadata"/> 类型的实例。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="tableName">原始表名。</param>
    /// <param name="schema">原始架构名。</param>
    /// <param name="properties">属性元数据集合。</param>
    /// <param name="generatedFactory">由生成代码提供的实体构造委托。</param>
    public EntityModelMetadata(Type entityType, string tableName, string schema,
        IEnumerable<EntityPropertyMetadata> properties, Func<object> generatedFactory = null)
        : this(entityType, tableName, schema, properties, generatedFactory, null)
    {
    }

    /// <summary>
    /// 初始化一个包含生成式 Mutation 值读取器的 <see cref="EntityModelMetadata" /> 类型实例。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="tableName">原始表名。</param>
    /// <param name="schema">原始架构名。</param>
    /// <param name="properties">属性元数据。</param>
    /// <param name="generatedFactory">生成的实体构造委托。</param>
    /// <param name="generatedValueReader">按属性名读取实体值的生成委托。</param>
    public EntityModelMetadata(Type entityType, string tableName, string schema,
        IEnumerable<EntityPropertyMetadata> properties, Func<object> generatedFactory,
        Func<object, string, object> generatedValueReader)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        TableName = tableName;
        Schema = schema;
        GeneratedFactory = generatedFactory;
        GeneratedValueReader = generatedValueReader;
        var values = (properties ?? Enumerable.Empty<EntityPropertyMetadata>())
            .Where(property => property != null)
            .ToDictionary(property => property.PropertyName, StringComparer.OrdinalIgnoreCase);
        Properties = new ReadOnlyDictionary<string, EntityPropertyMetadata>(values);
    }

    /// <summary>
    /// 获取实体类型。
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// 获取原始表名。
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// 获取原始架构名。
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// 获取按 CLR 属性名索引的属性元数据集合。
    /// </summary>
    public IReadOnlyDictionary<string, EntityPropertyMetadata> Properties { get; }

    /// <summary>
    /// 获取由生成代码提供的实体构造委托；类型不支持直接构造时返回空值。
    /// </summary>
    public Func<object> GeneratedFactory { get; }

    /// <summary>
    /// 获取由生成代码提供的实体值读取委托；按属性名直接读取值，不触发运行时反射。
    /// </summary>
    /// <remarks>
    /// 该委托只在生成模型的参与列都具有生成 Getter 时由 Mutation Plan 使用；
    /// 兼容模型或不完整模型继续使用既有 Getter 缓存和回退路径。
    /// </remarks>
    public Func<object, string, object> GeneratedValueReader { get; }

    /// <summary>
    /// 获取按 Provider 标识和操作类型选择完整 SQL 常量的生成委托；未覆盖时返回空值。
    /// </summary>
    /// <remarks>
    /// 仅在最终映射与生成模型一致时使用。参数转换、校验、数据边界和执行仍由运行时负责。
    /// </remarks>
    public Func<string, Builders.SqlOperationKind, string> GeneratedMutationSql { get; init; }

    /// <summary>
    /// 获取按 Provider 标识选择单个自增主键 Insert SQL 常量的生成委托；未覆盖时返回空值。
    /// </summary>
    /// <remarks>
    /// 仅最终映射与生成模型一致时使用；按 Provider 使用 Returning、Output 或既有身份值读取。
    /// 键策略、静态读取、回填和事务仍由原执行链负责。
    /// </remarks>
    public Func<string, string> GeneratedKeyInsertSql { get; init; }

    /// <summary>获取有限列筛选及批量形状的完整 SQL 常量；未声明的形状返回空值。</summary>
    /// <remarks>
    /// 参数依次为 Provider、操作、单个写属性名及行数；属性名为空表示全部默认写列。
    /// 当前生成器覆盖单列 Insert/Update 和支持多行 Values 的双行默认列 Insert，避免枚举列子集。
    /// 仅最终映射及默认参数、格式化服务兼容时使用，不保存实体或请求值。
    /// </remarks>
    public Func<string, Builders.SqlOperationKind, string, int, string> GeneratedMutationShapeSql { get; init; }
}
