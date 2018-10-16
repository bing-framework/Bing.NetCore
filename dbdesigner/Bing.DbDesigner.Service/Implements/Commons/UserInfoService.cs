using Bing;
using Bing.Extensions.AutoMapper;
using Bing.Domains.Repositories;
using Bing.Datas.Queries;
using Bing.Applications;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Commons.Domain.Models;
using Bing.DbDesigner.Commons.Domain.Repositories;
using Bing.DbDesigner.Service.Dtos.Commons;
using Bing.DbDesigner.Service.Queries.Commons;
using Bing.DbDesigner.Service.Abstractions.Commons;

namespace Bing.DbDesigner.Service.Implements.Commons {
    /// <summary>
    /// 用户信息服务
    /// </summary>
    public class UserInfoService : CrudServiceBase<UserInfo, UserInfoDto, UserInfoQuery>, IUserInfoService {
        /// <summary>
        /// 初始化用户信息服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="userInfoRepository">用户信息仓储</param>
        public UserInfoService( IDbDesignerUnitOfWork unitOfWork, IUserInfoRepository userInfoRepository )
            : base( unitOfWork, userInfoRepository ) {
            UserInfoRepository = userInfoRepository;
        }
        
        /// <summary>
        /// 用户信息仓储
        /// </summary>
        public IUserInfoRepository UserInfoRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<UserInfo> CreateQuery( UserInfoQuery param ) {
            return new Query<UserInfo>( param );
        }
    }
}