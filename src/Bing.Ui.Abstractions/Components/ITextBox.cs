using Bing.Ui.Operations.Forms;
using Bing.Ui.Operations.Forms.Validations;

namespace Bing.Ui.Components
{
    /// <summary>
    /// 文本框
    /// </summary>
    public interface ITextBox : IFormControl, IReadOnly, IMinLength, IMaxLength, IMin, IMax, IRegex
    {
    }
}
