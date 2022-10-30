using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Bing.Utils.Json;
using Microsoft.IdentityModel.Tokens;

namespace Bing.Permissions.Identity.JwtBearer.Internal;

/// <summary>
/// Jwt令牌校验器
/// </summary>
internal sealed class JsonWebTokenValidator : IJsonWebTokenValidator
{
    /// <summary>
    /// 校验
    /// </summary>
    /// <param name="encodeJwt">加密后的Jwt令牌</param>
    /// <param name="options">Jwt选项配置</param>
    /// <param name="validatePayload">校验负载</param>
    public bool Validate(string encodeJwt, JwtOptions options, Func<IDictionary<string, string>, JwtOptions, bool> validatePayload)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
            throw new ArgumentNullException(nameof(options.Secret),
                $@"{nameof(options.Secret)}为Null或空字符串。请在""appsettings.json""配置""{nameof(JwtOptions)}""节点及其子节点""{nameof(JwtOptions.Secret)}""");
        var jwtArray = encodeJwt.Split('.');
        if (jwtArray.Length < 3)
            return false;
        var header = JsonHelper.ToObject<Dictionary<string, string>>(Base64UrlEncoder.Decode(jwtArray[0]));
        var payload = JsonHelper.ToObject<Dictionary<string, string>>(Base64UrlEncoder.Decode(jwtArray[1]));

        // 首先验证签名是否正确
        var hs256 = new HMACSHA256(Encoding.UTF8.GetBytes(options.Secret));
        var sign = Base64UrlEncoder.Encode(
            hs256.ComputeHash(Encoding.UTF8.GetBytes(string.Concat(jwtArray[0], ".", jwtArray[1]))));
        // 签名不正确直接返回
        if (!string.Equals(jwtArray[2], sign))
            return false;
        // 其次验证是否在有效期内
        //var now = ToUnixEpochDate(DateTime.UtcNow);
        //if (!(now >= long.Parse(payload["nbf"].ToString()) && now < long.Parse(payload["exp"].ToString())))
        //    return false;
        // 进行自定义验证
        return validatePayload(payload, options);
    }

    /// <summary>
    /// 生成时间戳
    /// </summary>
    private long ToUnixEpochDate(DateTime date) =>
        (long)Math.Round((date.ToUniversalTime() - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .TotalSeconds);
}