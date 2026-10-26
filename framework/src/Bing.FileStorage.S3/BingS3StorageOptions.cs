using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage;

/// <summary>S3 分片校验方式；实例投入使用后不能切换已有会话的模式。</summary>
public enum S3UploadMode { SignedParts, ServerProxy }

/// <summary>S3 兼容存储客户端配置。密钥可留空以使用 AWS 默认凭据链。</summary>
public sealed class BingS3StorageOptions
{
    /// <summary>可选的 S3 服务地址；MinIO 通常使用 http://host:9000。</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>AWS 签名区域；MinIO 可使用 us-east-1。</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>可选访问密钥；配置时必须同时提供 SecretKey。</summary>
    public string? AccessKey { get; set; }

    /// <summary>可选私密密钥；配置时必须同时提供 AccessKey。</summary>
    public string? SecretKey { get; set; }

    /// <summary>用于在对象键中区分此应用的数据前缀。</summary>
    public string StorageNamespace { get; set; } = "bing";

    /// <summary>兼容 MinIO 与多数 S3 兼容服务的路径式寻址。</summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>每次查询服务端分片清单的最大数量，范围为 1 至 1000。</summary>
    public int ListPartsPageSize { get; set; } = 1000;

    /// <summary>逻辑容器到实际桶的映射；为空时保持原有同名桶行为，有映射时拒绝未登记容器。</summary>
    public Dictionary<string, string> ContainerBuckets { get; set; } = new(StringComparer.Ordinal);

    /// <summary>默认使用远端 SHA-256 校验的直传；代理模式保留载荷签名并使用服务端回执。</summary>
    public S3UploadMode UploadMode { get; set; } = S3UploadMode.SignedParts;

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

/// <summary>S3 兼容对象存储的命名 Provider 注册入口。</summary>
public static class BingS3StorageExtensions
{
    /// <summary>注册一个共享 S3 客户端和按范围绑定的存储实例。</summary>
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
