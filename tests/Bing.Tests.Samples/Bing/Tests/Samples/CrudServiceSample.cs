using System.Threading.Tasks;
using Bing.Applications;
using Bing.Datas.Queries;
using Bing.Datas.UnitOfWorks;
using Bing.Domains.Repositories;
using Bing.Mapping;
using Bing.Utils.Extensions;

namespace Bing.Tests.Samples
{
    /// <summary>
    /// 增删改查服务样例
    /// </summary>
    public interface ICrudServiceSample : ICrudService<DtoSample, QueryParameterSample>
    {
    }

    /// <summary>
    /// 工作单元样例
    /// </summary>
    public class UnitOfWorkSample : IUnitOfWork
    {
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// 提交，返回影响的行数
        /// </summary>
        public int Commit() => 1;

        /// <summary>
        /// 提交，返回影响的行数
        /// </summary>
        public Task<int> CommitAsync() => Task.FromResult(1);
    }

    /// <summary>
    /// 增删改查服务样例
    /// </summary>
    public class CrudServiceSample : CrudServiceBase<EntitySample, DtoSample, QueryParameterSample>, ICrudServiceSample
    {
        /// <summary>
        /// 初始化一个<see cref="CrudServiceSample"/>类型的实例
        /// </summary>
        public CrudServiceSample(IUnitOfWork unitOfWork, IRepositorySample repository) : base(unitOfWork, repository)
        {
        }

        /// <summary>
        /// 转换为实体
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        protected override EntitySample ToEntity(DtoSample dto) => dto.MapTo(new EntitySample(dto.Id.ToGuid()));

        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="parameter">查询参数</param>
        protected override IQueryBase<EntitySample> CreateQuery(QueryParameterSample parameter) => new Query<EntitySample>(parameter);
    }
}
