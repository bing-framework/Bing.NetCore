namespace Bing.Domains.Entities
{
    /// <summary>
    /// 是否已锁定
    /// </summary>
    public interface ILockable
    {
        /// <summary>
        /// 是否已锁定
        /// </summary>
        bool IsLocked { get; set; }
    }
}
