namespace Bing.Serialization;

/// <summary>
/// Bssom 序列化器
/// </summary>
public interface IBssomSerializer : IObjectSerializer<byte[]>, IBytesSerializer, IStreamSerializerAsync
{
}
