namespace Bing.Domains.Entities.Auditing
{
    /// <summary>
    /// 操作人审计
    /// </summary>
    public interface IAuditor : ICreatorAudited, IModifierAudited
    {
    }

    /// <summary>
    /// 操作人审计
    /// </summary>
    /// <typeparam name="TKey">操作人标识类型</typeparam>
    public interface IAuditor<TKey> : ICreatorAudited<TKey>, IModifierAudited<TKey>
    {
    }
}
