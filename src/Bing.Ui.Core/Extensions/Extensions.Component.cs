using Bing.Ui.Components;
using Bing.Ui.Components.Internal;
using Bing.Ui.Configs;
using Bing.Ui.Operations;

namespace Bing.Ui.Extensions
{
    /// <summary>
    /// 组件扩展
    /// </summary>
    public static partial class Extensions
    {
        #region Attribute(设置属性)

        /// <summary>
        /// 设置属性
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        public static TComponent Attribute<TComponent>(this TComponent component, string name, string value)
            where TComponent : IOption, IAttribute
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.OutputAttributes.Add(name, value); });
            return component;
        }

        /// <summary>
        /// 设置属性
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="value">属性值</param>
        public static TComponent Attribute<TComponent>(this TComponent component, string value)
            where TComponent : IOption, IAttribute
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.OutputAttributes.Add(value, null); });
            return component;
        }

        #endregion

        #region Id(设置标识)

        /// <summary>
        /// 设置标识
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="id">组件标识</param>
        public static TComponent Id<TComponent>(this TComponent component, string id)
            where TComponent : IOption
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.Id, id); });
            return component;
        }

        #endregion

        #region Name(设置名称)

        /// <summary>
        /// 设置名称
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="name">组件名称</param>
        public static TComponent Name<TComponent>(this TComponent component, string name)
            where TComponent : IOption, IName
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.Name, name); });
            return component;
        }

        #endregion

        #region Text(设置文本)

        /// <summary>
        /// 设置文本
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="text">文本</param>
        public static TComponent Text<TComponent>(this TComponent component, string text)
            where TComponent : IOption, IText
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.Text, text); });
            return component;
        }

        #endregion

        #region Disable(禁用)

        /// <summary>
        /// 禁用
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        public static TComponent Disable<TComponent>(this TComponent component)
            where TComponent : IOption, IDisabled
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.Disabled, true); });
            return component;
        }

        #endregion

        #region Tooltip(提示)

        /// <summary>
        /// 提示
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="tooltip">提示</param>
        public static TComponent Tooltip<TComponent>(this TComponent component, string tooltip)
            where TComponent : IOption, ITooltip
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.Tooltip, tooltip); });
            return component;
        }

        #endregion

        #region Height(设置高度)

        /// <summary>
        /// 设置高度
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="height">高度</param>
        public static TComponent Height<TComponent>(this TComponent component, double height)
            where TComponent : IOption, IHeight
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.Height, height); });
            return component;
        }

        #endregion
    }
}
