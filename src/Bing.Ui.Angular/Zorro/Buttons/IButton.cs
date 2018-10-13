using System;
using Bing.Ui.Components;
using Bing.Ui.Operations.Navigation;

namespace Bing.Ui.Zorro.Buttons
{
    /// <summary>
    /// 按钮
    /// </summary>
    public interface IButton : IComponent, IContainer<IDisposable>, Components.IButton, IMenuId
    {
    }
}
