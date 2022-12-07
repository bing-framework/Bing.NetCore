namespace Bing.Serialization;

/// <summary>
/// Yaml 序列化器配置入口
/// </summary>
public interface IYamlSerializerConfigureEntry
{
    /// <summary>
    /// 配置Yaml序列化器
    /// </summary>
    /// <param name="serializer">Yaml序列化器</param>
    void ConfigureYamlSerializer(IYamlSerializer serializer);

    /// <summary>
    /// 配置Yaml序列化器
    /// </summary>
    /// <param name="serializerFactory">Yaml序列化器工厂</param>
    void ConfigureYamlSerializer(Func<IYamlSerializer> serializerFactory);
}
