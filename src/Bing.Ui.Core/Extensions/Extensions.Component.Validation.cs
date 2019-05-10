using Bing.Ui.Components;
using Bing.Ui.Components.Internal;
using Bing.Ui.Configs;
using Bing.Ui.Operations.Forms.Validations;

namespace Bing.Ui.Extensions
{
    /// <summary>
    /// 组件扩展 - 验证
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 必填项
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="message">错误消息</param>
        /// <returns></returns>
        public static TComponent Required<TComponent>(this TComponent component, string message = null)
            where TComponent : IRequired
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config =>
            {
                config.SetAttribute(UiConst.Required,true);
                config.SetAttribute(UiConst.RequiredMessage, message);
            });
            return component;
        }

        /// <summary>
        /// 最小长度
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="minLength">最小长度</param>
        /// <param name="message">错误消息</param>
        /// <returns></returns>
        public static TComponent MinLength<TComponent>(this TComponent component,int minLength, string message = null)
            where TComponent : IComponent,IMinLength
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config =>
            {
                config.SetAttribute(UiConst.MinLength, minLength);
                config.SetAttribute(UiConst.MinLengthMessage, message);
            });
            return component;
        }

        /// <summary>
        /// 最大长度
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="maxLength">最大长度</param>
        /// <param name="message">错误消息</param>
        /// <returns></returns>
        public static TComponent MaxLength<TComponent>(this TComponent component, int maxLength, string message = null)
            where TComponent : IComponent, IMaxLength
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config =>
            {
                config.SetAttribute(UiConst.MaxLength, maxLength);
                config.SetAttribute(UiConst.MaxMessage, message);
            });
            return component;
        }

        /// <summary>
        /// 最小值
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="min">最小值</param>
        /// <param name="message">错误消息</param>
        /// <returns></returns>
        public static TComponent Min<TComponent>(this TComponent component, int min, string message = null)
            where TComponent : IComponent, IMin
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config =>
            {
                config.SetAttribute(UiConst.Min, min);
                config.SetAttribute(UiConst.MinMessage, message);
            });
            return component;
        }

        /// <summary>
        /// 最大值
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="max">最大值</param>
        /// <param name="message">错误消息</param>
        /// <returns></returns>
        public static TComponent Max<TComponent>(this TComponent component, int max, string message = null)
            where TComponent : IComponent, IMax
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config =>
            {
                config.SetAttribute(UiConst.Max, max);
                config.SetAttribute(UiConst.MaxMessage, message);
            });
            return component;
        }

        /// <summary>
        /// 正则表达式验证
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="pattern">正则表达式</param>
        /// <param name="message">验证失败消息</param>
        /// <returns></returns>
        public static TComponent Max<TComponent>(this TComponent component, string pattern, string message)
            where TComponent : IComponent, IRegex
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config =>
            {
                config.SetAttribute(UiConst.Regex, pattern);
                config.SetAttribute(UiConst.RegexMessage, message);
            });
            return component;
        }
    }
}
