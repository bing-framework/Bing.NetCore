using System;
using System.Data;

namespace Bing.Data.Metadata;

/// <summary>
/// Oracle数据类型转换器
/// </summary>
public class OracleTypeConverter : ITypeConverter
{
    /// <inheritdoc />
    /// <returns>对应的数据库类型；无法识别数据类型时返回 <see langword="null"/>。</returns>
    public DbType? ToDbType(string dataType, int? length = null)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return null;
        switch (dataType.Trim().ToLowerInvariant())
        {
            case "varchar2":
            case "nvarchar2":
            case "char":
            case "nchar":
            case "long":
            case "clob":
            case "nclob":
            case "rowid":
            case "urowid":
                return DbType.String;
            case "number":
            case "numeric":
            case "decimal":
                return DbType.Decimal;
            case "binary_float":
                return DbType.Single;
            case "binary_double":
            case "float":
                return DbType.Double;
            case "date":
                return DbType.DateTime;
            case "timestamp":
                return DbType.DateTime2;
            case "timestamp with time zone":
            case "timestamp with local time zone":
                return DbType.DateTimeOffset;
            case "raw":
            case "long raw":
            case "blob":
            case "bfile":
                return DbType.Binary;
            case "xmltype":
                return DbType.Xml;
            case "boolean":
                return DbType.Boolean;
        }
        throw new NotSupportedException($"不支持 Oracle 数据类型 {dataType}。");
    }
}
