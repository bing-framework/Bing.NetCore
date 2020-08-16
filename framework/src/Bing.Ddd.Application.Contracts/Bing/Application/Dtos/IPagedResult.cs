namespace Bing.Application.Dtos
{
    /// <summary>
    /// 定义分页结果
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public interface IPagedResult<T> : IListResult<T>, IHasTotalCount
    {
    }
}
