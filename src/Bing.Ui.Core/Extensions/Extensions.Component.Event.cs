using Bing.Ui.Components;
using Bing.Ui.Components.Internal;
using Bing.Ui.Configs;
using Bing.Ui.Operations.Events;

namespace Bing.Ui.Extensions
{
    /// <summary>
    /// 组件扩展 - 事件
    /// </summary>
    public static partial class Extensions
    {
        #region OnClick(单击事件处理函数)

        /// <summary>
        /// 单击事件处理函数
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="handler">单击事件处理函数，范例：handle()</param>
        public static TComponent OnClick<TComponent>(this TComponent component, string handler)
            where TComponent : IOption, IOnClick
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.OnClick, handler); });
            return component;
        }

        #endregion

        #region OnChange(变更事件处理函数)

        /// <summary>
        /// 变更事件处理函数
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="handler">变更事件处理函数，范例：handle()</param>
        public static TComponent OnChange<TComponent>(this TComponent component, string handler)
            where TComponent : IOption, IOnChange
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.OnChange, handler); });
            return component;
        }

        #endregion

        #region OnFocus(获得焦点事件处理函数)

        /// <summary>
        /// 获得焦点事件处理函数
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="handler">获得焦点事件处理函数，范例：handle()</param>
        public static TComponent OnFocus<TComponent>(this TComponent component, string handler)
            where TComponent : IOption, IOnFocus
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.OnFocus, handler); });
            return component;
        }

        #endregion

        #region OnBlur(失去焦点事件处理函数)

        /// <summary>
        /// 失去焦点事件处理函数
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="handler">失去焦点事件处理函数，范例：handle()</param>
        public static TComponent OnBlur<TComponent>(this TComponent component, string handler)
            where TComponent : IOption, IOnBlur
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.OnBlur, handler); });
            return component;
        }

        #endregion

        #region OnKeyup(键盘按键事件处理函数)

        /// <summary>
        /// 键盘按键事件处理函数
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="handler">键盘按键事件处理函数，范例：handle()</param>
        public static TComponent OnKeyup<TComponent>(this TComponent component, string handler)
            where TComponent : IOption, IOnKeyup
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.OnKeyup, handler); });
            return component;
        }

        #endregion

        #region OnKeydown(键盘按下事件处理函数)

        /// <summary>
        /// 键盘按下事件处理函数
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="handler">键盘按下事件处理函数，范例：handle()</param>
        public static TComponent OnKeydown<TComponent>(this TComponent component, string handler)
            where TComponent : IOption, IOnKeydown
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.OnKeydown, handler); });
            return component;
        }

        #endregion

        #region OnSubmit(提交事件处理函数)

        /// <summary>
        /// 提交事件处理函数
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="handler">提交事件处理函数，范例：handle()</param>
        public static TComponent OnSubmit<TComponent>(this TComponent component, string handler)
            where TComponent : IOption, IOnSubmit
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.OnSubmit, handler); });
            return component;
        }

        #endregion
    }
}
