using System;
using System.ComponentModel.DataAnnotations;
using Bing.Applications.Dtos;

namespace Bing.Admin.Service.Requests.Systems
{
    /// <summary>
    /// 重置密码请求
    /// </summary>
    public class ResetPasswordRequest : RequestBase
    {
        /// <summary>
        /// 用户标识
        /// </summary>
        [Required(ErrorMessage = "用户标识不能为空")]
        public Guid UserId { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; }
    }
}
