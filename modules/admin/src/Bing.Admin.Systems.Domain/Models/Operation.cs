using Bing.Helpers;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 操作
    /// </summary>
    public partial class Operation
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
        public void InitPinYin() => PinYin = Str.PinYin(Name);
    }
}
