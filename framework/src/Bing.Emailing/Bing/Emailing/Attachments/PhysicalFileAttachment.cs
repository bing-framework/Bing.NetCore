namespace Bing.Emailing.Attachments;

/// <summary>
/// 以本地物理文件作为邮件附件的实现。
/// </summary>
public class PhysicalFileAttachment : IAttachment
{
    /// <summary>
    /// 缓存首次打开的物理文件读取流。
    /// </summary>
    private FileStream _stream;

    /// <summary>
    /// 获取附件文件的绝对路径。
    /// </summary>
    public string AbsolutePath { get; }

    /// <summary>
    /// 使用已存在的物理文件路径初始化 <see cref="PhysicalFileAttachment"/> 的实例。
    /// </summary>
    /// <param name="absolutePath">附件文件的绝对路径。</param>
    /// <exception cref="FileNotFoundException">指定路径不存在文件时抛出。</exception>
    public PhysicalFileAttachment(string absolutePath)
    {
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException($"文件未找到：{absolutePath}");
        AbsolutePath = absolutePath;
    }

    /// <summary>
    /// 释放缓存的物理文件读取流。
    /// </summary>
    public void Dispose() => _stream?.Dispose();

    /// <inheritdoc />
    /// <remarks>返回流由当前附件实例管理，使用完成后必须释放附件实例。</remarks>
    public Stream GetFileStream() => _stream ??= new FileStream(AbsolutePath, FileMode.Open);

    /// <inheritdoc />
    public string GetName() => Path.GetFileName(AbsolutePath);
}