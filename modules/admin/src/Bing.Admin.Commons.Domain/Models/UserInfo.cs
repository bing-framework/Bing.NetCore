
using Bing.Extensions;

namespace Bing.Admin.Commons.Domain.Models
{
    /// <summary>
    /// 用户信息
    /// </summary>
    public partial class UserInfo
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public override void Init()
        {
            base.Init();
            InitPinYin();
        }

        /// <summary>
        /// 初始化拼音简码
        /// </summary>
        public void InitPinYin()
        {
            if (!Name.IsEmpty())
                PinYin = Bing.Helpers.Str.PinYin(Name);
            else
                PinYin = string.Empty;
        }
    }
}
