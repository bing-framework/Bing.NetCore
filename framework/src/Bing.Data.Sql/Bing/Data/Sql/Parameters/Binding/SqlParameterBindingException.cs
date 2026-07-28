namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数绑定异常
/// </summary>
public sealed class SqlParameterBindingException : Exception
{
    /// <summary>
    /// 初始化一个<see cref="SqlParameterBindingException"/>类型的实例
    /// </summary>
    /// <param name="parameterName">参数名称</param>
    /// <param name="context">参数绑定上下文</param>
    /// <param name="propertyName">关联属性名称</param>
    public SqlParameterBindingException(string parameterName, SqlParameterBindingContext context,
        string propertyName = null)
        : base(CreateMessage(parameterName, context, propertyName))
    {
        ParameterName = parameterName;
        Sql = context?.Sql;
        DbKey = context?.DbKey;
        SourceType = context?.Source?.GetType();
        EntityType = context?.EntityType;
        PropertyName = propertyName;
    }

    /// <summary>
    /// 参数名称
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// SQL 语句
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 数据源标识
    /// </summary>
    public string DbKey { get; }

    /// <summary>
    /// 参数源类型
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// 关联属性名称
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// 创建异常消息
    /// </summary>
    /// <param name="parameterName">参数名称</param>
    /// <param name="context">参数绑定上下文</param>
    /// <param name="propertyName">关联属性名称</param>
    /// <returns>异常消息</returns>
    private static string CreateMessage(string parameterName, SqlParameterBindingContext context, string propertyName)
    {
        return $"无法解析 SQL 参数 '{parameterName}'。SQL: {context?.Sql ?? "<未提供>"}；DbKey: {context?.DbKey ?? "<未提供>"}；" +
               $"参数源类型: {context?.Source?.GetType().FullName ?? "<未提供>"}；实体类型: {context?.EntityType?.FullName ?? "<未提供>"}；" +
               $"关联属性: {propertyName ?? "<未提供>"}。";
    }
}