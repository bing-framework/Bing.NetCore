using System;
using System.Threading.Tasks;
using Bing.Domain.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.ObjectMapping;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 用户登录日志 管理
    /// </summary>
    public class UserLoginLogManager : DomainServiceBase, IUserLoginLogManager
    {
        /// <summary>
        /// 初始化一个<see cref="UserLoginLogManager"/>类型的实例
        /// </summary>
        public UserLoginLogManager(IUserLoginLogRepository userLoginLogRepository)
        {
            UserLoginLogRepository = userLoginLogRepository;
        }

        /// <summary>
        /// 用户登录日志仓储
        /// </summary>
        protected IUserLoginLogRepository UserLoginLogRepository { get; }

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="parameter">参数</param>
        public async Task CreateAsync(UserLoginLogParameter parameter)
        {
            var entity = new UserLoginLog();
            parameter.MapTo(entity);
            entity.Init();
            entity.CreationTime = DateTime.Now;
            await UserLoginLogRepository.AddAsync(entity);
        }
    }
}
