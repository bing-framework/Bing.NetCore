using System;
using System.Linq;
using System.Reflection;

namespace Bing.Reflection
{
    /// <summary>
    /// 公共实例方法查找器
    /// </summary>
    public class PublicInstanceMethodInfoFinder : IMethodInfoFinder
    {
        /// <summary>
        /// 查找指定条件的项
        /// </summary>
        /// <param name="type">要查找的类型</param>
        /// <param name="predicate">筛选条件</param>
        public MethodInfo[] Find(Type type, Func<MethodInfo, bool> predicate) => FindAll(type).Where(predicate).ToArray();

        /// <summary>
        /// 查找所有项
        /// </summary>
        /// <param name="type">要查找的类型</param>
        public MethodInfo[] FindAll(Type type) => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    }
}
