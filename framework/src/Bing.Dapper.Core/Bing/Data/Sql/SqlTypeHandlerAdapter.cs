using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// 将显式登记的类型处理方法接入生成读取和真实数据库参数绑定，不扫描 Dapper 全局注册表。
/// </summary>
/// <typeparam name="T">处理的 CLR 类型。</typeparam>
/// <remarks>同一实例必须同时登记为 IFieldValueConverter 和 ISqlDbParameterCustomizer，并位于默认转换器之前。</remarks>
public sealed class SqlTypeHandlerAdapter<T> : IFieldValueConverter, ISqlDbParameterCustomizer, ISqlTypeHandlerParameterAdapter
{
    private readonly Func<object, T> _parse;
    private readonly Action<IDbDataParameter, T> _setValue;

    /// <summary>初始化显式类型处理适配器。</summary>
    /// <param name="parse">从数据库值读取 CLR 值的方法。</param>
    /// <param name="setValue">配置真实数据库参数的方法，保留处理器对类型、长度和值的设置。</param>
    public SqlTypeHandlerAdapter(Func<object, T> parse, Action<IDbDataParameter, T> setValue)
    {
        _parse = parse ?? throw new ArgumentNullException(nameof(parse));
        _setValue = setValue ?? throw new ArgumentNullException(nameof(setValue));
    }

    /// <inheritdoc />
    public bool CanConvert(ColumnMappingMetadata column, DatabaseContext databaseContext) =>
        (Nullable.GetUnderlyingType(column?.ClrType ?? typeof(object)) ?? column?.ClrType) == typeof(T);

    /// <inheritdoc />
    public object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext) => value;

    /// <inheritdoc />
    public object ConvertFromProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext) =>
        value == null || value is DBNull ? value : _parse(value);

    /// <inheritdoc />
    public bool CanHandle(DatabaseType databaseType) => true;

    /// <inheritdoc />
    public void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter)
    {
        if (dbParameter == null) throw new ArgumentNullException(nameof(dbParameter));
        // 只处理实际值，不将其他列或 NULL 强行转换为 T。
        if (sqlParameter?.Value is T value) _setValue(dbParameter, value);
    }
}

