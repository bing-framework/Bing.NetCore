using System;
using System.ComponentModel.DataAnnotations;
using Bing.Data.Queries;

namespace Bing.Admin.Service.Shared.Queries.Systems
{
    /// <summary>
    /// 管理员查询参数
    /// </summary>
    public class AdministratorQuery : QueryParameter
    {
        private string _nickname = string.Empty;
        /// <summary>
        /// 昵称
        /// </summary>
        [Display(Name = "昵称")]
        public string Nickname
        {
            get => _nickname == null ? string.Empty : _nickname.Trim();
            set => _nickname = value;
        }
        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name = "启用")]
        public bool? Enabled { get; set; }

        /// <summary>
        /// 角色标识
        /// </summary>
        public Guid? RoleId { get; set; }
    }
}
