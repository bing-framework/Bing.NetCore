namespace Bing.Serialization;

/// <summary>
/// XML 序列化器
/// </summary>
public interface IXmlSerializer : IObjectSerializer, ITextSerializer
{
    /// <summary>
    /// 将XML字符串反序列化为对象
    /// </summary>
    /// <typeparam name="TValue">对象类型</typeparam>
    /// <param name="xml">xml字符串</param>
    /// <returns>如果 xml 为 null 或为空，将返回 TValue 的默认值</returns>
    TValue FromXml<TValue>(string xml);

    /// <summary>
    /// 将XML字符串反序列化为对象
    /// </summary>
    /// <param name="type">对象类型</param>
    /// <param name="xml">xml字符串</param>
    /// <returns>如果 xml 为 null 或为空，将返回 type 的默认值</returns>
    object FromXml(Type type, string xml);

    /// <summary>
    /// 序列化为XML字符串
    /// </summary>
    /// <typeparam name="TValue">序列化对象类型</typeparam>
    /// <param name="value">值</param>
    string ToXml<TValue>(TValue value);

    /// <summary>
    /// 序列化为XML字符串
    /// </summary>
    /// <param name="type">序列化对象类型</param>
    /// <param name="value">值</param>
    string ToXml(Type type, object value);
}
