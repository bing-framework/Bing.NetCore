namespace Bing.Data.Sql.Generated;

/// <summary>
/// 将实体类型加入显式 SQL 模型生成上下文。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SqlModelAttribute : Attribute
{
    /// <summary>
    /// 初始化一个 <see cref="SqlModelAttribute"/> 实例。
    /// </summary>
    /// <param name="modelType">实体类型。</param>
    public SqlModelAttribute(Type modelType) => ModelType = modelType ?? throw new ArgumentNullException(nameof(modelType));

    /// <summary>
    /// 获取要生成元数据的实体类型。
    /// </summary>
    public Type ModelType { get; }
}

/// <summary>
/// 将查询结果类型加入显式 SQL 模型生成上下文。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SqlResultAttribute : Attribute
{
    /// <summary>
    /// 初始化一个 <see cref="SqlResultAttribute"/> 实例。
    /// </summary>
    /// <param name="resultType">查询结果类型。</param>
    public SqlResultAttribute(Type resultType) => ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));

    /// <summary>
    /// 获取要生成元数据的查询结果类型。
    /// </summary>
    public Type ResultType { get; }
}

/// <summary>
/// 将命名参数类型加入显式 SQL 模型生成上下文。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SqlParametersAttribute : Attribute
{
    /// <summary>
    /// 初始化一个 <see cref="SqlParametersAttribute"/> 实例。
    /// </summary>
    /// <param name="parametersType">命名参数类型。</param>
    public SqlParametersAttribute(Type parametersType) => ParametersType = parametersType ?? throw new ArgumentNullException(nameof(parametersType));

    /// <summary>
    /// 获取要生成元数据的命名参数类型。
    /// </summary>
    public Type ParametersType { get; }
}
