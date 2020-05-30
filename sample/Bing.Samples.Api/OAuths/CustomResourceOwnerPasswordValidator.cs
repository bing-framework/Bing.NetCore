using System;
using System.Threading.Tasks;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Test;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Authentication;

namespace Bing.Samples.Api.OAuths
{
    /// <summary>
    /// 自定义资源所有者密码验证器
    /// </summary>
    public class CustomResourceOwnerPasswordValidator:IResourceOwnerPasswordValidator
    {
        private readonly TestUserStore _users;
        private readonly ISystemClock _clock;

        public CustomResourceOwnerPasswordValidator(TestUserStore users, ISystemClock clock)
        {
            _users = users;
            _clock = clock;
        }

        public Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            if (_users.ValidateCredentials(context.UserName, context.Password))
            {
                var user = _users.FindByUsername(context.UserName);
                //验证通过返回结果 
                // subjectId 为用户唯一标识 一般为用户id
                // authenticationMethod 描述自定义授权类型的认证方法 
                // authTime 授权时间
                // claims 需要返回的用户身份信息单元 此处应该根据我们从数据库读取到的用户信息 添加Claims 如果是从数据库中读取角色信息，那么我们应该在此处添加
                context.Result = new GrantValidationResult(
                    user.SubjectId ?? throw new ArgumentException("Subject Id not set", nameof(user.SubjectId)),
                    OidcConstants.AuthenticationMethods.Password, _clock.UtcNow.UtcDateTime, user.Claims);
            }
            else
            {
                context.Result=new GrantValidationResult(TokenRequestErrors.InvalidGrant,"invalid custom credential");
            }

            return Task.CompletedTask;
        }
    }
}
