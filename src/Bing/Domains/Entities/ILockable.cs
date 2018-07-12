namespace Bing.Domains.Entities
{
    /// <summary>
    /// 数据锁定
    /// </summary>
    public interface ILockable
    {
        /// <summary>
        /// 是否锁定当前信息
        /// </summary>
        bool IsLocked { get; set; }
    }
}
