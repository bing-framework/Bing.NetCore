using Bing.Ui.Operations;
using Bing.Ui.Operations.Bind;
using Bing.Ui.Operations.Events;
using Bing.Ui.Operations.Forms;

namespace Bing.Ui.Components
{
    /// <summary>
    /// 颜色选择器
    /// </summary>
    public interface IColorPicker : IComponent, IName, IDisabled, IModel, IOnChange, IStandalone, IBindName
    {
    }
}
