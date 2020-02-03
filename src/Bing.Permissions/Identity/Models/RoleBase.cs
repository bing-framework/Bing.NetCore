using Bing.Extensions;
using Bing.Extensions;
using Bing.Utils.Helpers;
using Str = Bing.Helpers.Str;

namespace Bing.Permissions.Identity.Models
{
    /// <summary>
    /// 角色基类
    /// </summary>
    public partial class RoleBase<TRole, TKey, TParentId>
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public override void Init()
        {
            base.Init();
            InitType();
            InitPinYin();
        }

        /// <summary>
        /// 初始化类型
        /// </summary>
        public void InitType()
        {
            if (Type.IsEmpty())
                Type = "Role";
        }

        /// <summary>
        /// 初始化拼音简码
        /// </summary>
        public void InitPinYin() => PinYin = Str.PinYin(Name);
    }
}
