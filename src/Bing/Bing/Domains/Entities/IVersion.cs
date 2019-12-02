namespace Bing.Domains.Entities
{
    /// <summary>
    /// 乐观锁
    /// </summary>
    public interface IVersion
    {
        /// <summary>
        /// 版本号
        /// </summary>
        byte[] Version { get; set; }
    }
}
