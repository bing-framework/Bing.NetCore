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
    /// 权限服务
    /// </summary>
    public class PermissionService : CrudServiceBase<Permission, PermissionDto, PermissionQuery>, IPermissionService {
        /// <summary>
        /// 初始化权限服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="permissionRepository">权限仓储</param>
        public PermissionService( IDbDesignerUnitOfWork unitOfWork, IPermissionRepository permissionRepository )
            : base( unitOfWork, permissionRepository ) {
            PermissionRepository = permissionRepository;
        }
        
        /// <summary>
        /// 权限仓储
        /// </summary>
        public IPermissionRepository PermissionRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<Permission> CreateQuery( PermissionQuery param ) {
            return new Query<Permission>( param );
        }
    }
}