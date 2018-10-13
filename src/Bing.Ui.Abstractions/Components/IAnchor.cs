using Bing.Ui.Operations;
using Bing.Ui.Operations.Events;
using Bing.Ui.Operations.Navigation;
using Bing.Ui.Operations.Styles;

namespace Bing.Ui.Components
{
    /// <summary>
    /// 链接
    /// </summary>
    public interface IAnchor : IComponent, ILink, IText, IDisabled, IColor, ITooltip, IButtonStyle, IOnClick
    {
    }
}
