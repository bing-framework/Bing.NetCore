namespace Bing.Content;

/// <summary>
/// 定义一个远程流内容的接口
/// </summary>
public interface IRemoteStreamContent : IDisposable
{
    /// <summary>
    /// 获取文件名。如果未提供文件名，则为 null。
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// 获取内容的MIME类型。默认为 "application/octet-stream"。
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// 获取内容的长度。如果无法确定长度，或者流不支持查找操作，则为 null。
    /// </summary>
    long? ContentLength { get; }

    /// <summary>
    /// 获取表示文件内容的流。
    /// </summary>
    /// <returns>文件内容的流。</returns>
    Stream GetStream();
}
