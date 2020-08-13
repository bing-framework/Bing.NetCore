namespace Bing.Admin.Infrastructure
{
    /// <summary>
    /// 消息事件常量
    /// </summary>
    public static class MessageEventConst
    {
        /// <summary>
        /// 前缀
        /// </summary>
        internal const string Prefix = "utopa.erp.";

        /// <summary>
        /// 用户登录
        /// </summary>
        public const string UserLogin = Prefix + nameof(UserLogin);

        /// <summary>
        /// 测试消息
        /// </summary>
        public const string TestMessage1 = Prefix + nameof(TestMessage1);

        /// <summary>
        /// 测试消息
        /// </summary>
        public const string TestMessage2 = Prefix + nameof(TestMessage2);
    }
}
