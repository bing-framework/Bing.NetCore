namespace Bing.Serialization;

/// <summary>
/// Toml序列化器配置入口
/// </summary>
public interface ITomlSerializerConfigureEntry
{
    /// <summary>
    /// 配置Toml序列化器
    /// </summary>
    /// <param name="serializer">Toml序列化器</param>
    void ConfigureTomlSerializer(ITomlSerializer serializer);

    /// <summary>
    /// 配置Toml序列化器
    /// </summary>
    /// <param name="serializerFactory">Toml序列化器工厂</param>
    void ConfigureTomlSerializer(Func<ITomlSerializer> serializerFactory);
}
