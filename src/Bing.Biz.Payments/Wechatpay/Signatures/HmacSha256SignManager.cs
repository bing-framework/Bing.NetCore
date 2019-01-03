using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Bing.Utils.Parameters;
using Bing.Utils.Signatures;

namespace Bing.Biz.Payments.Wechatpay.Signatures
{
    /// <summary>
    /// 微信支付HMACSHA256签名管理器
    /// </summary>
    public class HmacSha256SignManager:ISignManager
    {
        /// <summary>
        /// 签名密钥
        /// </summary>
        private readonly ISignKey _key;

        /// <summary>
        /// Url参数生成器
        /// </summary>
        private readonly UrlParameterBuilder _builder;

        /// <summary>
        /// 初始化一个<see cref="HmacSha256SignManager"/>类型的实例
        /// </summary>
        /// <param name="key">签名密钥</param>
        /// <param name="builder">参数生成器</param>
        public HmacSha256SignManager(ISignKey key, ParameterBuilder builder = null)
        {
            key.CheckNotNull(nameof(key));
            _key = key;
            _builder = builder == null ? new UrlParameterBuilder() : new UrlParameterBuilder(builder);
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public ISignManager Add(string key, object value)
        {
            _builder.Add(key, value);
            return this;
        }

        /// <summary>
        /// 签名
        /// </summary>
        /// <returns></returns>
        public string Sign()
        {
            var value = $"{_builder.Result(true)}&key={_key.GetKey()}";
            return Encrypt.HmacSha256(value, _key.GetKey()).ToUpper();
        }

        /// <summary>
        /// 验证签名
        /// </summary>
        /// <param name="sign">签名</param>
        /// <returns></returns>
        public bool Verify(string sign)
        {
            if (sign.IsEmpty())
            {
                return false;
            }
            return sign == Sign();
        }
    }
}
