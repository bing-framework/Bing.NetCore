using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage;

/// <summary>
/// 标识 S3 分片校验和传输所采用的模式。
/// </summary>
/// <remarks>
/// 模式在后端实例投入使用后固定，不能改变已有会话的处理方式。
/// </remarks>
public enum S3UploadMode
{
    /// <summary>
    /// 由客户端通过签名请求直接上传 S3 分片，并校验服务端回执。
    /// </summary>
    SignedParts,
    /// <summary>
    /// 由服务器接收分片内容并代理上传到 S3。
    /// </summary>
    ServerProxy
}

/// <summary>
/// S3 兼容存储客户端配置。
/// </summary>
/// <remarks>密钥可留空以使用 AWS 默认凭据链。</remarks>
public sealed class BingS3StorageOptions
{
    /// <summary>
    /// 获取或设置可选的 S3 服务地址。
    /// </summary>
    /// <remarks>MinIO 通常使用 http://host:9000。</remarks>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// 获取或设置AWS 签名区域。
    /// </summary>
    /// <remarks>MinIO 可使用 us-east-1。默认值为 <c>&quot;us-east-1&quot;</c>。</remarks>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// 获取或设置可选访问密钥。
    /// </summary>
    /// <remarks>配置时必须同时提供 SecretKey。</remarks>
    public string? AccessKey { get; set; }

    /// <summary>
    /// 获取或设置可选私密密钥。
    /// </summary>
    /// <remarks>配置时必须同时提供 AccessKey。</remarks>
    public string? SecretKey { get; set; }

    /// <summary>
    /// 获取或设置用于在对象键中区分此应用的数据前缀。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;bing&quot;</c>。</remarks>
    public string StorageNamespace { get; set; } = "bing";

    /// <summary>
    /// 获取或设置兼容 MinIO 与多数 S3 兼容服务的路径式寻址。
    /// </summary>
    /// <remarks>默认值为 true。</remarks>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// 获取或设置每次查询服务端分片清单的最大数量，范围为 1 至 1000。
    /// </summary>
    /// <remarks>默认值为 1000。</remarks>
    public int ListPartsPageSize { get; set; } = 1000;

    /// <summary>
    /// 获取或设置逻辑容器到实际桶的映射。
    /// </summary>
    /// <remarks>为空时保持原有同名桶行为，有映射时拒绝未登记容器。</remarks>
    public Dictionary<string, string> ContainerBuckets { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 获取或设置默认使用远端 SHA-256 校验的直传。
    /// </summary>
    /// <remarks>代理模式保留载荷签名并使用服务端回执。</remarks>
    public S3UploadMode UploadMode { get; set; } = S3UploadMode.SignedParts;

    /// <summary>
    /// 复制并验证 S3 存储选项。
    /// </summary>
    /// <returns>包含已验证配置的新选项实例。</returns>
    /// <remarks>
    /// 验证命名空间、上传模式、容器映射、区域、分页大小、访问凭据和服务地址。
    /// </remarks>
    internal BingS3StorageOptions Snapshot()
    {
        var copy = new BingS3StorageOptions
        {
            ServiceUrl = ServiceUrl,
            Region = Region,
            AccessKey = AccessKey,
            SecretKey = SecretKey,
            StorageNamespace = StorageNamespace,
            ForcePathStyle = ForcePathStyle,
            ListPartsPageSize = ListPartsPageSize,
            UploadMode = UploadMode,
            ContainerBuckets = new Dictionary<string, string>(ContainerBuckets ?? throw new ArgumentNullException(nameof(ContainerBuckets)), StringComparer.Ordinal)
        };

        FileStorageNames.ValidateIdentifier(copy.StorageNamespace, nameof(StorageNamespace));
        if (!Enum.IsDefined(typeof(S3UploadMode), copy.UploadMode)) throw new ArgumentOutOfRangeException(nameof(UploadMode));
        foreach (var pair in copy.ContainerBuckets)
        {
            FileStorageNames.ValidateIdentifier(pair.Key, nameof(ContainerBuckets));
            S3FileStorage.ValidateBucket(pair.Value);
        }
        if (string.IsNullOrWhiteSpace(copy.Region))
            throw new ArgumentException("S3 区域不能为空。", nameof(Region));
        if (copy.ListPartsPageSize is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(ListPartsPageSize), "分片查询分页大小须为 1 至 1000。");
        if (string.IsNullOrWhiteSpace(copy.AccessKey) != string.IsNullOrWhiteSpace(copy.SecretKey))
            throw new ArgumentException("AccessKey 与 SecretKey 必须同时配置。");
        if (copy.ServiceUrl != null &&
            (!Uri.TryCreate(copy.ServiceUrl, UriKind.Absolute, out var endpoint) ||
             (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)))
            throw new ArgumentException("S3 服务地址必须是绝对 HTTP 或 HTTPS 地址。", nameof(ServiceUrl));

        return copy;
    }
}

/// <summary>
/// S3 兼容文件存储注册扩展。
/// </summary>
/// <remarks>
/// S3 兼容对象存储的命名 Provider 注册入口。
/// </remarks>
public static class BingS3StorageExtensions
{
    /// <summary>
    /// 配置并注册 S3 文件存储后端。
    /// </summary>
    /// <param name="services">要添加后端的依赖注入服务集合。</param>
    /// <param name="name">后端名称。</param>
    /// <param name="configure">配置 S3 存储选项的委托。</param>
    /// <returns>原依赖注入服务集合。</returns>
    /// <remarks>
    /// 配置完成后复制并验证选项，再注册后端工厂。
    /// </remarks>
    public static IServiceCollection AddBingS3Storage(
        this IServiceCollection services,
        string name,
        Action<BingS3StorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        FileStorageNames.ValidateIdentifier(name, nameof(name));

        var options = new BingS3StorageOptions();
        configure(options);
        var snapshot = options.Snapshot();

        services.AddBingFileStorageBackend(name, _ => CreateBackend(name, snapshot));
        return services;
    }

    /// <summary>
    /// 根据验证后的选项创建 S3 文件存储后端。
    /// </summary>
    /// <param name="name">文件存储后端名称。</param>
    /// <param name="options">已验证的 S3 存储选项。</param>
    /// <returns>拥有所创建 S3 客户端的文件存储后端。</returns>
    /// <remarks>
    /// 未配置访问密钥时使用 SDK 默认凭据链。
    /// </remarks>
    internal static IFileStorageBackend CreateBackend(string name, BingS3StorageOptions options)
    {
        var config = new AmazonS3Config
        {
            AuthenticationRegion = options.Region,
            ForcePathStyle = options.ForcePathStyle
        };
        if (options.UploadMode == S3UploadMode.ServerProxy)
        {
            // 避免 SDK 自动添加兼容服务未声明支持的扩展 checksum；请求仍使用 SigV4 载荷签名。
            config.RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED;
        }

        if (options.ServiceUrl != null)
        {
            config.ServiceURL = options.ServiceUrl;
            config.UseHttp = new Uri(options.ServiceUrl).Scheme == Uri.UriSchemeHttp;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        IAmazonS3 client = options.AccessKey == null
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey!), config);

        return new S3FileStorageBackend(name, options.StorageNamespace, client, true, options.ListPartsPageSize,
            options.ContainerBuckets, options.UploadMode);
    }
}
