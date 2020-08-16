namespace Bing.Application.Services
{
    /// <summary>
    /// 创建更新应用服务
    /// </summary>
    /// <typeparam name="TEntityDto">实体 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TCreateUpdateInput">创建/更新参数类型</typeparam>
    public interface ICreateUpdateAppService<TEntityDto, in TKey, in TCreateUpdateInput> : ICreateUpdateAppService<TEntityDto, TKey, TCreateUpdateInput, TCreateUpdateInput>
    {
    }

    /// <summary>
    /// 创建更新应用服务
    /// </summary>
    /// <typeparam name="TGetOutputDto">对象输出 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TCreateInput">创建参数类型</typeparam>
    /// <typeparam name="TUpdateInput">更新参数类型</typeparam>
    public interface ICreateUpdateAppService<TGetOutputDto, in TKey, in TCreateInput, in TUpdateInput> : ICreateAppService<TGetOutputDto, TCreateInput>
        , IUpdateAppService<TGetOutputDto, TKey, TUpdateInput>
    {
    }
}
