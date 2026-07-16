namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据源连接字符串解析器
/// </summary>
public interface ISqlConnectionStringResolver
{
    /// <summary>
    /// 解析指定数据源的连接字符串
    /// </summary>
    /// <param name="dataSource">SQL 数据源描述</param>
    /// <returns>已解析的连接字符串</returns>
    string Resolve(SqlDataSourceDescriptor dataSource);
}

/// <summary>
/// 默认 SQL 数据源连接字符串解析器
/// </summary>
public sealed class DefaultSqlConnectionStringResolver : ISqlConnectionStringResolver
{
    /// <summary>
    /// 已注册的连接字符串集合
    /// </summary>
    private readonly ConnectionStringCollection _connectionStrings;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlConnectionStringResolver"/>类型的实例
    /// </summary>
    /// <param name="connectionStrings">连接字符串集合</param>
    public DefaultSqlConnectionStringResolver(ConnectionStringCollection connectionStrings = null) =>
        _connectionStrings = connectionStrings;

    /// <inheritdoc />
    public string Resolve(SqlDataSourceDescriptor dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource?.ConnectionString) == false)
            return dataSource.ConnectionString;
        if (string.IsNullOrWhiteSpace(dataSource?.ConnectionStringName) == false)
        {
            var connectionString = _connectionStrings != null &&
                                   _connectionStrings.TryGetValue(dataSource.ConnectionStringName, out var value)
                ? value
                : null;
            if (string.IsNullOrWhiteSpace(connectionString) == false)
                return connectionString;
            throw new InvalidOperationException(
                $"SQL 数据源 {dataSource.Key} 未找到连接字符串。缺失配置字段: {nameof(SqlDataSourceDescriptor.ConnectionString)} 或连接字符串名称 {dataSource.ConnectionStringName}。");
        }
        throw new InvalidOperationException(
            $"SQL 数据源 {dataSource?.Key ?? "<未指定>"} 缺少连接字符串配置。缺失配置字段: {nameof(SqlDataSourceDescriptor.ConnectionString)} 或 {nameof(SqlDataSourceDescriptor.ConnectionStringName)}。");
    }
}
