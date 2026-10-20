namespace Bing.Emailing.Attachments;

/// <summary>
/// 使用调用方提供的内存流作为邮件附件的实现。
/// </summary>
public class MemoryStreamAttachment : IAttachment
{
    /// <summary>
    /// 保存附件内容的内存流。
    /// </summary>
    private readonly MemoryStream _stream;

    /// <summary>
    /// 保存邮件中显示的附件文件名。
    /// </summary>
    private readonly string _fileName;

    /// <summary>
    /// 初始化一个 <see cref="MemoryStreamAttachment"/> 类型的实例。
    /// </summary>
    /// <param name="stream">作为附件内容的内存流。</param>
    /// <param name="fileName">邮件中显示的附件文件名。</param>
    public MemoryStreamAttachment(MemoryStream stream, string fileName)
    {
        _stream = stream;
        _fileName = fileName;
    }

    /// <inheritdoc />
    public void Dispose() => _stream.Dispose();

    /// <inheritdoc />
    /// <remarks>返回构造时提供的同一内存流，流的生命周期由当前附件实例管理。</remarks>
    public Stream GetFileStream() => _stream;

    /// <inheritdoc />
    public string GetName() => _fileName;
}