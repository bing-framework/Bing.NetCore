using System.Threading.Tasks;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Systems {
    /// <summary>
    /// 应用程序仓储
    /// </summary>
    public class ApplicationRepository : RepositoryBase<Application>, IApplicationRepository {
        /// <summary>
        /// 初始化应用程序仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public ApplicationRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }

        #region GetByCodeAsync(通过应用程序编码查找)

        /// <summary>
        /// 通过应用程序编码查找
        /// </summary>
        /// <param name="code">应用程序编码</param>
        /// <returns></returns>
        public async Task<Application> GetByCodeAsync(string code)
        {
            return await SingleAsync(x => x.Code == code);
        }

        #endregion

    }
}