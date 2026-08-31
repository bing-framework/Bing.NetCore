using System.Data;

namespace Bing.Data.Metadata;

/// <summary>
/// Sqlite数据类型转换器
/// </summary>
public class SqliteTypeConverter : ITypeConverter
{
    /// <inheritdoc />
    /// <returns>对应的数据库类型；无法识别数据类型时返回 <see cref="DbType.Object"/>，空数据类型返回 <see langword="null"/>。</returns>
    public DbType? ToDbType(string dataType, int? length = null)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return null;
        switch (dataType.ToLowerInvariant())
        {
            case "integer":
                return DbType.Int64;
            case "real":
                return DbType.Double;
            case "text":
                return DbType.String;
            case "blob":
                return DbType.Binary;
            default:
                return DbType.Object;
        }
    }
}
