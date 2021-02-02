using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bing.Exceptions;
using Bing.ObjectMapping;
using Bing.Validations;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 授权参数基类
    /// </summary>
    public class AuthorizationParamBase : IValidation
    {
        /// <summary>
        /// 授权类型
        /// </summary>
        [Required(ErrorMessage = "授权类型[ResponseType]不能为空")]
        public string ResponseType { get; set; } = OAuthConst.Code;

        /// <summary>
        /// 重定向地址
        /// </summary>
        public string RedirectUri { get; set; }

        /// <summary>
        /// 客户端的当前状态，可以指定任意值，认证服务器会原封不动地返回这个值
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void Init() => InitState();

        /// <summary>
        /// 初始化客户端当前状态
        /// </summary>
        private void InitState()
        {
            if (string.IsNullOrWhiteSpace(State)) 
                State = Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 验证
        /// </summary>
        public virtual ValidationResultCollection Validate()
        {
            var result = DataAnnotationValidation.Validate(this);
            if (result.IsValid)
                return ValidationResultCollection.Success;
            throw new Warning(result.First().ErrorMessage);
        }

        /// <summary>
        /// 转换为授权参数
        /// </summary>
        public virtual AuthorizationParam ToParam() => this.MapTo<AuthorizationParam>();
    }
}
