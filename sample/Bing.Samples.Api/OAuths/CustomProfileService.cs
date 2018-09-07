using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Logs;
using Bing.Utils.Extensions;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Test;

namespace Bing.Samples.Api.OAuths
{
    public class CustomProfileService:IProfileService
    {
        protected readonly TestUserStore Users;

        protected readonly ILog Log;

        public CustomProfileService(TestUserStore users,ILog log)
        {
            Users = users;
            Log = log;
        }

        public Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            Log.Debug(
                $"Get profile called for subject {context.Subject.GetSubjectId()} from client {context.Client.ClientName ?? context.Client.ClientId} with claim types {context.RequestedClaimTypes.Join()} via {context.Caller}");

            // 判断是否有请求Claim信息
            if (context.RequestedClaimTypes.Any())
            {
                // 根据用户标识查找用户信息
                var user = Users.FindBySubjectId(context.Subject.GetSubjectId());
                if (user != null)
                {
                    // 调用此方法以后内部会进行过滤，只将用户请求的Claim加入到 context.IssuedClaims 集合中 这样我们的请求方便能正常获取到所需Claim
                    context.AddRequestedClaims(user.Claims);
                }
            }

            Log.Debug($"Issued claims: {context.IssuedClaims.Select(x => x.Type.Join())}");
            return Task.CompletedTask;
        }

        public Task IsActiveAsync(IsActiveContext context)
        {
            Log.Debug($"IsActive called from:{context.Caller}");
            var user = Users.FindBySubjectId(context.Subject.GetSubjectId());
            context.IsActive = user?.IsActive == true;
            return Task.CompletedTask;
        }
    }
}
