using System;
using System.Linq;
using Bing.Reflections;

namespace Bing.Core.Modularity
{
    /// <summary>
    /// Bing 模块类型查找器
    /// </summary>
    public class BingModuleTypeFinder : BaseTypeFinderBase<BingModule>, IBingModuleTypeFinder
    {
        /// <summary>
        /// 初始化一个<see cref="BingModuleTypeFinder"/>类型的实例
        /// </summary>
        /// <param name="allAssemblyFinder">所有程序集查找器</param>
        public BingModuleTypeFinder(IAllAssemblyFinder allAssemblyFinder) : base(allAssemblyFinder)
        {
        }

        /// <summary>
        /// 重写已实现所有项的查找
        /// </summary>
        protected override Type[] FindAllItems()
        {
            // 排除被继承的Module实类
            var types = base.FindAllItems();
            var baseModuleTypes = types.Select(x => x.BaseType)
                .Where(x => x != null && x.IsClass && !x.IsAbstract && !x.IsInterface)
                .ToArray();
            return types.Except(baseModuleTypes).ToArray();
        }
    }
}
