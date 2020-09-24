namespace Bing.Mapping
{
    /// <summary>
    /// 定义映射器配置文件
    /// </summary>
    public interface IOrderedMapperProfile
    {
        /// <summary>
        /// 排序
        /// </summary>
        int Order { get; }
    }
}
