using System.Threading.Tasks;

namespace Bing.Application.Services
{
    /// <summary>
    /// 创建应用服务
    /// </summary>
    /// <typeparam name="TEntityDto">实体 - 数据传输对象类型</typeparam>
    public interface ICreateAppService<TEntityDto> : ICreateAppService<TEntityDto, TEntityDto> { }

    /// <summary>
    /// 创建应用服务
    /// </summary>
    /// <typeparam name="TGetOutputDto">对象输出 - 数据传输对象类型</typeparam>
    /// <typeparam name="TCreateInput">创建参数类型</typeparam>
    public interface ICreateAppService<TGetOutputDto, in TCreateInput> : IApplicationService
    {
        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="input">请求参数</param>
        TGetOutputDto Create(TCreateInput input);

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="input">请求参数</param>
        Task<TGetOutputDto> CreateAsync(TCreateInput input);
    }
}
