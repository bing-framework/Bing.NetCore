namespace Bing.Content;

/// <summary>
/// 定义可作为远程内容传输的流及其元数据。
/// </summary>
public interface IRemoteStreamContent : IDisposable
{
    /// <summary>
    /// 获取可选的内容文件名。
    /// </summary>
    /// <remarks>未提供文件名时为 <c>null</c>。</remarks>
    string FileName { get; }

    /// <summary>
    /// 获取内容的 MIME 类型。
    /// </summary>
    /// <remarks>默认值为 <c>application/octet-stream</c>。</remarks>
    string ContentType { get; }

    /// <summary>
    /// 获取可选的内容长度。
    /// </summary>
    /// <remarks>长度未知或流不支持定位时为 <c>null</c>。</remarks>
    long? ContentLength { get; }

    /// <summary>
    /// 获取表示内容的流。
    /// </summary>
    /// <returns>内容流。</returns>
    /// <remarks>流的位置和所有权由具体实现定义；调用方应在释放当前内容对象前完成读取。</remarks>
    Stream GetStream();
}
