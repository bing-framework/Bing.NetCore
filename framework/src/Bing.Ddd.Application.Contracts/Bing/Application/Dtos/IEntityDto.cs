namespace Bing.Application.Dtos
{
    /// <summary>
    /// 定义实体 - 数据传输对象
    /// </summary>
    public interface IEntityDto
    {
    }

    /// <summary>
    /// 定义实体 - 数据传输对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public interface IEntityDto<TKey> : IEntityDto
    {
        /// <summary>
        /// 标识
        /// </summary>
        TKey Id { get; set; }
    }
}
