using System.Collections.Generic;

namespace Bing.IM.Models.Base
{
    /// <summary>
    /// 大集团组模型
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public class BigGroupModel<TKey> : AvatarModelBase<TKey>
    {
        /// <summary>
        /// 群组名
        /// </summary>
        public string GroupName { get; set; }
    }

    /// <summary>
    /// 朋友组模型
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public class FriendGroupModel<TKey> : ModelBase<TKey>
    {
        /// <summary>
        /// 群组名
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// 在线状态
        /// </summary>
        public int Online { get; set; }

        /// <summary>
        /// 用户列表
        /// </summary>
        public IEnumerable<UserModel<TKey>> Users { get; set; }
    }
}
