using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Core.Modularity;
using Bing.Core.Options;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Core.Builders
{
    /// <summary>
    /// Bing 构建器
    /// </summary>
    public class BingBuilder : IBingBuilder
    {
        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="BingBuilder"/>类型的实例
        /// </summary>
        public BingBuilder()
        {
            AddModules = new List<Type>();
            ExceptModules = new List<Type>();
        }

        #endregion

        #region 属性

        /// <summary>
        /// 加载的模块集合
        /// </summary>
        public IEnumerable<Type> AddModules { get; private set; }

        /// <summary>
        /// 排除的模块集合
        /// </summary>
        public IEnumerable<Type> ExceptModules { get; private set; }

        /// <summary>
        /// Bing 选项配置委托
        /// </summary>
        public Action<BingOptions> OptionsAction { get; private set; }

        #endregion

        #region AddModule(添加模块)

        /// <summary>
        /// 添加指定模块。执行此功能后将仅加载指定的模块
        /// </summary>
        /// <typeparam name="TModule">要添加的模块类型</typeparam>
        public IBingBuilder AddModule<TModule>() where TModule : BingModule
        {
            var list = AddModules.ToList();
            list.AddIfNotContains(typeof(TModule));
            AddModules = list;
            return this;
        }

        #endregion

        #region ExceptModule(排除模块)

        /// <summary>
        /// 排除指定模块。执行此功能后将从自动加载的模块中排除指定模块
        /// </summary>
        /// <typeparam name="TModule">要排除的模块类型</typeparam>
        public IBingBuilder ExceptModule<TModule>() where TModule : BingModule
        {
            var list = ExceptModules.ToList();
            list.AddIfNotContains(typeof(TModule));
            ExceptModules = list;
            return this;
        }

        #endregion

        #region AddOptions(添加选项配置)

        /// <summary>
        /// 添加Bing选项配置
        /// </summary>
        /// <param name="optionsAction">选项操作</param>
        public IBingBuilder AddOptions(Action<BingOptions> optionsAction)
        {
            Check.NotNull(optionsAction, nameof(optionsAction));
            OptionsAction = optionsAction;
            return this;
        }

        #endregion
    }
}
