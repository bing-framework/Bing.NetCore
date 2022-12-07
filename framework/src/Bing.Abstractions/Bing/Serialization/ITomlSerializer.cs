namespace Bing.Serialization;

/// <summary>
/// Toml 序列化器
/// </summary>
public interface ITomlSerializer : IObjectSerializer, ITextSerializer
{
    /// <summary>
    /// 将Toml字符串反序列化为对象
    /// </summary>
    /// <typeparam name="TValue">对象类型</typeparam>
    /// <param name="toml">toml字符串</param>
    /// <returns>如果 toml 为 null 或为空，将返回 TValue 的默认值</returns>
    TValue FromToml<TValue>(string toml);

    /// <summary>
    /// 将Toml字符串反序列化为对象
    /// </summary>
    /// <param name="type">对象类型</param>
    /// <param name="toml">toml字符串</param>
    /// <returns>如果 toml 为 null 或为空，将返回 type 的默认值</returns>
    object FromToml(Type type, string toml);

    /// <summary>
    /// 序列化为Toml字符串
    /// </summary>
    /// <typeparam name="TValue">序列化对象类型</typeparam>
    /// <param name="value">值</param>
    string ToToml<TValue>(TValue value);

    /// <summary>
    /// 序列化为Toml字符串
    /// </summary>
    /// <param name="type">序列化对象类型</param>
    /// <param name="value">值</param>
    string ToToml(Type type, object value);
}
