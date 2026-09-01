using System.Data;

namespace Bing.Data.Metadata;

/// <summary>
/// 将 PostgreSQL 数据类型转换为数据库类型。
/// </summary>
public class PostgreSqlTypeConverter : ITypeConverter
{
    /// <inheritdoc />
    public DbType? ToDbType(string dataType, int? length = null)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return null;
        switch (dataType.ToLowerInvariant())
        {
            case "uuid":
                return DbType.Guid;
            case "varchar":
            case "text":
            case "json":
            case "jsonb":
            case "xml":
                return DbType.String;
            case "bool":
                return DbType.Boolean;
            case "char":
                return DbType.Byte;
            case "int2":
                return DbType.Int16;
            case "int4":
                return DbType.Int32;
            case "int8":
                return DbType.Int64;
            case "float4":
                return DbType.Single;
            case "float8":
                return DbType.Double;
            case "numeric":
            case "decimal":
                return DbType.Decimal;
            case "date":
                return DbType.Date;
            case "time":
            case "timetz":
                return DbType.Time;
            case "timestamp":
            case "timestamptz":
                return DbType.DateTime;
            case "bytea":
                return DbType.Binary;
        }
        throw new NotSupportedException(
            $"PostgreSQL 数据类型 '{dataType}' 当前没有映射到 DbType。请扩展 PostgreSqlTypeConverter 映射，或使用已支持的数据类型。");
    }
}
