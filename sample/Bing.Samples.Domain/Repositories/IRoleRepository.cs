using System;
using Bing.Domains.Repositories;
using Bing.Samples.Domain.Models;

namespace Bing.Samples.Domain.Repositories
{
    /// <summary>
    /// 角色仓储
    /// </summary>
    public interface IRoleRepository : ITreeRepository<Role, Guid, Guid?>
    {
    }
}
