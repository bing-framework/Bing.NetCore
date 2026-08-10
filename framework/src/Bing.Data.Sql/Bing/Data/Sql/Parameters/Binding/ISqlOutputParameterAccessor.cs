namespace Bing.Data.Sql;

/// <summary>
/// SQL 输出参数访问器
/// </summary>
public interface ISqlOutputParameterAccessor
{
    /// <summary>
    /// 获取输出参数值
    /// </summary>
    /// <param name="name">参数名称</param>
    /// <returns>输出参数值</returns>
    object GetValue(string name);

    /// <summary>
    /// 获取输出参数值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="name">参数名称</param>
    /// <returns>转换后的输出参数值</returns>
    T GetValue<T>(string name);

    /// <summary>
    /// 尝试获取输出参数值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="name">参数名称</param>
    /// <param name="value">输出参数值</param>
    /// <returns>获取成功时返回 true</returns>
    bool TryGetValue<T>(string name, out T value);
}

/// <summary>
/// 提供执行完成后输出参数值快照的内部契约。
/// </summary>
internal interface ISqlOutputParameterSnapshotProvider
{
    /// <summary>
    /// 创建当前输出参数的独立值快照。
    /// </summary>
    /// <returns>不再依赖 ADO.NET 参数对象的访问器。</returns>
    ISqlOutputParameterAccessor CreateSnapshot();
}

/// <summary>
/// 输出参数值快照。
/// </summary>
internal sealed class SqlOutputParameterSnapshot : ISqlOutputParameterAccessor
{
    /// <summary>
    /// 输出参数值。
    /// </summary>
    private readonly IReadOnlyDictionary<string, object> _values;

