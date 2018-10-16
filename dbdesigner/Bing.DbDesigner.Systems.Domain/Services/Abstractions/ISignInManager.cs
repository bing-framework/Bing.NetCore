using System;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Security.Identity.Services.Abstractions;

namespace Bing.DbDesigner.Systems.Domain.Services.Abstractions
{
    /// <summary>
    /// 登录管理
    /// </summary>
    public interface ISignInManager:ISignInManager<User,Guid>
    {
    }
}
