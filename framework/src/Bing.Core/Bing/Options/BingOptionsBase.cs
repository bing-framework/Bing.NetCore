namespace Bing.Options;

/// <summary>
/// Bing配置项基类
/// </summary>
public abstract class BingOptionsBase : IBingOptions
{
    /// <summary>
    /// 初始化一个<see cref="BingOptionsBase"/>类型的实例
    /// </summary>
    protected BingOptionsBase()
    {
        Extensions = new List<IBingOptionsExtension>();
    }

    /// <summary>
    /// 配置项扩展列表
    /// </summary>
    internal IList<IBingOptionsExtension> Extensions { get; }

    /// <summary>
    /// 添加配置项扩展
    /// </summary>
    /// <param name="extension">配置项扩展</param>
    public void AddExtension(IBingOptionsExtension extension)
    {
        if (extension == null)
            throw new ArgumentNullException(nameof(extension));
        Extensions.Add(extension);
    }
}
