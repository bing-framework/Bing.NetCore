using System;
using System.Collections.Generic;

namespace Bing.Security.Identity.JwtBearer
{
    /// <summary>
    /// Jwt令牌校验器
    /// </summary>
    public interface IJsonWebTokenValidator
    {
        /// <summary>
        /// 校验
        /// </summary>
        /// <param name="encodeJwt">加密后的Jwt令牌</param>
        /// <param name="options">Jwt选项配置</param>
        /// <param name="validatePayload">校验负载</param>
        bool Validate(string encodeJwt, JsonWebTokenOptions options,
            Func<IDictionary<string, string>, JsonWebTokenOptions, bool> validatePayload);
    }
}
