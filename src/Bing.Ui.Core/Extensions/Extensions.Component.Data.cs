using Bing.Ui.Components;
using Bing.Ui.Components.Internal;
using Bing.Ui.Configs;
using Bing.Ui.Operations.Datas;

namespace Bing.Ui.Extensions
{
    /// <summary>
    /// 组件扩展 - 数据
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 设置Url
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="url">Url</param>
        /// <returns></returns>
        public static TComponent Url<TComponent>(this TComponent component, string url)
            where TComponent : IComponent, IUrl
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.Url, url); });
            return component;
        }

        /// <summary>
        /// 设置数据源
        /// </summary>
        /// <typeparam name="TComponent">组件类型</typeparam>
        /// <param name="component">组件实例</param>
        /// <param name="dataSource">数据源</param>
        /// <returns></returns>
        public static TComponent DataSource<TComponent>(this TComponent component, string dataSource)
            where TComponent : IComponent, IDataSource
        {
            var option = component as IOptionConfig;
            option?.Config<Config>(config => { config.SetAttribute(UiConst.DataSource, dataSource); });
            return component;
        }
    }
}
