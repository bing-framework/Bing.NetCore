using System;
using System.IO;

namespace Bing.Emailing;

/// <summary>
/// 附件
/// </summary>
public interface IAttachment : IDisposable
{
    /// <summary>
    /// 获取文件流
    /// </summary>
    Stream GetFileStream();

    /// <summary>
    /// 获取文件名称
    /// </summary>
    string GetName();
}