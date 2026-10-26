using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage.FileSystem;

/// <summary>单节点持久存储；RootPath 必须在重启后保留，且不能由非受信任用户改写目录结构。</summary>
public sealed class FileSystemStorageOptions
{
    public string RootPath { get; set; } = "";
    public string Namespace { get; set; } = "bing";
}

public static class FileSystemStorageRegistration
{
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

/// <summary>配置快照；不持有业务用户、租户或打开的文件流。</summary>
public sealed class FileSystemStorageBackend : IFileStorageBackend
{
    private readonly string _root;
    private readonly string _space;
    public FileSystemStorageBackend(string name, string root, string space = "bing")
    {
        Name = FileStorageNames.ValidateIdentifier(name, nameof(name));
        if (!Path.IsPathFullyQualified(root)) throw new ArgumentException("需要绝对路径。", nameof(root));
        _root = Path.GetFullPath(root);
        _space = FileStorageNames.ValidateIdentifier(space, nameof(space));
        LocalPaths.CheckLinks(_root);
        Directory.CreateDirectory(_root);
    }
    public string Name { get; }
    public IFileStorage Bind(StorageScope scope) => new FileSystemStorage(Name, scope, _root, _space);
}
