using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using Bing.Aspects;
using Bing.Aspects.Base;
using Bing.Extensions;
using Bing.Security.Claims;

namespace Bing.Events
{
    ///// <summary>
    ///// CAP会话 属性
    ///// </summary>
    //[AttributeUsage(AttributeTargets.Method)]
    //public class CapSessionAttribute : InterceptorBase
    //{
    //    /// <summary>
    //    /// 当前安全主体访问器
    //    /// </summary>
    //    [Autowired]
    //    public ICurrentPrincipalAccessor CurrentPrincipalAccessor { get; set; }

    //    public override async Task Invoke(AspectContext context, AspectDelegate next)
    //    {
    //        var obj = context.Parameters[0];
    //        var objType = obj.GetType();
    //        // 只对类类型进行拦截
    //        if (!objType.IsClass)
    //        {
    //            await next(context);
    //            return;
    //        }

    //        if (obj is IEventSession session)
    //        {
    //            using (CurrentPrincipalAccessor.Change(new ClaimsPrincipal(GetClaimsPrincipal())))
    //            {
    //                await next(context);
    //            }
    //        }
    //    }

    //    /// <summary>
    //    /// 获取安全令牌
    //    /// </summary>
    //    private ClaimsPrincipal GetClaimsPrincipal()
    //    {
    //        var claims = new List<Claim>();
    //        //AddClaim(claims,IdentityModel.JwtClaimTypes.Subject, Guid.NewGuid().ToString());
    //        //AddClaim(claims, IdentityModel.JwtClaimTypes.Name, "Test");
    //        AddClaim(claims, BingClaimTypes.UserId, Guid.NewGuid().ToString());
    //        AddClaim(claims, BingClaimTypes.UserName, "Test");
    //        AddClaim(claims, Bing.Security.Claims.ClaimTypes.FullName, "测试名称");
    //        AddClaim(claims, BingClaimTypes.PhoneNumber, "123456");
    //        AddClaim(claims, BingClaimTypes.Email, "test@test.com");
    //        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    //    }

    //    /// <summary>
    //    /// 添加声明
    //    /// </summary>
    //    /// <param name="list">列表</param>
    //    /// <param name="claim">声明</param>
    //    public void AddClaim(List<Claim> list, Claim claim)
    //    {
    //        if (claim == null
    //            || claim.Value.IsEmpty()
    //            || list.Exists(x => string.Equals(x.Type.SafeString(), claim.Type.SafeString(), StringComparison.CurrentCultureIgnoreCase)))
    //            return;
    //        list.Add(claim);
    //    }

    //    /// <summary>
    //    /// 添加声明
    //    /// </summary>
    //    /// <param name="list">列表</param>
    //    /// <param name="type">类型</param>
    //    /// <param name="value">值</param>
    //    public void AddClaim(List<Claim> list, string type, string value)
    //    {
    //        if (type.IsEmpty() || value.IsEmpty())
    //            return;
    //        AddClaim(list, new Claim(type, value));
    //    }
    //}
}
