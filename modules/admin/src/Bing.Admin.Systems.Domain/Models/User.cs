
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Bing.Admin.Infrastructure.Encryptor;
using Bing.Extensions;
using Bing.Security.Encryptors;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 用户
    /// </summary>
    public partial class User
    {
        /// <summary>
        /// 声明列表
        /// </summary>
        private readonly List<Claim> _claims;

        /// <summary>
        /// 获取声明列表
        /// </summary>
        public List<Claim> GetClaims() => _claims;

        /// <summary>
        /// 添加声明
        /// </summary>
        /// <param name="claim">声明</param>
        public void AddClaim(Claim claim)
        {
            if (claim == null
                || claim.Value.IsEmpty()
                || _claims.Exists(x => string.Equals(x.Type.SafeString(), claim.Type.SafeString(), StringComparison.CurrentCultureIgnoreCase)))
                return;
            _claims.Add(claim);
        }

        /// <summary>
        /// 添加声明
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="value">值</param>
        public void AddClaim(string type, string value)
        {
            if (type.IsEmpty() || value.IsEmpty())
                return;
            AddClaim(new Claim(type, value));
        }

        /// <summary>
        /// 添加用户声明
        /// </summary>
        public void AddUserClaims()
        {
            AddClaim(IdentityModel.JwtClaimTypes.Subject, Id.ToString());
            AddClaim(IdentityModel.JwtClaimTypes.Name, UserName);
            AddClaim(Bing.Security.Claims.ClaimTypes.FullName, Nickname);
            AddClaim(Bing.Security.Claims.ClaimTypes.Mobile, PhoneNumber);
            AddClaim(Bing.Security.Claims.ClaimTypes.Email, Email);
        }

        /// <summary>
        /// 获取加密器
        /// </summary>
        protected override IEncryptor GetEncryptor() => AesEncryptor.Instance;
    }
}
