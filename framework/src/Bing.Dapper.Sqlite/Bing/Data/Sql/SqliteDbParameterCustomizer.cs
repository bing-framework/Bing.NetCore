using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.Sqlite;

namespace Bing.Data.Sql;

/// <summary>
/// Sqlite 数据库参数定制器
/// </summary>
public sealed class SqliteDbParameterCustomizer : ISqlDbParameterCustomizer
{
    /// <inheritdoc />
    /// <returns>数据库类型为 SQLite 时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    public bool CanHandle(DatabaseType databaseType) => databaseType == DatabaseType.Sqlite;

    /// <inheritdoc />
    public void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter)
    {
        if (dbParameter is not SqliteParameter parameter ||
            Enum.TryParse<SqliteType>(GetTypeName(sqlParameter?.ProviderTypeName), true, out var providerType) == false)
            return;
        parameter.SqliteType = providerType;
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
