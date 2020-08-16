using System.Threading.Tasks;

namespace Bing.Application.Services
{
    /// <summary>
    /// 更新应用服务
    /// </summary>
    /// <typeparam name="TEntityDto">实体 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public interface IUpdateAppService<TEntityDto, in TKey> : IUpdateAppService<TEntityDto, TKey, TEntityDto> { }

    /// <summary>
    /// 更新应用服务
    /// </summary>
    /// <typeparam name="TGetOutputDto">对象输出 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TUpdateInput">更新参数类型</typeparam>
    public interface IUpdateAppService<TGetOutputDto, in TKey, in TUpdateInput> : IApplicationService
    {
        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="id">实体标识</param>
        /// <param name="input">请求参数</param>
        TGetOutputDto Update(TKey id, TUpdateInput input);

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="id">实体标识</param>
        /// <param name="input">请求参数</param>
        Task<TGetOutputDto> UpdateAsync(TKey id, TUpdateInput input);
    }
}
