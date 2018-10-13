using Bing.Ui.Operations.Datas;
using Bing.Ui.Operations.Forms;

namespace Bing.Ui.Components
{
    /// <summary>
    /// 下拉列表
    /// </summary>
    public interface ISelect:IFormControl,IUrl,IDataSource,IItem,IResetOption,IMultiple
    {
        /// <summary>
        /// 绑定枚举
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <returns></returns>
        ISelect Enum<TEnum>();
    }
}
