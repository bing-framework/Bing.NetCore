using System.Threading.Tasks;
using Bing.Application.Dtos;

namespace Bing.Application.Services
{
    /// <summary>
    /// 查询应用服务
    /// </summary>
    /// <typeparam name="TEntityDto">实体 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public interface IReadOnlyAppService<TEntityDto, in TKey> : IReadOnlyAppService<TEntityDto, TEntityDto, TKey, PagedAndSortedResultRequestDto>
    {
    }

    /// <summary>
    /// 查询应用服务
    /// </summary>
    /// <typeparam name="TEntityDto">实体 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TGetListInput">列表输入 - 数据传输对象类型</typeparam>
    public interface IReadOnlyAppService<TEntityDto, in TKey, in TGetListInput> : IReadOnlyAppService<TEntityDto, TEntityDto, TKey, TGetListInput>
    {
    }

    /// <summary>
    /// 查询应用服务
    /// </summary>
    /// <typeparam name="TGetOutputDto">对象输出 - 数据传输对象类型</typeparam>
    /// <typeparam name="TGetListOutputDto">列表输出 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TGetListInput">列表输入 - 数据传输对象类型</typeparam>
    public interface IReadOnlyAppService<TGetOutputDto, TGetListOutputDto, in TKey, in TGetListInput> : IApplicationService
    {
        /// <summary>
        /// 通过标识获取
        /// </summary>
        /// <param name="id">实体标识</param>
        TGetOutputDto Get(TKey id);

        /// <summary>
        /// 通过标识获取
        /// </summary>
        /// <param name="id">实体标识</param>
        Task<TGetOutputDto> GetAsync(TKey id);

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="input">查询参数</param>
        PagedResultDto<TGetListOutputDto> GetList(TGetListInput input);

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="input">查询参数</param>
        Task<PagedResultDto<TGetListOutputDto>> GetListAsync(TGetListInput input);
    }
}
