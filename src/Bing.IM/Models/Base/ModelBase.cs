namespace Bing.IM.Models.Base
{
    /// <summary>
    /// 模型基类
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public class ModelBase<TKey>
    {
        /// <summary>
        /// 标识
        /// </summary>
        public TKey Id { get; set; }
    }

    /// <summary>
    /// 头像模型基类
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public class AvatarModelBase<TKey> : ModelBase<TKey>
    {
        /// <summary>
        /// 头像
        /// </summary>
        public string Avatar { get; set; }
    }
}
