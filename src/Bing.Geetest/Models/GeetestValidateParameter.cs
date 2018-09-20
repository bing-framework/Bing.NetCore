namespace Bing.Geetest.Models
{
    /// <summary>
    /// 极验验证参数
    /// </summary>
    public class GeetestValidateParameter
    {
        /// <summary>
        /// 是否离线
        /// </summary>
        public bool Offline { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 本次验证会话的唯一标识
        /// </summary>
        public string Challenge { get; set; }

        /// <summary>
        /// 秒代码
        /// </summary>
        public string Seccode { get; set; }

        /// <summary>
        /// 验证
        /// </summary>
        public string Validate { get; set; }
    }
}
