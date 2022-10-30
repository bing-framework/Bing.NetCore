namespace Bing.Serialization;

/// <summary>
/// Yaml 序列化器
/// </summary>
public interface IYamlSerializer : IObjectSerializer, ITextSerializer
{
    /// <summary>
    /// 将YAML字符串反序列化为对象
    /// </summary>
    /// <typeparam name="TValue">对象类型</typeparam>
    /// <param name="yaml">yaml字符串</param>
    /// <returns>如果 yaml 为 null 或为空，将返回 TValue 的默认值</returns>
    TValue FromYaml<TValue>(string yaml);

    /// <summary>
    /// 将YAML字符串反序列化为对象
    /// </summary>
    /// <param name="type">对象类型</param>
    /// <param name="yaml">yaml字符串</param>
    /// <returns>如果 yaml 为 null 或为空，将返回 type 的默认值</returns>
    object FromYaml(Type type, string yaml);

    /// <summary>
    /// 序列化为YAML字符串
    /// </summary>
    /// <typeparam name="TValue">序列化对象类型</typeparam>
    /// <param name="value">值</param>
    string ToYaml<TValue>(TValue value);

    /// <summary>
    /// 序列化为YAML字符串
    /// </summary>
    /// <param name="type">序列化对象类型</param>
    /// <param name="value">值</param>
    string ToYaml(Type type, object value);
}
