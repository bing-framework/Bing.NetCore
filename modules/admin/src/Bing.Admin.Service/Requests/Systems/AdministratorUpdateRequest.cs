using System.ComponentModel.DataAnnotations;
using Bing.Applications.Dtos;
using Bing.Biz.Enums;

namespace Bing.Admin.Service.Requests.Systems
{
    /// <summary>
    /// 管理员 - 更新 请求
    /// </summary>
    public class AdministratorUpdateRequest : RequestBase
    {
        /// <summary>
        /// 用户标识
        /// </summary>
        [Required(ErrorMessage = "用户标识不能为空")]
        public string Id { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        [StringLength(50, ErrorMessage = "昵称输入过长，不能超过50位")]
        public string Nickname { get; set; }

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
