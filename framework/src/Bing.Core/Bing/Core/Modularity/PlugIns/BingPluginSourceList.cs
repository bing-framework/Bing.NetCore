using System.Collections.ObjectModel;
using System.Reflection;

namespace Bing.Core.Modularity;

/// <summary>
/// 管理应用使用的启动期插件来源。
/// </summary>
public sealed class BingPluginSourceList : Collection<IBingPluginSource>
{
    /// <summary>
    /// 添加一个插件目录来源。
    /// </summary>
    /// <param name="directory">包含 <c>bing-plugin.json</c> 的插件目录。</param>
    /// <returns>当前来源集合。</returns>
    public BingPluginSourceList AddDirectory(string directory)
    {
        Add(new DirectoryBingPluginSource(directory, false));
        return this;
    }

    /// <summary>
    /// 添加一个插件父目录来源。
    /// </summary>
    /// <param name="directory">包含插件子目录的父目录。</param>
    /// <returns>当前来源集合。</returns>
    public BingPluginSourceList AddFolder(string directory)
    {
        Add(new DirectoryBingPluginSource(directory, true));
        return this;
    }

    /// <inheritdoc />
    protected override void InsertItem(int index, IBingPluginSource item)
    {
        base.InsertItem(index, item ?? throw new ArgumentNullException(nameof(item)));
    }
}

/// <summary>
/// 使用文件系统目录发现插件的内置来源。
/// </summary>
internal sealed class DirectoryBingPluginSource : IBingPluginSource
{
    /// <summary>
    /// 插件目录或父目录路径。
    /// </summary>
    private readonly string _directory;

    /// <summary>
    /// 是否枚举一级子目录。
    /// </summary>
    private readonly bool _folder;

    /// <summary>
    /// 初始化一个 <see cref="DirectoryBingPluginSource" /> 类型的实例。
    /// </summary>
    /// <param name="directory">目录路径。</param>
    /// <param name="folder">是否把路径作为插件父目录。</param>
    public DirectoryBingPluginSource(string directory, bool folder)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _folder = folder;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetPluginDirectories()
    {
        if (_folder)
        {
            if (!Directory.Exists(_directory))
                throw new DirectoryNotFoundException("插件父目录不存在: " + _directory);
            return Directory.EnumerateDirectories(_directory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal);
        }

        return new[] { _directory };
    }
}
