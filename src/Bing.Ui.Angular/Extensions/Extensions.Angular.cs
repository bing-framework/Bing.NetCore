using Bing.Ui.Angular;
using Bing.Ui.Builders;
using Bing.Ui.Configs;

namespace Bing.Ui.Extensions
{
    /// <summary>
    /// Angular 扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 添加 Angular 指令
        /// </summary>
        /// <typeparam name="TBuilder">生成器类型</typeparam>
        /// <param name="builder">生成器实例</param>
        /// <param name="config">配置</param>
        /// <returns></returns>
        public static TBuilder Angular<TBuilder>(this TBuilder builder, IConfig config) where TBuilder : TagBuilder
        {
            builder.NgIf(config).NgFor(config).NgClass(config);
            return builder;
        }

        /// <summary>
        /// 添加 NgIf 指令
        /// </summary>
        /// <typeparam name="TBuilder">生成器类型</typeparam>
        /// <param name="builder">生成器实例</param>
        /// <param name="config">配置</param>
        /// <returns></returns>
        public static TBuilder NgIf<TBuilder>(this TBuilder builder, IConfig config) where TBuilder : TagBuilder
        {
            builder.AddAttribute("*ngIf", config.GetValue(AngularConst.NgIf));
            return builder;
        }

        /// <summary>
        /// 添加 NgFor 指令
        /// </summary>
        /// <typeparam name="TBuilder">生成器类型</typeparam>
        /// <param name="builder">生成器实例</param>
        /// <param name="config">配置</param>
        /// <returns></returns>
        public static TBuilder NgFor<TBuilder>(this TBuilder builder, IConfig config) where TBuilder : TagBuilder
        {
            builder.AddAttribute("*ngFor", config.GetValue(AngularConst.NgFor));
            return builder;
        }

        /// <summary>
        /// 添加 NgClass 指令
        /// </summary>
        /// <typeparam name="TBuilder">生成器类型</typeparam>
        /// <param name="builder">生成器实例</param>
        /// <param name="config">配置</param>
        /// <returns></returns>
        public static TBuilder NgClass<TBuilder>(this TBuilder builder, IConfig config) where TBuilder : TagBuilder
        {
            builder.AddAttribute("[ngClass]", config.GetValue(AngularConst.NgClass));
            return builder;
        }

        /// <summary>
        /// 添加 路由链接 指令
        /// </summary>
        /// <typeparam name="TBuilder">生成器类型</typeparam>
        /// <param name="builder">生成器实例</param>
        /// <param name="config">配置</param>
        /// <returns></returns>
        public static TBuilder Link<TBuilder>(this TBuilder builder, IConfig config) where TBuilder : TagBuilder
        {
            builder.AddAttribute("href", config.GetValue(UiConst.Url));
            builder.AddAttribute("routerLink", config.GetValue(UiConst.Link));
            builder.AddAttribute("[routerLink]", config.GetValue(AngularConst.BindLink));
            builder.AddAttribute("routerLinkActive", config.GetValue(UiConst.Active));
            builder.AddAttribute("[routerLinkActive]", config.GetValue(AngularConst.BindActive));
            builder.AddAttribute("[queryParams]", config.GetValue(UiConst.QueryParams));
            if (config.GetValue<bool>(UiConst.Exact))
            {
                builder.AddAttribute("[routerLinkActiveOptions]", "{exact: true}");
            }
            return builder;
        }

        /// <summary>
        /// 添加 click 指令
        /// </summary>
        /// <typeparam name="TBuilder">生成器类型</typeparam>
        /// <param name="builder">生成器实例</param>
        /// <param name="config">配置</param>
        /// <returns></returns>
        public static TBuilder OnClick<TBuilder>(this TBuilder builder, IConfig config) where TBuilder : TagBuilder
        {
            builder.AddAttribute("(click)", config.GetValue(UiConst.OnClick));
            return builder;
        }
    }
}
