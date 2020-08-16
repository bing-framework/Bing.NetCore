using Bing.Application.Dtos;

namespace Bing.Application.Services
{
    /// <summary>
    /// 增删改查应用服务
    /// </summary>
    /// <typeparam name="TEntityDto">实体 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public interface ICrudAppService<TEntityDto, in TKey> : ICrudAppService<TEntityDto, TKey, PagedAndSortedResultRequestDto> { }

    /// <summary>
    /// 增删改查应用服务
    /// </summary>
    /// <typeparam name="TEntityDto">实体 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TGetListInput">列表输入 - 数据传输对象类型</typeparam>
    public interface ICrudAppService<TEntityDto, in TKey, in TGetListInput> : ICrudAppService<TEntityDto, TKey, TGetListInput, TEntityDto> { }

    /// <summary>
    /// 增删改查应用服务
    /// </summary>
    /// <typeparam name="TEntityDto">实体 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TGetListInput">列表输入 - 数据传输对象类型</typeparam>
    /// <typeparam name="TCreateUpdateInput">创建/更新参数类型</typeparam>
    public interface ICrudAppService<TEntityDto, in TKey, in TGetListInput, in TCreateUpdateInput> : ICrudAppService<TEntityDto, TKey, TGetListInput, TCreateUpdateInput, TCreateUpdateInput> { }

    /// <summary>
    /// 增删改查应用服务
    /// </summary>
    /// <typeparam name="TEntityDto">实体 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TGetListInput">列表输入 - 数据传输对象类型</typeparam>
    /// <typeparam name="TCreateInput">创建参数类型</typeparam>
    /// <typeparam name="TUpdateInput">更新参数类型</typeparam>
    public interface ICrudAppService<TEntityDto, in TKey, in TGetListInput, in TCreateInput, in TUpdateInput> : ICrudAppService<TEntityDto, TEntityDto, TKey, TGetListInput, TCreateInput, TUpdateInput> { }

    /// <summary>
    /// 增删改查应用服务
    /// </summary>
    /// <typeparam name="TGetOutputDto">对象输出 - 数据传输对象类型</typeparam>
    /// <typeparam name="TGetListOutputDto">列表输出 - 数据传输对象类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TGetListInput">列表输入 - 数据传输对象类型</typeparam>
    /// <typeparam name="TCreateInput">创建参数类型</typeparam>
    /// <typeparam name="TUpdateInput">更新参数类型</typeparam>
    public interface ICrudAppService<TGetOutputDto, TGetListOutputDto, in TKey, in TGetListInput, in TCreateInput, in TUpdateInput>
        : IReadOnlyAppService<TGetOutputDto, TGetListOutputDto, TKey, TGetListInput>
        , ICreateUpdateAppService<TGetOutputDto, TKey, TCreateInput, TUpdateInput>
        , IDeleteAppService<TKey>
    {
    }
}
