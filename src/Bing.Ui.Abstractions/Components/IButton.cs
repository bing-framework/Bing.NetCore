using Bing.Ui.Operations;
using Bing.Ui.Operations.Events;
using Bing.Ui.Operations.Styles;

namespace Bing.Ui.Components
{
    /// <summary>
    /// 按钮
    /// </summary>
    public interface IButton : IText, IDisabled, IColor, ITooltip, IButtonStyle, IOnClick
    {
    }
}
