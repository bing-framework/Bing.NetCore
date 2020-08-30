using System.Collections.Generic;
using Bing.Extensions;

namespace Bing.Admin.Service.Shared.Dtos.NgAlain
{
    /// <summary>
    /// 应用程序数据
    /// </summary>
    public class AppData
    {
        /// <summary>
        /// 初始化一个<see cref="AppData"/>类型的实例
        /// </summary>
        public AppData()
        {
            App = new AppInfo();
            User = new UserInfo();
            Role = new List<RoleInfo>();
            Menu = new List<MenuInfo>();
            Tenant = new TenantInfo();
        }

        /// <summary>
        /// 应用程序信息
        /// </summary>
        public AppInfo App { get; set; }

        /// <summary>
        /// 用户信息
        /// </summary>
        public UserInfo User { get; set; }

        /// <summary>
        /// 角色信息
        /// </summary>
        public List<RoleInfo> Role { get; set; }

        /// <summary>
        /// 租户信息
        /// </summary>
        public TenantInfo Tenant { get; set; }

        /// <summary>
        /// 菜单信息
        /// </summary>
        public List<MenuInfo> Menu { get; set; }

        /// <summary>
        /// 是否租户
        /// </summary>
        public bool IsTenant => !Tenant.Id.IsEmpty();
    }
}
