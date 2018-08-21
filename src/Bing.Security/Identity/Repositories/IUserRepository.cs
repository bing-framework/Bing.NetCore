using Bing.Domains.Repositories;
using Bing.Security.Identity.Models;

namespace Bing.Security.Identity.Repositories
{
    /// <summary>
    /// 用户仓储
    /// </summary>
    /// <typeparam name="TUser">用户类型</typeparam>
    /// <typeparam name="TKey">用户标识类型</typeparam>
    public interface IUserRepository<TUser,in TKey>:IRepository<TUser,TKey> where TUser:User<TUser,TKey>
    {
    }
}
