namespace Bing.Geetest.Models
{
    /// <summary>
    /// 极验注册结果
    /// </summary>
    public class GeetestRegisterResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Geetest 私钥
        /// </summary>
        public string Gt { get; set; }

        /// <summary>
        /// 本次验证会话的唯一标识
        /// </summary>
        public string Challenge { get; set; }

        /// <summary>
        /// 是否新的验证码
        /// </summary>
        public bool NewCaptcha { get; set; }
    }
}
