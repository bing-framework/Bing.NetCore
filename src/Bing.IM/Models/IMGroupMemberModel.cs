using System.Collections.Generic;
using Bing.IM.Models.Base;

namespace Bing.IM.Models
{
    /// <summary>
    /// IM群组群员列表模型
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    // ReSharper disable once InconsistentNaming
    public class IMGroupMemberModel<TKey>
    {
        /// <summary>
        /// 本人
        /// </summary>
        public UserModel<TKey> Owner { get; set; }

        /// <summary>
        /// 群员数
        /// </summary>
        public int Members { get; set; }

        /// <summary>
        /// 群员列表
        /// </summary>
        public IEnumerable<UserModel<TKey>> Users { get; set; }
    }
}
