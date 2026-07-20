using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using MySqlConnector;

namespace Bing.Data.Sql;

/// <summary>
/// MySql 数据库参数定制器
/// </summary>
public sealed class MySqlDbParameterCustomizer : ISqlDbParameterCustomizer
{
    /// <inheritdoc />
    public bool CanHandle(DatabaseType databaseType) => databaseType == DatabaseType.MySql ||
                                                       databaseType == DatabaseType.Doris;

    /// <inheritdoc />
    public void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter)
    {
        if (dbParameter is not MySqlParameter parameter ||
            Enum.TryParse<MySqlDbType>(GetTypeName(sqlParameter?.ProviderTypeName), true, out var providerType) == false)
            return;
        parameter.MySqlDbType = providerType;
    }

    /// <summary>
    /// 获取不含长度声明的 Provider 类型名
    /// </summary>
    /// <param name="typeName">Provider 类型名</param>
    /// <returns>基础类型名</returns>
    private static string GetTypeName(string typeName)
    {
        var index = typeName?.IndexOf('(') ?? -1;
        return index < 0 ? typeName : typeName.Substring(0, index).Trim();
    }
}