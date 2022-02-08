namespace Bing.AspNetCore.ExceptionHandling
{
    /// <summary>
    /// 授权异常处理器选项配置
    /// </summary>
    public class BingAuthorizationExceptionHandlerOptions
    {
        /// <summary>
        /// 认证方案
        /// </summary>
        public string AuthenticationScheme { get; set; }
    }
}
