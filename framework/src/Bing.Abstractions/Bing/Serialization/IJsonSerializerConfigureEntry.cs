namespace Bing.Serialization;

/// <summary>
/// Json 序列化器配置入口
/// </summary>
public interface IJsonSerializerConfigureEntry
{
    /// <summary>
    /// 配置Json序列化器
    /// </summary>
    /// <param name="serializer">Json序列化器</param>
    void ConfigureJsonSerializer(IJsonSerializer serializer);

    /// <summary>
    /// 配置Json序列化器
    /// </summary>
    /// <param name="serializerFactory">Json序列化器工厂</param>
    void ConfigureJsonSerializer(Func<IJsonSerializer> serializerFactory);
}
