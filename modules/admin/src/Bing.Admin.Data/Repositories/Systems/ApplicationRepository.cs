using System.Threading.Tasks;
using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Pos.Systems.Extensions;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Datas.EntityFramework.Core;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 应用程序 仓储
    /// </summary>
    public class ApplicationRepository : CompactRepositoryBase<Application, ApplicationPo>, IApplicationRepository
    {
        private readonly IApplicationPoStore _store;

        /// <summary>
        /// 初始化一个<see cref="ApplicationRepository"/>类型的实例
        /// </summary>
        /// <param name="store">应用程序存储器</param>
        public ApplicationRepository(IApplicationPoStore store) : base(store) => _store = store;

        /// <summary>
        /// 将持久化对象转成实体
        /// </summary>
        /// <param name="po">持久化对象</param>
        protected override Application ToEntity(ApplicationPo po) => po.ToEntity();

        /// <summary>
        /// 将实体转换成持久化对象
        /// </summary>
        /// <param name="entity">实体</param>
        protected override ApplicationPo ToPo(Application entity) => entity.ToPo();

        /// <summary>
        /// 是否允许跨域访问
        /// </summary>
        /// <param name="origin">来源</param>
        public Task<bool> IsOriginAllowedAsync(string origin) => Task.FromResult(true);

        /// <summary>
        /// 是否允许创建应用程序
        /// </summary>
        /// <param name="entity">应用程序</param>
        public async Task<bool> CanCreateAsync(Application entity)
        {
            var exists = await _store.ExistsAsync(x => x.Code == entity.Code);
            return exists == false;
        }

        /// <summary>
        /// 是否允许修改应用程序
        /// </summary>
        /// <param name="entity">应用程序</param>
        public async Task<bool> CanUpdateAsync(Application entity)
        {
            var exists = await _store.ExistsAsync(x => x.Id != entity.Id && x.Code == entity.Code);
            return exists == false;
        }

        /// <summary>
        /// 通过应用程序编码查找
        /// </summary>
        /// <param name="code">应用程序编码</param>
        public async Task<Application> GetByCodeAsync(string code)
        {
            var po = await _store.SingleAsync(x => x.Code == code);
            return ToEntity(po);
        }
    }
}
