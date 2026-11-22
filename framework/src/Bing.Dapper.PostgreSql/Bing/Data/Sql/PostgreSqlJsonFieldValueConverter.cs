using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>在 PostgreSQL JSON 文本和应用 CLR 类型之间使用静态元数据转换。</summary>
internal sealed class PostgreSqlJsonFieldValueConverter<T>(JsonTypeInfo<T> typeInfo) : IFieldValueConverter
{
    /// <inheritdoc />
    public bool CanConvert(ColumnMappingMetadata column, DatabaseContext databaseContext) =>
        databaseContext?.DataSource?.DatabaseType == DatabaseType.PgSql &&
        column?.ClrType == typeof(T) &&
        column.ConverterKind is FieldValueConverterKind.None or FieldValueConverterKind.JsonSerialize &&
        (string.Equals(column.ProviderTypeName, "json", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(column.ProviderTypeName, "jsonb", StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext) =>
        value is null or DBNull ? null : JsonSerializer.Serialize((T)value, typeInfo);

    /// <inheritdoc />
    public object ConvertFromProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext) =>
        value is null or DBNull ? null : JsonSerializer.Deserialize((string)value, typeInfo);
}
