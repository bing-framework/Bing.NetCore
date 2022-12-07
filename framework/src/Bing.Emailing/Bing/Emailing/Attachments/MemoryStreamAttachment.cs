using System.IO;

namespace Bing.Emailing.Attachments;

/// <summary>
/// 内存流附件
/// </summary>
public class MemoryStreamAttachment : IAttachment
{
    /// <summary>
    /// 内存流
    /// </summary>
    private readonly MemoryStream _stream;

    /// <summary>
    /// 文件名
    /// </summary>
    private readonly string _fileName;

    /// <summary>
    /// 初始化一个<see cref="MemoryStreamAttachment"/>类型的实例
    /// </summary>
    /// <param name="stream">内存流</param>
    /// <param name="fileName">文件名</param>
    public MemoryStreamAttachment(MemoryStream stream, string fileName)
    {
        _stream = stream;
        _fileName = fileName;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose() => _stream.Dispose();

    /// <summary>
    /// 获取文件流
    /// </summary>
    public Stream GetFileStream() => _stream;

    /// <summary>
    /// 获取文件名称
    /// </summary>
    public string GetName() => _fileName;
}