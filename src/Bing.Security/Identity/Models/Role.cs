using Bing.Utils.Helpers;

namespace Bing.Security.Identity.Models
{
    /// <summary>
    /// 角色
    /// </summary>
    public abstract partial class Role<TRole, TKey, TParentId>
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public override void Init()
        {
            base.Init();
            IsAdmin = false;
            InitPinYin();
        }

        /// <summary>
        /// 初始化拼音简码
        /// </summary>
        public void InitPinYin()
        {
            PinYin = Str.PinYin(Name);
        }
    }
}
