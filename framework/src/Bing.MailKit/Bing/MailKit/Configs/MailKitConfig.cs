using MailKit.Security;

namespace Bing.MailKit.Configs;

/// <summary>
/// MailKit 配置
/// </summary>
public class MailKitConfig
{
    /// <summary>
    /// 安全套接字选项
    /// </summary>
    public SecureSocketOptions? SecureSocketOption { get; set; }

    /// <summary>
    /// 服务器证书验证回调。465 端口需要进行配置
    /// </summary>
    public bool? ServerCertificateValidationCallback { get; set; }
}