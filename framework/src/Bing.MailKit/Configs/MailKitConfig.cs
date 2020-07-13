using MailKit.Security;

namespace Bing.MailKit.Configs
{
    /// <summary>
    /// MailKit 配置
    /// </summary>
    public class MailKitConfig
    {
        /// <summary>
        /// 安全套接字选项
        /// </summary>
        public SecureSocketOptions? SecureSocketOption { get; set; }
    }
}
