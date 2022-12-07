namespace Bing.Serialization;

/// <summary>
/// Json 序列化器
/// </summary>
public interface IJsonSerializer : IObjectSerializer, ITextSerializer
{
    /// <summary>
    /// 将Json字符串反序列化为对象
    /// </summary>
    /// <typeparam name="TValue">对象类型</typeparam>
    /// <param name="json">json字符串</param>
    /// <returns>如果 json 为 null 或为空，将返回 TValue 的默认值</returns>
    TValue FromJson<TValue>(string json);

    /// <summary>
    /// 将Json字符串反序列化为对象
    /// </summary>
    /// <param name="type">对象类型</param>
    /// <param name="json">json字符串</param>
    /// <returns>如果 json 为 null 或为空，将返回 type 的默认值</returns>
    object FromJson(Type type, string json);

    /// <summary>
    /// 序列化为Json字符串
    /// </summary>
    /// <typeparam name="TValue">序列化对象类型</typeparam>
    /// <param name="value">值</param>
    string ToJson<TValue>(TValue value);

    /// <summary>
    /// 序列化为Json字符串
    /// </summary>
    /// <param name="type">序列化对象类型</param>
    /// <param name="value">值</param>
    string ToJson(Type type, object value);
}
