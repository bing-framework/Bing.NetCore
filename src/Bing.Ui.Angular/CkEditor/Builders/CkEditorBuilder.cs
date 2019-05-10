using  Bing.Ui.Builders;

namespace Bing.Ui.CkEditor.Builders
{
    /// <summary>
    /// CkEditor富文本框编辑器生成器
    /// </summary>
    public class CkEditorBuilder:TagBuilder
    {
        /// <summary>
        /// 初始化一个<see cref="CkEditorBuilder"/>类型的实例
        /// </summary>
        public CkEditorBuilder() : base("ckeditor")
        {
        }
    }
}
