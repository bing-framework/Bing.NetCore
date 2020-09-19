using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.Domain.Services;

namespace Bing.Admin.Systems.Domain.Services.Abstractions
{
    /// <summary>
    /// 用户登录日志管理
    /// </summary>
    public interface IUserLoginLogManager : IDomainService
    {
        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="parameter">参数</param>
        Task CreateAsync(UserLoginLogParameter parameter);
    }
}
