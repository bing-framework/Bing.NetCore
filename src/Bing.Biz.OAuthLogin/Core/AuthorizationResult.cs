namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权结果
    /// </summary>
    public class AuthorizationResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 授权接口返回的原始消息
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// 结果
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 请求参数
        /// </summary>
        public string Parameter { get; set; }

        /// <summary>
        /// 初始化一个<see cref="AuthorizationResult"/>类型的实例
        /// </summary>
        public AuthorizationResult() { }

        /// <summary>
        /// 初始化一个<see cref="AuthorizationResult"/>类型的实例
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="raw">授权接口返回的原始消息</param>
        public AuthorizationResult(bool success, string raw)
        {
            Success = success;
            Raw = raw;
        }
    }
}
