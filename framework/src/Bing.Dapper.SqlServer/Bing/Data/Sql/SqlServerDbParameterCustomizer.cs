using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.SqlClient;

namespace Bing.Data.Sql;

/// <summary>
/// 定制 SQL Server 数据库参数。
/// </summary>
public sealed class SqlServerDbParameterCustomizer : ISqlDbParameterCustomizer
{
    /// <inheritdoc />
    public bool CanHandle(DatabaseType databaseType) => databaseType == DatabaseType.SqlServer;

    /// <inheritdoc />
    public void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter)
    {
        if (dbParameter is not SqlParameter parameter || sqlParameter?.ProviderTypeName is not { } declaration)
            return;

        var index = declaration.IndexOf('(');
        var typeName = (index < 0 ? declaration : declaration.Substring(0, index)).Trim();
        SqlDbType providerType;
        if (string.Equals(typeName, "sql_variant", StringComparison.OrdinalIgnoreCase))
            providerType = SqlDbType.Variant;
        else if (Enum.TryParse(typeName, true, out providerType) == false)
            return;
        parameter.SqlDbType = providerType;

        // 驱动从值推断的长度会随短值或空值变化。仅在调用方未指定长度时使用列的 MAX 声明。
        if (index >= 0 && sqlParameter.Size == null &&
            providerType is SqlDbType.NVarChar or SqlDbType.VarChar or SqlDbType.VarBinary)
        {
            var length = declaration.Substring(index + 1).TrimEnd();
            if (length.EndsWith(")", StringComparison.Ordinal) &&
                string.Equals(length.Substring(0, length.Length - 1).Trim(), "max", StringComparison.OrdinalIgnoreCase))
                parameter.Size = -1;
        }
    }
}
