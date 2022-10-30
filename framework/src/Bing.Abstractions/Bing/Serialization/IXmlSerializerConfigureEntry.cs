namespace Bing.Serialization;

/// <summary>
/// Xml 序列化器配置入口
/// </summary>
public interface IXmlSerializerConfigureEntry
{
    /// <summary>
    /// 配置Xml序列化器
    /// </summary>
    /// <param name="serializer">Xml序列化器</param>
    void ConfigureXmlSerializer(IXmlSerializer serializer);

    /// <summary>
    /// 配置Xml序列化器
    /// </summary>
    /// <param name="serializerFactory">Xml序列化器工厂</param>
    void ConfigureXmlSerializer(Func<IXmlSerializer> serializerFactory);
}
