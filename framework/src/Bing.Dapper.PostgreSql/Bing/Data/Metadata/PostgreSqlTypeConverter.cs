using System.Data;
using NpgsqlTypes;

namespace Bing.Data.Metadata;

/// <summary>
/// 将 PostgreSQL 数据类型转换为数据库类型。
/// </summary>
public class PostgreSqlTypeConverter : ITypeConverter
{
    private static readonly Dictionary<string, NpgsqlDbType> RangeParameterTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["int4range"] = NpgsqlDbType.IntegerRange,
        ["int4multirange"] = NpgsqlDbType.IntegerMultirange,
        ["int8range"] = NpgsqlDbType.BigIntRange,
        ["int8multirange"] = NpgsqlDbType.BigIntMultirange,
        ["numrange"] = NpgsqlDbType.NumericRange,
        ["nummultirange"] = NpgsqlDbType.NumericMultirange,
        ["daterange"] = NpgsqlDbType.DateRange,
        ["datemultirange"] = NpgsqlDbType.DateMultirange,
        ["tsrange"] = NpgsqlDbType.TimestampRange,
        ["tsmultirange"] = NpgsqlDbType.TimestampMultirange,
        ["tstzrange"] = NpgsqlDbType.TimestampTzRange,
        ["tstzmultirange"] = NpgsqlDbType.TimestampTzMultirange
    };

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
        // DbType 无法表示范围类型，交给 Npgsql 参数定制器绑定。
        if (RangeParameterTypes.ContainsKey(dataType))
            return null;
        throw new NotSupportedException(
            $"PostgreSQL 数据类型 '{dataType}' 当前没有映射到 DbType。请扩展 PostgreSqlTypeConverter 映射，或使用已支持的数据类型。");
    }

    /// <summary>将 PostgreSQL 内置范围列的 SQL 类型名转换为参数类型。</summary>
    internal static bool TryGetRangeParameterType(string typeName, out NpgsqlDbType type)
    {
        type = default;
        return typeName != null && RangeParameterTypes.TryGetValue(typeName, out type);
    }
}
