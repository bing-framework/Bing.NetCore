using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bing.Application.Dtos;
using Bing.Exceptions;
using Bing.Validations;

namespace Bing.Admin.Service.Shared.Requests.Systems
{
    /// <summary>
    /// 更新密码请求
    /// </summary>
    public class UpdatePasswordRequest : RequestBase
    {
        /// <summary>
        /// 旧密码
        /// </summary>
        [Required(ErrorMessage = "旧密码不能为空")]
        public string OldPassword { get; set; }

        /// <summary>
        /// 新密码
        /// </summary>
        [Required(ErrorMessage = "新密码不能为空")]
        public string NewPassword { get; set; }

        /// <summary>
        /// 确认密码
        /// </summary>
        [Required(ErrorMessage = "确认密码不能为空")]
        public string ConfirmPassword { get; set; }

        /// <summary>
        /// 验证
        /// </summary>
        public override ValidationResultCollection Validate()
        {
            var result = DataAnnotationValidation.Validate(this);
            if (NewPassword != ConfirmPassword)
                result.Add(new ValidationResult("新密码不一致"));
            if (result.IsValid)
                return ValidationResultCollection.Success;
            throw new Warning(result.First().ErrorMessage);
        }
    }
}
