using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage;

/// <summary>
/// 七牛 S3 存储配置。
/// </summary>
/// <remarks>实际空间保持私有，密钥只在服务端使用。</remarks>
public sealed class BingQiniuStorageOptions
{
    /// <summary>
    /// 获取或设置区域对应的 HTTPS S3 服务地址。
    /// </summary>
    /// <remarks>
    /// <para>默认值为空字符串。</para>
    /// <para>例如 https://s3.cn-east-1.qiniucs.com。</para>
    /// </remarks>
    public string Endpoint { get; set; } = "";
    /// <summary>
    /// 获取或设置S3 区域标识。
    /// </summary>
    /// <remarks>不能使用原生 Kodo 区域简称代替。默认值为空字符串。</remarks>
    public string Region { get; set; } = "";
    /// <summary>
    /// 获取或设置存储服务访问密钥标识。
    /// </summary>
    /// <remarks>默认值为空字符串。</remarks>
    public string AccessKey { get; set; } = "";
    /// <summary>
    /// 获取或设置存储服务私密密钥。
    /// </summary>
    /// <remarks>默认值为空字符串。</remarks>
    public string SecretKey { get; set; } = "";
    /// <summary>
    /// 获取或设置隔离当前应用对象键的命名空间。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;bing&quot;</c>。</remarks>
    public string StorageNamespace { get; set; } = "bing";
    /// <summary>
    /// 获取或设置是否使用路径式存储桶寻址。
    /// </summary>
    /// <remarks>默认值为 true。</remarks>
    public bool ForcePathStyle { get; set; } = true;
    /// <summary>
    /// 获取或设置查询分片清单时的单页大小。
    /// </summary>
    /// <remarks>默认值为 1000。</remarks>
    public int ListPartsPageSize { get; set; } = 1000;
    /// <summary>
    /// 获取或设置每个允许使用的逻辑容器均须映射到控制台显示的 S3 空间名。
    /// </summary>
    public Dictionary<string, string> ContainerBuckets { get; set; } = new(StringComparer.Ordinal);
    /// <summary>
    /// 获取或设置默认代理上传。
    /// </summary>
    /// <remarks>仅在目标空间通过强校验直传验证后显式选择 SignedParts。</remarks>
    public S3UploadMode UploadMode { get; set; } = S3UploadMode.ServerProxy;
}

/// <summary>
/// 七牛 S3 兼容文件存储注册扩展。
/// </summary>
/// <remarks>
/// 通过现有 S3 Provider 注册七牛命名存储实例。
/// </remarks>
public static class BingQiniuStorageExtensions
{
    /// <summary>
    /// 验证 Qiniu S3 端点并注册存储后端。
    /// </summary>
    /// <param name="services">要添加后端的依赖注入服务集合。</param>
    /// <param name="name">后端名称。</param>
    /// <param name="configure">配置 Qiniu 存储选项的委托。</param>
    /// <returns>原依赖注入服务集合。</returns>
    /// <remarks>
    /// Endpoint 必须是与 Region 匹配的 HTTPS S3 服务地址；凭据和逻辑容器映射必须配置。
    /// </remarks>
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
