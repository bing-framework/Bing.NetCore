using Amazon.S3;

namespace Bing.FileStorage;

/// <summary>
/// 持有命名 S3 客户端的存储后端。
/// </summary>
/// <remarks>
/// 持有命名 S3 客户端；绑定门面共享该客户端且不拥有其生命周期。
/// </remarks>
public sealed class S3FileStorageBackend : IFileStorageBackend, IDisposable
{
    /// <summary>
    /// 保存 S3 后端使用的命名空间前缀。
    /// </summary>
    private readonly string _storageNamespace;
    /// <summary>
    /// 保存 S3 客户端实例。
    /// </summary>
    private readonly IAmazonS3 _client;
    /// <summary>
    /// 指示后端是否拥有并可释放 S3 客户端。
    /// </summary>
    private readonly bool _ownsClient;
    /// <summary>
    /// 保存列举分片时使用的分页大小。
    /// </summary>
    private readonly int _listPartsPageSize;
    /// <summary>
    /// 保存容器名称到 S3 存储桶的映射。
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> _containerBuckets;
    /// <summary>
    /// 保存 S3 后端上传使用的模式。
    /// </summary>
    private readonly S3UploadMode _uploadMode;

    /// <summary>
    /// 初始化一个 <see cref="S3FileStorageBackend" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="storageNamespace">文件对象使用的逻辑存储命名空间。</param>
    /// <param name="client">用于访问对应后端或服务的客户端。</param>
    /// <remarks>绑定调用方拥有的 S3 客户端；此构造函数不会释放该客户端。</remarks>
    public S3FileStorageBackend(string name, string storageNamespace, IAmazonS3 client)
        : this(name, storageNamespace, client, false, 1000)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="S3FileStorageBackend" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="client">用于访问对应后端或服务的客户端。</param>
    /// <remarks>使用调用方拥有的客户端及配置快照；适用于显式 SDK 客户端装配。</remarks>
    public S3FileStorageBackend(string name, BingS3StorageOptions options, IAmazonS3 client)
        : this(name, (options ?? throw new ArgumentNullException(nameof(options))).Snapshot(), client, false)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="S3FileStorageBackend" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="client">用于访问对应后端或服务的客户端。</param>
    /// <param name="ownsClient">指示当前实例是否拥有客户端的释放责任。</param>
    private S3FileStorageBackend(string name, BingS3StorageOptions options, IAmazonS3 client, bool ownsClient)
        : this(name, options.StorageNamespace, client, ownsClient, options.ListPartsPageSize, options.ContainerBuckets, options.UploadMode)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="S3FileStorageBackend" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="storageNamespace">文件对象使用的逻辑存储命名空间。</param>
    /// <param name="client">用于访问对应后端或服务的客户端。</param>
    /// <param name="ownsClient">指示当前实例是否拥有客户端的释放责任。</param>
    /// <param name="listPartsPageSize">列出分片时每页读取的分片数量。</param>
    /// <param name="containerBuckets">逻辑容器名称到后端存储桶名称的映射。</param>
    /// <param name="uploadMode">文件上传所采用的模式。</param>
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

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    /// <remarks>
    /// <para>绑定一个明确范围的轻量存储门面。</para>
    /// </remarks>
    public IFileStorage Bind(StorageScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new S3FileStorage(Name, _storageNamespace, scope, _client, _listPartsPageSize, _containerBuckets, _uploadMode);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>释放由此 Provider 创建并拥有的 SDK 客户端。</para>
    /// </remarks>
    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }
}
