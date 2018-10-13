using System;
using System.IO;
using Bing.Ui.Components;
using Bing.Ui.Renders;

namespace Bing.Ui.Zorro.Buttons
{
    /// <summary>
    /// 按钮
    /// </summary>
    public class Button:ContainerBase<IDisposable>,IButton
    {
        public Button(TextWriter writer) : base(writer)
        {
        }

        protected override IRender GetRender()
        {
            throw new NotImplementedException();
        }

        protected override IDisposable GetWrapper()
        {
            throw new NotImplementedException();
        }
    }
}
