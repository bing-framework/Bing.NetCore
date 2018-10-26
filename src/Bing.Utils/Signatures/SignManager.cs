using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Bing.Utils.Parameters;

namespace Bing.Utils.Signatures
{
    /// <summary>
    /// 签名管理器
    /// </summary>
    public class SignManager:ISignManager
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
        /// 初始化一个<see cref="SignManager"/>类型的实例
        /// </summary>
        /// <param name="key">签名密钥</param>
        /// <param name="builder">Url参数生成器</param>
        public SignManager(ISignKey key, UrlParameterBuilder builder = null)
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
        public string Sign() => Encrypt.Rsa2Sign(_builder.Result(true), _key.GetKey());

        /// <summary>
        /// 验证签名
        /// </summary>
        /// <param name="sign">签名</param>
        /// <returns></returns>
        public bool Verify(string sign) => Encrypt.Rsa2Verify(_builder.Result(true), _key.GetPublicKey(), sign);
    }
}
