using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 描述实体 CLR 属性的列映射和写入约束元数据。
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
        ClrType = property.PropertyType;
        CanRead = property.GetMethod?.IsPublic == true && !property.GetMethod.IsStatic && property.GetIndexParameters().Length == 0;
        CanWrite = property.SetMethod?.IsPublic == true && !property.SetMethod.IsStatic;
        HasNonPublicSetter = property.SetMethod != null && !property.SetMethod.IsPublic;
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
    /// 创建描述实体属性映射能力的元数据。
    /// </summary>
    /// <param name="propertyName">实体属性名称。</param>
    /// <param name="clrType">属性的 CLR 类型。</param>
    /// <param name="canRead">属性是否可读取。</param>
    /// <param name="canWrite">属性是否可写入。</param>
    /// <param name="columnName">可选的数据库列名。</param>
    /// <param name="isIgnored">是否忽略该属性。</param>
    /// <param name="isKey">是否为主键。</param>
    /// <param name="databaseGeneratedOption">数据库生成方式。</param>
    /// <param name="isConcurrencyToken">是否为并发令牌。</param>
    /// <param name="isRequired">是否必需。</param>
    /// <param name="maxLength">可选的最大长度。</param>
    /// <param name="providerTypeName">可选的 Provider 类型名称。</param>
    /// <param name="generatedGetter">生成的属性读取器。</param>
    /// <param name="generatedSetter">生成的属性写入器。</param>
    /// <returns>包含指定属性映射信息的实体属性元数据。</returns>
    public static EntityPropertyMetadata CreateGenerated(string propertyName, Type clrType, bool canRead, bool canWrite,
        string columnName = null, bool isIgnored = false, bool isKey = false,
        DatabaseGeneratedOption databaseGeneratedOption = DatabaseGeneratedOption.None,
        bool isConcurrencyToken = false, bool isRequired = false, int? maxLength = null, string providerTypeName = null,
        Func<object, object> generatedGetter = null, Action<object, object> generatedSetter = null) =>
        new(propertyName, clrType, canRead, canWrite, columnName, isIgnored, isKey, databaseGeneratedOption,
            isConcurrencyToken, isRequired, maxLength, providerTypeName, generatedGetter, generatedSetter, false);

    /// <summary>
    /// 创建包含非公开 setter 标记的源码生成属性元数据。
    /// </summary>
    /// <param name="propertyName">实体属性名称。</param>
    /// <param name="clrType">属性的 CLR 类型。</param>
    /// <param name="canRead">属性是否可读取。</param>
    /// <param name="canWrite">属性是否可写入。</param>
    /// <param name="columnName">数据库列名。</param>
    /// <param name="isIgnored">是否忽略该属性。</param>
    /// <param name="isKey">是否为主键。</param>
    /// <param name="databaseGeneratedOption">数据库生成方式。</param>
    /// <param name="isConcurrencyToken">是否为并发令牌。</param>
    /// <param name="isRequired">是否必需。</param>
    /// <param name="maxLength">最大长度。</param>
    /// <param name="providerTypeName">Provider 类型名称。</param>
    /// <param name="generatedGetter">生成的属性读取器。</param>
    /// <param name="generatedSetter">生成的属性写入器。</param>
    /// <param name="hasNonPublicSetter">属性是否具有非公开 setter。</param>
    /// <returns>包含指定属性映射信息的实体属性元数据。</returns>
    public static EntityPropertyMetadata CreateGenerated(string propertyName, Type clrType, bool canRead, bool canWrite,
        string columnName, bool isIgnored, bool isKey, DatabaseGeneratedOption databaseGeneratedOption,
        bool isConcurrencyToken, bool isRequired, int? maxLength, string providerTypeName,
        Func<object, object> generatedGetter, Action<object, object> generatedSetter, bool hasNonPublicSetter) =>
        new(propertyName, clrType, canRead, canWrite, columnName, isIgnored, isKey, databaseGeneratedOption,
            isConcurrencyToken, isRequired, maxLength, providerTypeName, generatedGetter, generatedSetter,
            hasNonPublicSetter);

    /// <summary>
    /// 初始化一个 <see cref="EntityPropertyMetadata" /> 类型的实例。
    /// </summary>
    /// <param name="propertyName">对应的属性名称。</param>
    /// <param name="clrType">属性值的 CLR 类型。</param>
    /// <param name="canRead">指示该属性是否可读取。</param>
    /// <param name="canWrite">指示该属性是否可写入。</param>
    /// <param name="columnName">对应的数据库列名称。</param>
    /// <param name="isIgnored">指示该属性是否忽略映射。</param>
    /// <param name="isKey">指示该属性是否为主键。</param>
    /// <param name="databaseGeneratedOption">数据库生成主键的策略。</param>
    /// <param name="isConcurrencyToken">指示该属性是否参与并发冲突检测。</param>
    /// <param name="isRequired">指示该属性是否必填。</param>
    /// <param name="maxLength">允许的最大长度。</param>
    /// <param name="providerTypeName">Provider 对应的类型名称。</param>
    /// <param name="generatedGetter">生成的属性读取器。</param>
    /// <param name="generatedSetter">生成的属性写入器。</param>
    /// <param name="hasNonPublicSetter">属性是否具有非公开 setter。</param>
    private EntityPropertyMetadata(string propertyName, Type clrType, bool canRead, bool canWrite,
        string columnName = null, bool isIgnored = false, bool isKey = false,
        DatabaseGeneratedOption databaseGeneratedOption = DatabaseGeneratedOption.None,
        bool isConcurrencyToken = false, bool isRequired = false, int? maxLength = null, string providerTypeName = null,
        Func<object, object> generatedGetter = null, Action<object, object> generatedSetter = null,
        bool hasNonPublicSetter = false)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("属性名不能为空。", nameof(propertyName));
        PropertyName = propertyName;
        ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
        CanRead = canRead;
        CanWrite = canWrite;
        HasNonPublicSetter = hasNonPublicSetter;
        ColumnName = string.IsNullOrWhiteSpace(columnName) ? propertyName : columnName;
        IsIgnored = isIgnored;
        IsKey = isKey;
        DatabaseGeneratedOption = databaseGeneratedOption;
        IsConcurrencyToken = isConcurrencyToken;
        IsRequired = isRequired;
        MaxLength = maxLength;
        ProviderTypeName = providerTypeName;
        GeneratedGetter = generatedGetter;
        GeneratedSetter = generatedSetter;
    }

    /// <summary>
    /// 获取CLR 属性。
    /// </summary>
    /// <remarks>生成描述的该属性为空；查询读取使用成员描述及生成读取器。</remarks>
    public PropertyInfo Property { get; }

    /// <summary>
    /// 获取由源码生成的属性读取器；反射描述元数据没有此委托。
    /// </summary>
    public Func<object, object> GeneratedGetter { get; }

    /// <summary>
    /// 获取由源码生成的属性写入器；反射描述元数据没有此委托。
    /// </summary>
    public Action<object, object> GeneratedSetter { get; }

    /// <summary>获取生成的强类型读取赋值器；存储类型不匹配时返回 false，由通用转换处理。</summary>
    public Func<object, System.Data.Common.DbDataReader, int, bool> GeneratedReaderSetter { get; private set; }

    /// <summary>创建附带强类型读取赋值器的新描述，保留原描述及旧生成合同。</summary>
    /// <param name="readerSetter">直接读取并写入属性的委托；返回 false 时不得修改目标。</param>
    /// <returns>包含读取赋值器的新元数据。</returns>
    public EntityPropertyMetadata WithGeneratedReader(Func<object, System.Data.Common.DbDataReader, int, bool> readerSetter)
    {
        if (readerSetter == null) throw new ArgumentNullException(nameof(readerSetter));
        // 描述中的字段均为不可变值或共享委托；浅复制也能完整保留旧反射描述。
        var result = (EntityPropertyMetadata)MemberwiseClone();
        result.GeneratedReaderSetter = readerSetter;
        return result;
    }

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
    public Type ClrType { get; }

    /// <summary>
    /// 获取是否具有可用的公开实例读取器。
    /// </summary>
    public bool CanRead { get; }

    /// <summary>
    /// 获取是否具有可用的公开实例写入器。
    /// </summary>
    public bool CanWrite { get; }

    /// <summary>
    /// 获取属性是否具有非公开 setter。
    /// </summary>
    public bool HasNonPublicSetter { get; }

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
