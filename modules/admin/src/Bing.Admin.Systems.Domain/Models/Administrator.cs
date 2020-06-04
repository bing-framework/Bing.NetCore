using Bing.Admin.Systems.Domain.Parameters;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 管理员
    /// </summary>
    public partial class Administrator
    {
        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="parameter">参数</param>
        public void Update(UserParameter parameter)
        {
            Name = parameter.Nickname;
            Enabled = parameter.Enabled;
        }
    }
}
