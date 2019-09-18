using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Bing.Utils.Helpers;
using Bing.Utils.Timing;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bing.Security.Identity.JwtBearer.Internal
{
    /// <summary>
    /// Jwt构建器
    /// </summary>
    internal sealed class JsonWebTokenBuilder : IJsonWebTokenBuilder
    {
        /// <summary>
        /// Jwt选项配置
        /// </summary>
        private readonly JsonWebTokenOptions _options;

        /// <summary>
        /// Jwt安全令牌处理器
        /// </summary>
        private readonly JwtSecurityTokenHandler _jwtSecurityTokenHandler;

        public JsonWebTokenBuilder(IOptions<JsonWebTokenOptions> options)
        {
            _options = options.Value;
            if (_jwtSecurityTokenHandler == null)
                _jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
        }


        /// <summary>
        /// 创建令牌
        /// </summary>
        /// <param name="payload">负载</param>
        public async Task<JsonWebToken> CreateAsync(IDictionary<string, string> payload) => await CreateAsync(payload, _options);

        /// <summary>
        /// 创建令牌
        /// </summary>
        /// <param name="payload">负载</param>
        /// <param name="options">Jwt选项配置</param>
        public async Task<JsonWebToken> CreateAsync(IDictionary<string, string> payload, JsonWebTokenOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Secret))
                throw new ArgumentNullException(nameof(_options.Secret),
                    $@"{nameof(options.Secret)}为Null或空字符串。请在""appsettings.json""配置""{nameof(JsonWebTokenOptions)}""节点及其子节点""{nameof(JsonWebTokenOptions.Secret)}""");
            var now = DateTime.UtcNow;
            var claims = new List<Claim>();
            foreach (var key in payload.Keys)
            {
                var tempClaim = new Claim(key, payload[key]?.ToString());
                claims.Add(tempClaim);
            }

            var (token, expires) = CreateToken(claims, options, JsonWebTokenType.RefreshToken);
            var refreshTokenStr = token;

            (token, _) = CreateToken(claims, _options, JsonWebTokenType.AccessToken);

            return new JsonWebToken()
            {
                AccessToken = token,
                RefreshToken = refreshTokenStr,
                RefreshUtcExpires = Conv.To<long>(expires.ToJsGetTime())
            };

            //var jwt = new JwtSecurityToken(
            //    issuer: null,
            //    audience: null,
            //    claims: claims,
            //    notBefore: now,
            //    expires: now.Add(TimeSpan.FromMinutes(options.AccessExpireMinutes)),
            //    signingCredentials: new SigningCredentials(
            //        new SymmetricSecurityKey(Encoding.ASCII.GetBytes(options.Secret)),
            //        SecurityAlgorithms.HmacSha256));
            //var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
            //return new JsonWebToken()
            //{
            //    AccessToken = encodedJwt
            //};
        }


        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <param name="refreshToken">刷新令牌</param>
        public async Task<JsonWebToken> RefreshAsync(string refreshToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 创建令牌
        /// </summary>
        /// <param name="claims">声明列表</param>
        /// <param name="options">Jwt选项配置</param>
        /// <param name="tokenType">Jwt令牌类型</param>
        private (string, DateTime) CreateToken(IEnumerable<Claim> claims, JsonWebTokenOptions options,
            JsonWebTokenType tokenType)
        {
            var secret = options.Secret;
            if (secret == null)
                throw new ArgumentNullException(nameof(secret));
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);
            var now = DateTime.UtcNow;
            var minutes = tokenType == JsonWebTokenType.AccessToken
                ? options.AccessExpireMinutes > 0 ? options.AccessExpireMinutes : 5 // 默认5分钟
                : options.RefreshExpireMinutes > 0
                    ? options.RefreshExpireMinutes
                    : 10080; // 默认7天
            var expires = now.AddMinutes(minutes);
            var descriptor = new SecurityTokenDescriptor()
            {
                Subject = new ClaimsIdentity(claims),
                Audience = options.Audience,
                Issuer = options.Issuer,
                SigningCredentials = credentials,
                NotBefore = now,
                IssuedAt = now,
                Expires = expires
            };
            var token = _jwtSecurityTokenHandler.CreateToken(descriptor);
            var accessToken = _jwtSecurityTokenHandler.WriteToken(token);
            return (accessToken, expires);
        }

        /// <summary>
        /// Jwt令牌类型
        /// </summary>
        private enum JsonWebTokenType
        {
            /// <summary>
            /// 访问令牌
            /// </summary>
            AccessToken,
            /// <summary>
            /// 刷新令牌
            /// </summary>
            RefreshToken
        }
    }
}
