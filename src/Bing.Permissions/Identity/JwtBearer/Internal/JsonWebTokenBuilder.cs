using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.Tasks;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Extensions;
using Bing.Utils.Helpers;
using Bing.Utils.Timing;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bing.Permissions.Identity.JwtBearer.Internal
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
        /// 令牌Payload存储器
        /// </summary>
        private readonly ITokenPayloadStore _tokenPayloadStore;

        /// <summary>
        /// Jwt安全令牌处理器
        /// </summary>
        private readonly JwtSecurityTokenHandler _tokenHandler;

        /// <summary>
        /// Jwt选项配置
        /// </summary>
        private readonly JwtOptions _options;

        /// <summary>
        /// 初始化一个<see cref="JsonWebTokenBuilder"/>类型的实例
        /// </summary>
        /// <param name="tokenStore">Jwt令牌存储器</param>
        /// <param name="tokenPayloadStore">令牌Payload存储器</param>
        /// <param name="options">Jwt选项配置</param>
        public JsonWebTokenBuilder(IJsonWebTokenStore tokenStore
            , ITokenPayloadStore tokenPayloadStore
            , IOptions<JwtOptions> options)
        {
            _tokenStore = tokenStore;
            _tokenPayloadStore = tokenPayloadStore;
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
        public async Task<JsonWebToken> CreateAsync(IDictionary<string, string> payload, JwtOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Secret))
                throw new ArgumentNullException(nameof(_options.Secret),
                    $@"{nameof(options.Secret)}为Null或空字符串。请在""appsettings.json""配置""{nameof(JwtOptions)}""节点及其子节点""{nameof(JwtOptions.Secret)}""");
            var clientId = payload.ContainsKey("clientId") ? payload["clientId"] : Guid.NewGuid().ToString();
            var clientType = payload.ContainsKey("clientType") ? payload["clientType"] : "admin";
            var userId = GetUserId(payload);
            if (userId.IsEmpty())
                throw new ArgumentException("不存在用户标识");
            var claims = Helper.ToClaims(payload);

            // 生成刷新令牌
            var (refreshToken, refreshExpires) =
                Helper.CreateToken(_tokenHandler, claims, options, JsonWebTokenType.RefreshToken);
            var refreshTokenStr = refreshToken;
            await _tokenStore.SaveRefreshTokenAsync(new RefreshToken()
            {
                ClientId = clientId,
                EndUtcTime = refreshExpires,
                Value = refreshTokenStr
            });

            // 生成访问令牌
            var (token, accessExpires) =
                Helper.CreateToken(_tokenHandler, claims, _options, JsonWebTokenType.AccessToken);
            var accessToken = new JsonWebToken()
            {
                AccessToken = token,
                AccessTokenUtcExpires = Conv.To<long>(accessExpires.ToJsGetTime()),
                RefreshToken = refreshTokenStr,
                RefreshUtcExpires = Conv.To<long>(refreshExpires.ToJsGetTime())
            };
            await _tokenStore.SaveTokenAsync(accessToken, accessExpires);

            // 绑定用户设备令牌
            await _tokenStore.BindUserDeviceTokenAsync(userId, clientType, new DeviceTokenBindInfo()
            {
                UserId = userId, DeviceId = clientId, DeviceType = clientType, Token = accessToken,
            }, refreshExpires);
            // 存储payload
            await _tokenPayloadStore.SaveAsync(refreshToken, payload, refreshExpires);
            return accessToken;
        }

        /// <summary>
        /// 获取用户标识
        /// </summary>
        /// <param name="payload">负载列表</param>
        private string GetUserId(IDictionary<string, string> payload)
        {
            var userId = payload.GetOrDefault(IdentityModel.JwtClaimTypes.Subject, string.Empty);
            if(userId.IsEmpty())
                userId= payload.GetOrDefault(System.Security.Claims.ClaimTypes.NameIdentifier, string.Empty);
            return userId;
        }

        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <param name="refreshToken">刷新令牌</param>
        public async Task<JsonWebToken> RefreshAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
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
                    await _tokenPayloadStore.RemoveAsync(refreshToken);
                }
                    
                throw new Warning("刷新令牌不存在或已过期");
            }

            var principal = _tokenHandler.ValidateToken(refreshToken, parameters, out var securityToken);
            var payload = await _tokenPayloadStore.GetAsync(refreshToken);
            var result = await CreateAsync(payload, _options);
            if (result != null)
            {
                await _tokenStore.RemoveRefreshTokenAsync(refreshToken);
                await _tokenPayloadStore.RemoveAsync(refreshToken);
            }
            return result;
        }
    }
}
