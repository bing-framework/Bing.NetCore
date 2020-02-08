using Bing.Ui.Operations;
using Bing.Ui.Operations.Bind;
using Bing.Ui.Operations.Events;
using Bing.Ui.Operations.Forms;
using Bing.Ui.Operations.Forms.Validations;
using Bing.Ui.Operations.Layouts;

namespace Bing.Ui.Components
{
    /// <summary>
    /// 表单控件
    /// </summary>
    public interface IFormControl : IComponent, IName,
        IDisabled, IPlaceholder, IHint,
        IModel, IRequired, IOnChange,
        IColspan, IStandalone, IBindName,
        IPrefix, ISuffix,
        IOnFocus, IOnBlur,
        IOnKeyup, IOnKeydown

    {
    }
}
