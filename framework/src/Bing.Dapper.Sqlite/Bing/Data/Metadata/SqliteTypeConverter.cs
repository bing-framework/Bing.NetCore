using System.Data;

namespace Bing.Data.Metadata;

/// <summary>
/// Sqlite数据类型转换器
/// </summary>
public class SqliteTypeConverter : ITypeConverter
{
    /// <inheritdoc />
    public DbType? ToDbType(string dataType, int? length = null)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return null;
        switch (dataType.ToLower())
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
        throw new NotImplementedException(dataType);
    }
}
