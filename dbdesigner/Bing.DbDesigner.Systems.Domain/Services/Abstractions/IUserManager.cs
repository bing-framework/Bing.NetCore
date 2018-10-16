using System;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Security.Identity.Services.Abstractions;

namespace Bing.DbDesigner.Systems.Domain.Services.Abstractions
{
    /// <summary>
    /// 用户管理
    /// </summary>
    public interface IUserManager : IUserManager<User, Guid>
    {
    }
}
