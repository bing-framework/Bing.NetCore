using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage;

/// <summary>七牛 S3 存储配置；实际空间保持私有，密钥只在服务端使用。</summary>
public sealed class BingQiniuStorageOptions
{
    /// <summary>区域对应的 HTTPS S3 服务地址，例如 https://s3.cn-east-1.qiniucs.com。</summary>
    public string Endpoint { get; set; } = "";
    /// <summary>S3 区域标识；不能使用原生 Kodo 区域简称代替。</summary>
    public string Region { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string StorageNamespace { get; set; } = "bing";
    public bool ForcePathStyle { get; set; } = true;
    public int ListPartsPageSize { get; set; } = 1000;
    /// <summary>每个允许使用的逻辑容器均须映射到控制台显示的 S3 空间名。</summary>
    public Dictionary<string, string> ContainerBuckets { get; set; } = new(StringComparer.Ordinal);
    /// <summary>默认代理上传；仅在目标空间通过强校验直传验证后显式选择 SignedParts。</summary>
    public S3UploadMode UploadMode { get; set; } = S3UploadMode.ServerProxy;
}

/// <summary>通过现有 S3 Provider 注册七牛命名存储实例。</summary>
public static class BingQiniuStorageExtensions
{
    public static IServiceCollection AddBingQiniuStorage(this IServiceCollection services, string name,
        Action<BingQiniuStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new BingQiniuStorageOptions();
        configure(options);
        if (string.IsNullOrWhiteSpace(options.Region) ||
            !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps || !endpoint.IsDefaultPort ||
            endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            endpoint.AbsolutePath != "/" ||
            !string.Equals(endpoint.Host, "s3." + options.Region + ".qiniucs.com", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("七牛 Endpoint 必须是与 Region 对应的 HTTPS S3 服务域名。");
        if (string.IsNullOrWhiteSpace(options.AccessKey) || string.IsNullOrWhiteSpace(options.SecretKey))
            throw new ArgumentException("七牛存储必须显式提供 AccessKey 和 SecretKey。");
        if (options.ContainerBuckets == null || options.ContainerBuckets.Count == 0)
            throw new ArgumentException("七牛存储必须显式配置逻辑容器到 S3 空间名的映射。");

        return services.AddBingS3Storage(name, s3 =>
        {
            s3.ServiceUrl = endpoint.AbsoluteUri;
            s3.Region = options.Region;
            s3.AccessKey = options.AccessKey;
            s3.SecretKey = options.SecretKey;
            s3.StorageNamespace = options.StorageNamespace;
            s3.ForcePathStyle = options.ForcePathStyle;
            s3.ListPartsPageSize = options.ListPartsPageSize;
            s3.ContainerBuckets = options.ContainerBuckets;
            s3.UploadMode = options.UploadMode;
        });
    }
}
