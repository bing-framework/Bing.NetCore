namespace Bing.Domains.Entities
{
    /// <summary>
    /// 实体Hash。对实体的属性值进行Hash，确定实体是否存在变化。
    /// 这些变化可用于系统初始化时确定是否需要进行数据同步
    /// </summary>
    public interface IEntityHash
    {
    }
}
