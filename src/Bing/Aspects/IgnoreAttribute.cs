using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bing.Aspects
{
    /// <summary>
    /// 忽略拦截 属性
    /// </summary>
    public class IgnoreAttribute:AspectCore.DynamicProxy.NonAspectAttribute
    {
    }
}
