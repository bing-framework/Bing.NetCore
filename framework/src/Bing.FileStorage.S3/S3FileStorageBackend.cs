using Amazon.S3;

namespace Bing.FileStorage;

/// <summary>持有命名 S3 客户端；绑定门面共享该客户端且不拥有其生命周期。</summary>
public sealed class S3FileStorageBackend : IFileStorageBackend, IDisposable
{
    private readonly string _storageNamespace;
    private readonly IAmazonS3 _client;
    private readonly bool _ownsClient;
    private readonly int _listPartsPageSize;
    private readonly IReadOnlyDictionary<string, string> _containerBuckets;
    private readonly S3UploadMode _uploadMode;

    /// <summary>绑定调用方拥有的 S3 客户端；此构造函数不会释放该客户端。</summary>
    public S3FileStorageBackend(string name, string storageNamespace, IAmazonS3 client)
        : this(name, storageNamespace, client, false, 1000)
    {
    }

    /// <summary>使用调用方拥有的客户端及配置快照；适用于显式 SDK 客户端装配。</summary>
    public S3FileStorageBackend(string name, BingS3StorageOptions options, IAmazonS3 client)
        : this(name, (options ?? throw new ArgumentNullException(nameof(options))).Snapshot(), client, false)
    {
    }

    private S3FileStorageBackend(string name, BingS3StorageOptions options, IAmazonS3 client, bool ownsClient)
        : this(name, options.StorageNamespace, client, ownsClient, options.ListPartsPageSize, options.ContainerBuckets, options.UploadMode)
    {
    }

    internal S3FileStorageBackend(string name, string storageNamespace, IAmazonS3 client, bool ownsClient, int listPartsPageSize,
        IReadOnlyDictionary<string, string>? containerBuckets = null, S3UploadMode uploadMode = S3UploadMode.SignedParts)
    {
        Name = FileStorageNames.ValidateIdentifier(name, nameof(name));
        _storageNamespace = FileStorageNames.ValidateIdentifier(storageNamespace, nameof(storageNamespace));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
        if (listPartsPageSize is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(listPartsPageSize));
        _listPartsPageSize = listPartsPageSize;
        _containerBuckets = containerBuckets?.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        _uploadMode = uploadMode;
    }

    /// <summary>命名存储实例名称。</summary>
    public string Name { get; }

    /// <summary>绑定一个明确范围的轻量存储门面。</summary>
    public IFileStorage Bind(StorageScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new S3FileStorage(Name, _storageNamespace, scope, _client, _listPartsPageSize, _containerBuckets, _uploadMode);
    }

    /// <summary>释放由此 Provider 创建并拥有的 SDK 客户端。</summary>
    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }
}
