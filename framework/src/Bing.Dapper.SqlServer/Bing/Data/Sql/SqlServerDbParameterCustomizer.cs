using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.SqlClient;

namespace Bing.Data.Sql;

/// <summary>
/// Sql Server 数据库参数定制器
/// </summary>
public sealed class SqlServerDbParameterCustomizer : ISqlDbParameterCustomizer
{
    /// <inheritdoc />
    /// <returns>数据库类型为 SQL Server 时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    public bool CanHandle(DatabaseType databaseType) => databaseType == DatabaseType.SqlServer;

    /// <inheritdoc />
    public void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter)
    {
        if (dbParameter is not SqlParameter parameter ||
            Enum.TryParse<SqlDbType>(GetTypeName(sqlParameter?.ProviderTypeName), true, out var providerType) == false)
            return;
        parameter.SqlDbType = providerType;
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
