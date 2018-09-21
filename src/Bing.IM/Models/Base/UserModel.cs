namespace Bing.IM.Models.Base
{
    /// <summary>
    /// 用户模型
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public class UserModel<TKey>:AvatarModelBase<TKey>
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 签名
        /// </summary>
        public string Sign { get; set; }
    }

    /// <summary>
    /// 我的用户模型
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public class MiniUserModel<TKey> : UserModel<TKey>
    {
        /// <summary>
        /// 状态
        /// </summary>
        public string Status { get; set; } = "online";
    }
}
