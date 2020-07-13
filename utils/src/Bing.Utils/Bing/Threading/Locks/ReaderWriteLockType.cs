namespace Bing.Threading.Locks
{
    /// <summary>
    /// 读写锁类型
    /// </summary>
    public enum ReaderWriteLockType
    {
        /// <summary>
        /// 读取
        /// </summary>
        Read,

        /// <summary>
        /// 写入
        /// </summary>
        Write,

        /// <summary>
        /// 更新并读取
        /// </summary>
        UpgradeableRead
    }
}
