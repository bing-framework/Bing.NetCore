namespace Bing.Domains.Entities.Auditing
{
    /// <summary>
    /// 删除人审计
    /// </summary>
    public interface IDeleter
    {
        /// <summary>
        /// 删除人
        /// </summary>
        string Deleter { get; set; }
    }
}
