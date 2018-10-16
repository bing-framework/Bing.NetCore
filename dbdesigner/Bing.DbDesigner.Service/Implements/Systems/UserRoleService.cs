using Bing;
using Bing.Extensions.AutoMapper;
using Bing.Domains.Repositories;
using Bing.Datas.Queries;
using Bing.Applications;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.DbDesigner.Service.Dtos.Systems;
using Bing.DbDesigner.Service.Queries.Systems;
using Bing.DbDesigner.Service.Abstractions.Systems;

namespace Bing.DbDesigner.Service.Implements.Systems {
    /// <summary>
    /// 用户角色服务
    /// </summary>
    public class UserRoleService : CrudServiceBase<UserRole, UserRoleDto, UserRoleQuery>, IUserRoleService {
        /// <summary>
        /// 初始化用户角色服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="userRoleRepository">用户角色仓储</param>
        public UserRoleService( IDbDesignerUnitOfWork unitOfWork, IUserRoleRepository userRoleRepository )
            : base( unitOfWork, userRoleRepository ) {
            UserRoleRepository = userRoleRepository;
        }
        
        /// <summary>
        /// 用户角色仓储
        /// </summary>
        public IUserRoleRepository UserRoleRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<UserRole> CreateQuery( UserRoleQuery param ) {
            return new Query<UserRole>( param );
        }
    }
}