    /// <summary>
    /// 初始化输出参数值快照。
    /// </summary>
    /// <param name="values">已在本次执行完成后读取的输出值。</param>
    internal SqlOutputParameterSnapshot(IEnumerable<KeyValuePair<string, object>> values)
    {
        _values = (values ?? Array.Empty<KeyValuePair<string, object>>())
            .Where(item => string.IsNullOrWhiteSpace(item.Key) == false)
            .ToDictionary(item => NormalizeName(item.Key), item => CloneValue(item.Value), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 从输出参数访问器创建执行结果快照。
    /// </summary>
    /// <param name="accessor">框架参数绑定器创建的访问器。</param>
    /// <returns>独立的输出参数快照；没有输出参数时返回 null。</returns>
    /// <exception cref="NotSupportedException">访问器无法提供稳定快照时抛出。</exception>
    internal static ISqlOutputParameterAccessor Create(ISqlOutputParameterAccessor accessor)
    {
        if (accessor == null)
            return null;
        if (accessor is SqlOutputParameterSnapshot snapshot)
            return snapshot;
        if (accessor is ISqlOutputParameterSnapshotProvider provider)
            return provider.CreateSnapshot();
        throw new NotSupportedException($"输出参数访问器类型 {accessor.GetType().FullName} 不支持执行结果快照。");
    }

    /// <inheritdoc />
    public object GetValue(string name)
    {
        if (_values.TryGetValue(NormalizeName(name), out var value) == false)
            throw new KeyNotFoundException($"未找到输出参数 '{name}'。");
        return CloneValue(value);
    }

    /// <inheritdoc />
    public T GetValue<T>(string name)
    {
        if (_values.TryGetValue(NormalizeName(name), out var rawValue) == false)
            throw new KeyNotFoundException($"未找到输出参数 '{name}'。");
        if (rawValue == null)
        {
            if (SqlOutputParameterValueConverter.IsNullableType<T>())
                return default;
            throw new InvalidOperationException($"输出参数 '{name}' 的值为数据库 NULL，无法转换为非空类型 {typeof(T).FullName}。");
        }
        if (SqlOutputParameterValueConverter.TryConvert(rawValue, out T value))
            return value;
        throw new InvalidCastException($"输出参数 '{name}' 的来源类型 {rawValue.GetType().FullName} 无法转换为目标 CLR 类型 {typeof(T).FullName}。");
    }

    /// <inheritdoc />
    public bool TryGetValue<T>(string name, out T value)
    {
        if (_values.TryGetValue(NormalizeName(name), out var rawValue) == false)
        {
            value = default;
            return false;
        }
        if (rawValue == null)
        {
            value = default;
            return SqlOutputParameterValueConverter.IsNullableType<T>();
        }
        return SqlOutputParameterValueConverter.TryConvert(rawValue, out value);
    }

    /// <summary>
    /// 复制可变输出值。
    /// </summary>
    /// <typeparam name="T">值类型。</typeparam>
    /// <param name="value">原始值。</param>
    /// <returns>安全的值副本。</returns>
    private static T CloneValue<T>(T value) => value is Array array ? (T)array.Clone() : value;

    /// <summary>
    /// 规范化参数名称。
    /// </summary>
    /// <param name="name">参数名称。</param>
    /// <returns>不含 Provider 前缀的参数名称。</returns>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        name = name.Trim();
        return name[0] is '@' or ':' or '?' ? name.Substring(1) : name;
    }
}

/// <summary>
/// 输出参数值的统一转换器。
/// </summary>
/// <remarks>
/// 框架绑定器与执行结果快照均通过该组件转换，避免同步、异步和访问器实现出现不同的类型语义。
/// </remarks>
internal static class SqlOutputParameterValueConverter
{
    /// <summary>
    /// 尝试转换输出参数值。
    /// </summary>
    /// <typeparam name="T">目标 CLR 类型。</typeparam>
    /// <param name="rawValue">原始输出值。</param>
    /// <param name="value">转换结果。</param>
    /// <returns>转换成功时返回 true。</returns>
    internal static bool TryConvert<T>(object rawValue, out T value)
    {
        if (rawValue is T typedValue)
        {
            value = CloneValue(typedValue);
            return true;
        }
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        try
        {
            object convertedValue;
            if (targetType == typeof(Guid))
                convertedValue = rawValue is string text && Guid.TryParse(text, out var guid)
                    ? guid
                    : throw new InvalidCastException();
            else if (targetType == typeof(DateTimeOffset))
                convertedValue = rawValue is DateTime dateTime
                    ? new DateTimeOffset(dateTime)
                    : rawValue is string text && DateTimeOffset.TryParse(text, out var dateTimeOffset)
                        ? dateTimeOffset
                        : throw new InvalidCastException();
            else if (targetType == typeof(TimeSpan))
                convertedValue = rawValue is string text && TimeSpan.TryParse(text, out var timeSpan)
                    ? timeSpan
                    : throw new InvalidCastException();
            else if (targetType.IsEnum)
                convertedValue = rawValue is string enumName
                    ? Enum.Parse(targetType, enumName, ignoreCase: true)
                    : Enum.ToObject(targetType, rawValue);
            else
                convertedValue = Convert.ChangeType(rawValue, targetType);
            value = (T)convertedValue;
            return true;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or
                                          ArgumentException)
        {
            value = default;
            return false;
        }
    }

    /// <summary>
    /// 判断目标类型是否可表示数据库 NULL。
    /// </summary>
    /// <typeparam name="T">目标 CLR 类型。</typeparam>
    /// <returns>可表示 null 时返回 true。</returns>
    internal static bool IsNullableType<T>() => typeof(T).IsValueType == false ||
                                                Nullable.GetUnderlyingType(typeof(T)) != null;

    /// <summary>
    /// 复制可变输出值。
    /// </summary>
    /// <typeparam name="T">值类型。</typeparam>
    /// <param name="value">原始值。</param>
    /// <returns>安全的值副本。</returns>
    private static T CloneValue<T>(T value) => value is Array array ? (T)array.Clone() : value;
}