namespace Bing.Emailing;

/// <summary>
/// 定义可附加到电子邮件且需释放资源的附件。
/// </summary>
public interface IAttachment : IDisposable
{
    /// <summary>
    /// 获取用于创建邮件附件的内容流。
    /// </summary>
    /// <returns>附件内容流；其生命周期由附件实例管理。</returns>
    Stream GetFileStream();

    /// <summary>
    /// 获取邮件中显示的附件文件名。
    /// </summary>
    /// <returns>不包含目录信息的附件文件名。</returns>
    string GetName();
}