using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 实体属性元数据。
/// </summary>
public sealed class EntityPropertyMetadata
{
    /// <summary>
    /// 初始化一个 <see cref="EntityPropertyMetadata"/> 类型的实例。
    /// </summary>
    /// <param name="property">CLR 属性。</param>
    /// <param name="columnName">原始列名。</param>
    /// <param name="isIgnored">是否忽略。</param>
    /// <param name="isKey">是否为主键。</param>
    /// <param name="databaseGeneratedOption">数据库生成选项。</param>
    /// <param name="isConcurrencyToken">是否为并发令牌。</param>
    /// <param name="isRequired">是否必填。</param>
    /// <param name="maxLength">最大长度。</param>
    /// <param name="providerTypeName">Provider 数据类型名称。</param>
    public EntityPropertyMetadata(PropertyInfo property, string columnName = null, bool isIgnored = false,
        bool isKey = false, DatabaseGeneratedOption databaseGeneratedOption = DatabaseGeneratedOption.None,
        bool isConcurrencyToken = false, bool isRequired = false, int? maxLength = null,
        string providerTypeName = null)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        PropertyName = property.Name;
        ColumnName = string.IsNullOrWhiteSpace(columnName) ? property.Name : columnName;
        IsIgnored = isIgnored;
        IsKey = isKey;
        DatabaseGeneratedOption = databaseGeneratedOption;
        IsConcurrencyToken = isConcurrencyToken;
        IsRequired = isRequired;
        MaxLength = maxLength;
        ProviderTypeName = providerTypeName;
    }

    /// <summary>
    /// 获取 CLR 属性。
    /// </summary>
    public PropertyInfo Property { get; }

    /// <summary>
    /// 获取 CLR 属性名。
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// 获取原始列名。
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// 获取 CLR 属性类型。
    /// </summary>
    public Type ClrType => Property.PropertyType;

    /// <summary>
    /// 获取属性是否忽略。
    /// </summary>
    public bool IsIgnored { get; }

    /// <summary>
    /// 获取属性是否为主键。
    /// </summary>
    public bool IsKey { get; }

    /// <summary>
    /// 获取数据库生成选项。
    /// </summary>
    public DatabaseGeneratedOption DatabaseGeneratedOption { get; }

    /// <summary>
    /// 获取属性是否由数据库生成。
    /// </summary>
    public bool IsDatabaseGenerated => DatabaseGeneratedOption != DatabaseGeneratedOption.None;

    /// <summary>
    /// 获取属性是否为并发令牌。
    /// </summary>
    public bool IsConcurrencyToken { get; }

    /// <summary>
    /// 获取属性是否必填。
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// 获取属性允许的最大长度。
    /// </summary>
    public int? MaxLength { get; }

    /// <summary>
    /// 获取 Provider 数据类型名称。
    /// </summary>
    public string ProviderTypeName { get; }

    /// <summary>
    /// 获取属性是否可空。
    /// </summary>
    public bool IsNullable => IsRequired == false && (ClrType.IsValueType == false || Nullable.GetUnderlyingType(ClrType) != null);

    /// <summary>
    /// 获取属性是否可插入。
    /// </summary>
    public bool CanInsert => IsIgnored == false && DatabaseGeneratedOption != DatabaseGeneratedOption.Identity &&
        DatabaseGeneratedOption != DatabaseGeneratedOption.Computed;

    /// <summary>
    /// 获取属性是否可更新。
    /// </summary>
    public bool CanUpdate => IsIgnored == false && IsKey == false &&
        DatabaseGeneratedOption != DatabaseGeneratedOption.Identity &&
        DatabaseGeneratedOption != DatabaseGeneratedOption.Computed;
}