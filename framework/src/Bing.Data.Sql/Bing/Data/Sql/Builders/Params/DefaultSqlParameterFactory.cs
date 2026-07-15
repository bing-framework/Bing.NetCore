using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 默认 Sql 参数工厂
/// </summary>
public class DefaultSqlParameterFactory : ISqlParameterFactory
{
    /// <summary>
    /// 字段值转换器选择器
    /// </summary>
    private readonly IFieldValueConverterSelector _selector;

    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _options;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlParameterFactory"/>类型的实例
    /// </summary>
    /// <param name="selector">字段值转换器选择器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="options">Sql 元数据配置</param>
    public DefaultSqlParameterFactory(IFieldValueConverterSelector selector,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions options = null)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _databaseContextAccessor = databaseContextAccessor;
        _options = options ?? new SqlMetadataOptions();
    }

    /// <summary>
    /// 创建 Sql 参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <param name="column">列映射元数据</param>
    /// <param name="databaseContext">数据库上下文</param>
    /// <param name="entityType">实体类型</param>
    /// <param name="source">参数来源</param>
    /// <returns>Sql 参数</returns>
    public SqlParam Create(string name, object value, ColumnMappingMetadata column, DatabaseContext databaseContext,
        Type entityType = null, SqlParameterSource source = SqlParameterSource.Unknown)
    {
        var context = GetDatabaseContext(databaseContext);
        var convertedValue = column == null ? value : _selector.ConvertToProvider(value, column, context);
        return new SqlParam(name, convertedValue, column?.DbType, null, column?.Size, column?.Precision, column?.Scale)
        {
            OriginalValue = value,
            EntityType = entityType,
            PropertyName = column?.PropertyName,
            ColumnName = column?.ColumnName,
            DatabaseType = context?.DataSource?.DatabaseType,
            ProviderTypeName = column?.ProviderTypeName,
            Source = source,
            MetadataLevel = column == null ? SqlParameterMetadataLevel.Weak : SqlParameterMetadataLevel.Full,
            StorageKind = column?.StorageKind ?? ColumnStorageKind.Default,
            ConverterKind = column?.ConverterKind ?? FieldValueConverterKind.None,
            CustomConverterName = column?.CustomConverterName
        };
    }

    /// <summary>
    /// 获取数据库上下文
    /// </summary>
    /// <param name="databaseContext">数据库上下文</param>
    /// <returns>数据库上下文</returns>
    protected virtual DatabaseContext GetDatabaseContext(DatabaseContext databaseContext)
    {
        if (databaseContext != null)
            return databaseContext;
        if (_databaseContextAccessor?.Current != null)
            return _databaseContextAccessor.Current;
        if (_options.DefaultDatabaseContext != null)
            return _options.DefaultDatabaseContext;
        return new DatabaseContext();
    }
}
