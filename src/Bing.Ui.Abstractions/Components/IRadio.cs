using Bing.Ui.Operations;
using Bing.Ui.Operations.Bind;
using Bing.Ui.Operations.Datas;
using Bing.Ui.Operations.Events;
using Bing.Ui.Operations.Forms;
using Bing.Ui.Operations.Forms.Validations;
using Bing.Ui.Operations.Layouts;

namespace Bing.Ui.Components
{
    /// <summary>
    /// 单选框
    /// </summary>
    public interface IRadio : IComponent, IName,
        ILabel, IDisabled, IModel,
        IRequired, IOnChange, ILabelPosition,
        IUrl, IDataSource, IItem,
        IColspan, IStandalone, IBindName
    {
    }
}
