using System;
using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Oracle.ManagedDataAccess.Client;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle 数据库参数定制器
/// </summary>
public sealed class OracleDbParameterCustomizer : ISqlDbParameterCustomizer
{
    /// <inheritdoc />
    /// <returns>数据库类型为 Oracle 时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    public bool CanHandle(DatabaseType databaseType) => databaseType == DatabaseType.Oracle;

    /// <inheritdoc />
    public void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter)
    {
        if (dbParameter is not OracleParameter parameter ||
            Enum.TryParse<OracleDbType>(GetTypeName(sqlParameter?.ProviderTypeName), true, out var providerType) == false)
            return;
        parameter.OracleDbType = providerType;
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
