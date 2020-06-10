using System.ComponentModel.DataAnnotations;
using Bing.Applications.Dtos;
using Bing.Biz.Enums;

namespace Bing.Admin.Service.Requests.Systems
{
    /// <summary>
    /// 管理员 - 创建 请求
    /// </summary>
    public class AdministratorCreateRequest: RequestBase
    {
        /// <summary>
        /// 昵称
        /// </summary>
        [StringLength(50, ErrorMessage = "昵称输入过长，不能超过50位")]
        public string Nickname { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        [StringLength(256, ErrorMessage = "用户名输入过长，不能超过256位")]
        public string UserName { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        [Required(ErrorMessage = "密码不能为空")]
        [StringLength(256, ErrorMessage = "密码输入过长，不能超过256位")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        /// <summary>
        /// 性别。1:女士,2:男士
        /// </summary>
        public Gender? Gender { get; set; }

        /// <summary>
        /// 启用
        /// </summary>
        public bool? Enabled { get; set; }
    }
}
