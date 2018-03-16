using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.PlatformAbstractions;

namespace Bing.Reflections
{
    /// <summary>
    /// Web应用类型查找器
    /// </summary>
    public class WebAppTypeFinder : AppDomainTypeFinder
    {
        /// <summary>
        /// 获取程序集列表
        /// </summary>
        /// <returns></returns>
        public override List<Assembly> GetAssemblies()
        {
            LoadAssemblies(PlatformServices.Default.Application.ApplicationBasePath);
            return base.GetAssemblies();
        }
    }
}
