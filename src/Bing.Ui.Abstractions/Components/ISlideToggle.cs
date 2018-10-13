using Bing.Ui.Operations;
using Bing.Ui.Operations.Bind;
using Bing.Ui.Operations.Events;
using Bing.Ui.Operations.Forms;
using Bing.Ui.Operations.Forms.Validations;
using Bing.Ui.Operations.Layouts;
using Bing.Ui.Operations.Styles;

namespace Bing.Ui.Components
{
    /// <summary>
    /// 滑动开关
    /// </summary>
    public interface ISlideToggle : IComponent, IName, ILabel, IDisabled, IModel, IRequired, IOnChange, ILabelPosition,
        IColor, IColspan, IStandalone, IBindName
    {
    }
}
