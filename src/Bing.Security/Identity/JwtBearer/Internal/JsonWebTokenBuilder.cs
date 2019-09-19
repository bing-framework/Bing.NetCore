using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Bing.Exceptions;
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
        /// Jwt令牌存储器
        /// </summary>
        private readonly IJsonWebTokenStore _tokenStore;

        /// <summary>
        /// Jwt安全令牌处理器
        /// </summary>
        private readonly JwtSecurityTokenHandler _tokenHandler;

        /// <summary>
        /// Jwt选项配置
        /// </summary>
        private readonly JsonWebTokenOptions _options;

        public JsonWebTokenBuilder(IJsonWebTokenStore tokenStore
            ,IOptions<JsonWebTokenOptions> options)
        {
            _tokenStore = tokenStore;
            _options = options.Value;
            if (_tokenHandler == null)
                _tokenHandler = new JwtSecurityTokenHandler();
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
            var clientId = payload["clientId"] ?? Guid.NewGuid().ToString();
            var claims = new List<Claim>();
            foreach (var key in payload.Keys)
            {
                var tempClaim = new Claim(key, payload[key]?.ToString());
                claims.Add(tempClaim);
            }

            // 生成刷新令牌
            var (refreshToken, refreshExpires) = CreateToken(claims, options, JsonWebTokenType.RefreshToken);
            var refreshTokenStr = refreshToken;
            await _tokenStore.SaveRefreshTokenAsync(new RefreshToken()
            {
                ClientId = clientId,
                EndUtcTime = refreshExpires,
                Value = refreshTokenStr
            });

            // 生成访问令牌
            var (token, accessExpires) = CreateToken(claims, _options, JsonWebTokenType.AccessToken);
            var accessToken = new JsonWebToken()
            {
                AccessToken = token,
                RefreshToken = refreshTokenStr,
                RefreshUtcExpires = Conv.To<long>(refreshExpires.ToJsGetTime())
            };
            await _tokenStore.SaveTokenAsync(accessToken, accessExpires);

            return accessToken;
        }

        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <param name="refreshToken">刷新令牌</param>
        public async Task<JsonWebToken> RefreshAsync(string refreshToken)
        {
            if(string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));
            var parameters = new TokenValidationParameters()
            {
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
            };
            var tokenModel = await _tokenStore.GetRefreshTokenAsync(refreshToken);
            if (tokenModel == null || tokenModel.Value != refreshToken || tokenModel.EndUtcTime <= DateTime.UtcNow)
            {
                if (tokenModel != null && tokenModel.EndUtcTime <= DateTime.UtcNow)
                {
                    await _tokenStore.RemoveRefreshTokenAsync(refreshToken);
                }
                throw new Warning("刷新令牌不存在或已过期");
            }

            var principal = _tokenHandler.ValidateToken(refreshToken, parameters, out var securityToken);
            var payload = principal.Claims.ToDictionary(x => x.Type, x => x.Value);
            var result = await CreateAsync(payload, _options);
            if (result != null)
            {
                await _tokenStore.RemoveRefreshTokenAsync(refreshToken);


            }
            return result;
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
            var token = _tokenHandler.CreateToken(descriptor);
            var accessToken = _tokenHandler.WriteToken(token);
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
