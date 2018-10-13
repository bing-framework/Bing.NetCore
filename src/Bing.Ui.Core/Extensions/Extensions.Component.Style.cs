using Bing.Ui.Components;
using Bing.Ui.Components.Internal;
using Bing.Ui.Configs;
using Bing.Ui.Operations.Styles;

namespace Bing.Ui.Extensions
{
    /// <summary>
    /// 组件扩展 - 样式
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 设置class
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="class">css类名</param>
        /// <returns></returns>
        public static TComponent Class<TComponent>(this TComponent component, string @class)
            where TComponent : IOption,IClass
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.AddClass(@class); });
            return component;
        }

        /// <summary>
        /// 设置样式
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="style">样式</param>
        /// <returns></returns>
        public static TComponent Style<TComponent>(this TComponent component, string style)
            where TComponent : IOption,IStyle
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.OutputAttributes.Add(UiConst.Style, style); });
            return component;
        }
    }
}
