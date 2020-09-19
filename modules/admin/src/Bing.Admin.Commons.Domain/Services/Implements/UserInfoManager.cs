using Bing.Domain.Services;
using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Commons.Domain.Services.Abstractions;

namespace Bing.Admin.Commons.Domain.Services.Implements
{
    /// <summary>
    /// 用户信息 管理
    /// </summary>
    public class UserInfoManager : DomainServiceBase, IUserInfoManager
    {
        /// <summary>
        /// 初始化一个<see cref="UserInfoManager"/>类型的实例
        /// </summary>
        public UserInfoManager() { }
    }
}