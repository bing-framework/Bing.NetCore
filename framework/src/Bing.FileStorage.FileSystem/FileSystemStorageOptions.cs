using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage.FileSystem;

/// <summary>
/// 本地文件存储后端配置。
/// </summary>
/// <remarks>单节点持久存储；RootPath 必须在重启后保留，且不能由非受信任用户改写目录结构。</remarks>
public sealed class FileSystemStorageOptions
{
    /// <summary>
    /// 获取或设置持久化文件存储根目录。
    /// </summary>
    /// <remarks>默认值为空字符串。</remarks>
    public string RootPath { get; set; } = "";
    /// <summary>
    /// 获取或设置应用文件的命名空间。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;bing&quot;</c>。</remarks>
    public string Namespace { get; set; } = "bing";
}

/// <summary>
/// 提供本地文件系统存储后端的依赖注入扩展。
/// </summary>
public static class FileSystemStorageRegistration
{
    /// <summary>
    /// 配置并注册本地文件系统存储后端。
    /// </summary>
    /// <param name="services">要添加后端的依赖注入服务集合。</param>
    /// <param name="name">后端名称。</param>
    /// <param name="configure">配置文件系统存储选项的委托。</param>
    /// <returns>原依赖注入服务集合。</returns>
    /// <remarks>
    /// 根目录必须是非空绝对路径；命名空间和后端名称必须有效。
    /// </remarks>
    public static IServiceCollection AddBingFileSystemStorage(this IServiceCollection services, string name, Action<FileSystemStorageOptions> configure)
    {
        var options = new FileSystemStorageOptions();
        configure(options);
        if (string.IsNullOrWhiteSpace(options.RootPath) || !Path.IsPathFullyQualified(options.RootPath))
            throw new ArgumentException("本地存储必须使用绝对持久目录。", nameof(configure));
        var root = Path.GetFullPath(options.RootPath);
        var space = FileStorageNames.ValidateIdentifier(options.Namespace, nameof(options.Namespace));
        return services.AddBingFileStorageBackend(name, _ => new FileSystemStorageBackend(name, root, space));
    }
}

/// <summary>
/// 使用配置快照绑定范围的本地存储后端。
/// </summary>
/// <remarks>
/// 配置快照；不持有业务用户、租户或打开的文件流。
/// </remarks>
public sealed class FileSystemStorageBackend : IFileStorageBackend
{
    /// <summary>
    /// 保存文件系统后端的根目录路径。
    /// </summary>
    private readonly string _root;
    /// <summary>
    /// 保存文件系统后端使用的存储空间名称。
    /// </summary>
    private readonly string _space;
    /// <summary>
    /// 初始化一个 <see cref="FileSystemStorageBackend" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="root">当前对象图、工作单元或文件系统的根对象或根路径。</param>
    /// <param name="space">用于隔离文件系统存储数据的空间名称。</param>
    public FileSystemStorageBackend(string name, string root, string space = "bing")
    {
        Name = FileStorageNames.ValidateIdentifier(name, nameof(name));
        if (!Path.IsPathFullyQualified(root)) throw new ArgumentException("需要绝对路径。", nameof(root));
        _root = Path.GetFullPath(root);
        _space = FileStorageNames.ValidateIdentifier(space, nameof(space));
        LocalPaths.CheckLinks(_root);
        Directory.CreateDirectory(_root);
    }
    /// <inheritdoc />
    public string Name { get; }
    /// <inheritdoc />
    public IFileStorage Bind(StorageScope scope) => new FileSystemStorage(Name, scope, _root, _space);
}
