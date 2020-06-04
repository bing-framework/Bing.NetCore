using System.ComponentModel.DataAnnotations;
using Bing.Applications.Dtos;

namespace Bing.Admin.Service.Requests.Systems
{
    /// <summary>
    /// 后台登录请求
    /// </summary>
    public class AdminLoginRequest : RequestBase
    {
        /// <summary>
        /// 用户名
        /// </summary>
        [Required(ErrorMessage = "用户名不能为空")]
        public string UserName { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; }
    }
}
