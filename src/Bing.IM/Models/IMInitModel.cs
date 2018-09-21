using System.Collections.Generic;
using Bing.IM.Models.Base;

namespace Bing.IM.Models
{
    /// <summary>
    /// IM初始化模型
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    // ReSharper disable once InconsistentNaming
    public class IMInitModel<TKey>
    {
        /// <summary>
        /// 我的用户
        /// </summary>
        public MiniUserModel<TKey> Mini { get; set; }

        /// <summary>
        /// 朋友组列表
        /// </summary>
        public IEnumerable<FriendGroupModel<TKey>> Friends { get; set; }

        /// <summary>
        /// 群组列表
        /// </summary>
        public IEnumerable<BigGroupModel<TKey>> Groups { get; set; }
    }
}
