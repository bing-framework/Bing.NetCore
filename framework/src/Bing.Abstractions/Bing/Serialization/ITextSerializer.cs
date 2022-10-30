namespace Bing.Serialization;

/// <summary>
/// 文本对象序列化器元接口
/// </summary>
public interface ITextSerializer : IBytesSerializer
{
    /// <summary>
    /// 将字符串反序列化为对象
    /// </summary>
    /// <typeparam name="TValue">对象类型</typeparam>
    /// <param name="text">文本</param>
    /// <returns>如果 text 为 null 或为空，将返回 TValue 的默认值</returns>
    TValue FromText<TValue>(string text);

    /// <summary>
    /// 将字符串反序列化为对象
    /// </summary>
    /// <param name="type">对象类型</param>
    /// <param name="text">文本</param>
    /// <returns>如果 text 为 null 或为空，将返回 type 的默认值</returns>
    object FromText(Type type, string text);

    /// <summary>
    /// 序列化为字符串
    /// </summary>
    /// <typeparam name="TValue">序列化对象类型</typeparam>
    /// <param name="value">值</param>
    string ToText<TValue>(TValue value);

    /// <summary>
    /// 序列化为字符串
    /// </summary>
    /// <param name="type">序列化对象类型</param>
    /// <param name="value">值</param>
    string ToText(Type type, object value);
}
