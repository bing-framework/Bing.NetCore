namespace Bing;

/// <summary>
/// 定义枚举
/// </summary>
public abstract class Enumeration : IComparable
{
    /// <summary>
    /// 标识
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 初始化一个<see cref="Enumeration"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="name">名称</param>
    protected Enumeration(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// 输出字符串
    /// </summary>
    public override string ToString() => $"[{GetType().Name}] Id = {Id}, Name = {Name}";

    /// <summary>
    /// 是否相等
    /// </summary>
    /// <param name="other">其它对象</param>
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
    /// 获取哈希码
    /// </summary>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// 比较
    /// </summary>
    /// <param name="other">其它对象</param>
    public int CompareTo(object other) => string.Compare(Id, ((Enumeration)other).Id, StringComparison.Ordinal);

    /// <summary>
    /// 获取全部枚举值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public static IEnumerable<T> GetAll<T>() where T : Enumeration
    {
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        return fields.Select(f => f.GetValue(null)).Cast<T>();
    }

    /// <summary>
    /// 从值中解析<see cref="Enumeration"/>
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="value">值</param>
    public static T FromValue<T>(string value) where T : Enumeration
    {
        var matchingItem = Parse<T, string>(value, "value", item => item.Id == value);
        return matchingItem;
    }

    /// <summary>
    /// 从显示名称中解析<see cref="Enumeration"/>
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="displayName">显示名称</param>
    public static T FromDisplayName<T>(string displayName) where T : Enumeration
    {
        var matchingItem = Parse<T, string>(displayName, "display name", item => item.Name == displayName);
        return matchingItem;
    }

    /// <summary>
    /// 解析
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TK">键类型</typeparam>
    /// <param name="value">值</param>
    /// <param name="description">描述</param>
    /// <param name="predicate">条件</param>
    private static T Parse<T, TK>(TK value, string description, Func<T, bool> predicate) where T : Enumeration
    {
        var matchingItem = GetAll<T>().FirstOrDefault(predicate);
        if (matchingItem == null)
            throw new InvalidOperationException($"'{value}' is not a valid {description} in {typeof(T)}");
        return matchingItem;
    }
}
