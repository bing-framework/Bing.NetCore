using System;

namespace Bing.Ui.Attributes
{
    /// <summary>
    /// 模型绑定
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class ModelAttribute : Attribute
    {
        /// <summary>
        /// 模型
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 忽略模型绑定，设置为true则不设置模型绑定
        /// </summary>
        public bool Ignore { get; set; }

        /// <summary>
        /// 初始化一个<see cref="ModelAttribute"/>类型的实例
        /// </summary>
        /// <param name="model">模型</param>
        public ModelAttribute(string model = "model")
        {
            Model = model;
        }
    }
}
