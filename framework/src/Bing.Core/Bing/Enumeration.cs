namespace Bing;

/// <summary>
/// 提供具有字符串标识和显示名称的类型化枚举基类。
/// </summary>
public abstract class Enumeration : IComparable
{
    /// <summary>
    /// 获取枚举项的唯一字符串标识。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 获取枚举项面向显示或描述用途的名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 使用标识和名称初始化 <see cref="Enumeration"/> 的实例。
    /// </summary>
    /// <param name="id">枚举项的唯一字符串标识。</param>
    /// <param name="name">枚举项的显示名称。</param>
    protected Enumeration(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// 返回包含类型、标识和名称的枚举项文本表示。
    /// </summary>
    /// <returns>包含类型、标识和名称的文本表示。</returns>
    public override string ToString() => $"[{GetType().Name}] Id = {Id}, Name = {Name}";

    /// <summary>
    /// 判断当前枚举项是否与指定对象具有相同的具体类型和标识。
    /// </summary>
    /// <param name="other">要比较的对象。</param>
    /// <returns>对象为相同具体类型且标识相等时返回 <see langword="true"/>。</returns>
    public override bool Equals(object other)
    {
        var otherValue = other as Enumeration;
        if (otherValue == null)
            return false;
        var typeMatches = GetType() == other.GetType();
        var valueMatches = Id.Equals(otherValue.Id);
        return typeMatches && valueMatches;
    }

    /// <summary>
    /// 返回基于枚举项标识的哈希代码。
    /// </summary>
    /// <returns>基于枚举项标识计算的哈希代码。</returns>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// 按字符串标识顺序比较当前枚举项与指定对象。
    /// </summary>
    /// <param name="other">要比较的枚举项。</param>
    /// <returns>当前标识与指定标识的序关系。</returns>
    public int CompareTo(object other) => string.Compare(Id, ((Enumeration)other).Id, StringComparison.Ordinal);

    /// <summary>
    /// 获取指定可扩展枚举类型声明的全部静态枚举项。
    /// </summary>
    /// <typeparam name="T">继承 <see cref="Enumeration"/> 的具体枚举类型。</typeparam>
    /// <returns>按反射字段顺序返回的枚举项集合。</returns>
    public static IEnumerable<T> GetAll<T>() where T : Enumeration
    {
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        return fields.Select(f => f.GetValue(null)).Cast<T>();
    }

    /// <summary>
    /// 按唯一标识解析指定的可扩展枚举项。
    /// </summary>
    /// <typeparam name="T">继承 <see cref="Enumeration"/> 的具体枚举类型。</typeparam>
    /// <param name="value">要匹配的枚举项标识。</param>
    /// <returns>标识匹配的枚举项。</returns>
    /// <exception cref="InvalidOperationException">不存在匹配的枚举项时抛出。</exception>
    public static T FromValue<T>(string value) where T : Enumeration
    {
        var matchingItem = Parse<T, string>(value, "value", item => item.Id == value);
        return matchingItem;
    }

    /// <summary>
    /// 按显示名称解析指定的可扩展枚举项。
    /// </summary>
    /// <typeparam name="T">继承 <see cref="Enumeration"/> 的具体枚举类型。</typeparam>
    /// <param name="displayName">要匹配的显示名称。</param>
    /// <returns>显示名称匹配的枚举项。</returns>
    /// <exception cref="InvalidOperationException">不存在匹配的枚举项时抛出。</exception>
    public static T FromDisplayName<T>(string displayName) where T : Enumeration
    {
        var matchingItem = Parse<T, string>(displayName, "display name", item => item.Name == displayName);
        return matchingItem;
    }

    /// <summary>
    /// 使用指定条件从可扩展枚举项集合中查找匹配项。
    /// </summary>
    /// <typeparam name="T">继承 <see cref="Enumeration"/> 的具体枚举类型。</typeparam>
    /// <typeparam name="TK">待解析值的类型。</typeparam>
    /// <param name="value">待解析的值。</param>
    /// <param name="description">值的描述文本，用于构造异常消息。</param>
    /// <param name="predicate">判断枚举项是否匹配的条件。</param>
    /// <returns>满足条件的枚举项。</returns>
    /// <exception cref="InvalidOperationException">不存在满足条件的枚举项时抛出。</exception>
    private static T Parse<T, TK>(TK value, string description, Func<T, bool> predicate) where T : Enumeration
    {
        var matchingItem = GetAll<T>().FirstOrDefault(predicate);
        if (matchingItem == null)
            throw new InvalidOperationException($"'{value}' is not a valid {description} in {typeof(T)}");
        return matchingItem;
    }
}
