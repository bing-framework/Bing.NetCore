namespace Bing.Options;

/// <summary>
/// Bing 配置项的抽象基类。
/// </summary>
public abstract class BingOptionsBase : IBingOptions
{
    /// <summary>
    /// 初始化一个 <see cref="BingOptionsBase"/> 类型的实例。
    /// </summary>
    protected BingOptionsBase()
    {
        Extensions = new List<IBingOptionsExtension>();
    }

    /// <summary>
    /// 获取配置项扩展列表。
    /// </summary>
    internal IList<IBingOptionsExtension> Extensions { get; }

    /// <inheritdoc />
    public void AddExtension(IBingOptionsExtension extension)
    {
        if (extension == null)
            throw new ArgumentNullException(nameof(extension));
        Extensions.Add(extension);
    }
}
