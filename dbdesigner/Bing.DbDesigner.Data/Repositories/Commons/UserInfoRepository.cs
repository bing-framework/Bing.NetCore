using Bing.DbDesigner.Commons.Domain.Models;
using Bing.DbDesigner.Commons.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Commons {
    /// <summary>
    /// 用户信息仓储
    /// </summary>
    public class UserInfoRepository : RepositoryBase<UserInfo>, IUserInfoRepository {
        /// <summary>
        /// 初始化用户信息仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public UserInfoRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}