using Bing.Ui.Components;
using Bing.Ui.Components.Internal;
using Bing.Ui.Configs;
using Bing.Ui.Operations.Forms;

namespace Bing.Ui.Extensions
{
    /// <summary>
    /// 组件扩展 - 表单
    /// </summary>
    public static partial class Extensions
    {
        #region Placeholder(设置占位提示符)

        /// <summary>
        /// 设置占位提示符
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="text">文本</param>
        /// <returns></returns>
        public static TComponent Placeholder<TComponent>(this TComponent component, string text)
            where TComponent : IComponent, IPlaceholder
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.Placeholder, text); });
            return component;
        }

        #endregion

        #region Model(模型绑定)

        /// <summary>
        /// 模型绑定
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="model">模型</param>
        public static TComponent Model<TComponent>(this TComponent component, string model)
            where TComponent : IModel
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.Model, model); });
            return component;
        }

        #endregion
    }
}
